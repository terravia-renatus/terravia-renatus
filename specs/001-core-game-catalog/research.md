# Phase 0 Research: Core Game Catalog (Tribes, Units, Buildings & Construction Rules)

**Feature**: `001-core-game-catalog`
**Date**: 2026-11-06
**Spec**: [spec.md](./spec.md)

## Overview

This document consolidates technical research and architectural decisions for implementing the Core Game Catalog (9 Tribes, 90 Units + `u99` Traps + 35 Hero Templates, 50 Buildings with level progressions and prerequisites, and Construction/Demolition Queue rules) in compliance with the [Terravia Renatus Constitution](../../.specify/memory/constitution.md) and [Architecture Blueprint](../../docs/architecture_and_features.md).

---

## 1. Zero-Allocation In-Memory Catalog Representation

- **Decision**: Hydrate all static catalog definitions (Tribes `1..9`, Units `1..90, 99`, Hero Templates `h1..h86`, Buildings `1..50`, and Unit Research Prerequisites `r1..r90`) at application startup from versioned JSON catalog assets (seeded/mirrored in SQL Server via EF Core) into immutable, contiguous 1-based lookup arrays and `FrozenDictionary<TKey, TValue>` structures inside `Terravia.Domain`.
- **Rationale**:
  - **Constitutional Principle II (Zero-Allocation Hot Paths & Pure Domain Services)** requires high-frequency combat simulation, building prerequisite evaluation, and Utility AI scoring (`>= 50,000` simulations/sec) to execute without heap allocations or dictionary hash collisions.
  - Because `TribeId` (`1..9`), `UnitTypeId` (`1..90`, plus `99`), and `BuildingTypeId` (`1..50`) are dense, bounded integer ranges, flat `ReadOnlyMemory<T>` / `ImmutableArray<T>` tables indexed directly by ID achieve $O(1)$ array-offset lookups (`~1ns`) with zero boxing or GC pressure.
- **Alternatives Considered**:
  - *Querying SQL Server or EF Core `DbContext` per request*: Rejected; violates Principle III and IV and introduces millisecond I/O latency on hot paths.
  - *Standard mutable `Dictionary<int, T>`*: Rejected; slightly higher lookup overhead and lacks compile-time immutability guarantees compared to `ImmutableArray<T>` / `FrozenDictionary<int, T>`.

---

## 2. Declarative + Special-Case Building Prerequisite Engine (`IBuildingRuleCatalog`)

- **Decision**: Implement `IBuildingRuleCatalog` as a stateless, pure C# domain service in `Terravia.Domain` that combines a declarative rule table (`BuildingPrerequisiteRule` for standard `levels`, `exact`, `allowedTribes`, `deniedTribes`, `capitalRequirement`, `isUnique`, and `rebuildAtLevel` constraints) with explicit deterministic evaluators for the 7 context-sensitive building types (`gid 25` Residence, `gid 26` Palace, `gid 34` Stonemason's Lodge, `gid 38` Great Warehouse, `gid 39` Great Granary, `gid 40` World Wonder, and `gid 49` Great Workshop).
- **Rationale**:
  - Combines declarative metadata (serializable to `GET /api/game/catalog`) with fast, deterministic evaluation of multi-village and alliance-scoped invariants.
  - Keeps `VillageStateSnapshot` (passed as a lightweight `readonly ref struct` or value snapshot containing `SlotTypes[41]`, `SlotLevels[41]`, `WorldWonderLevel`, `QueuedMaxWorldWonderLevel`, `TribeId`, `IsCapital`, `IsArchon`, `HasAccountPalace`, `HasGreatStorageArtifact`, `OwnedWwPlanCount`, `AllianceWwPlanCount`, and `GreatWorkshopEnabled`) completely decoupled from Akka.NET and database I/O.
  - Enforces three critical domain invariants:
    1. Huns (`Tribe 6`) satisfy Stonemason's Lodge (`gid 34`) capital prerequisite via Command Center (`gid 44 >= 3`) instead of Palace (`gid 26 >= 3`).
    2. Account-wide Palace (`gid 26`) uniqueness strictly inspects inner building slot types (`Slot 19..40` `BuildingTypeId == 26`), preventing false positives when a village ID or building level equals `26`.
    3. World Wonder (`gid 40`) upgrade eligibility checks `max(CurrentSlot99Level, MaxQueuedSlot99Level)` across the entire build queue so Master Builder / Plus queueing at Level 48 (`49, 50, 51`) cannot bypass the second Alliance WW Building Plan requirement at Level 50.
- **Alternatives Considered**:
  - *Hardcoding a 50-branch switch statement for all buildings*: Rejected; harder to maintain and serialize to client catalogs (`GET /api/game/catalog`).
  - *Dynamic expression-tree scripting*: Rejected; unnecessary complexity and slower than compiled struct rules.

---

## 3. Exact Construction Time, Queue Chaining & Demolition Math

- **Decision**: Implement the canonical construction, queue chaining, and demolition mathematical formulas inside `IConstructionCalculator` and `VillageConstructionService` in `Terravia.Domain`:
  1. **Construction Duration (`CalculateBuildTimeSeconds`)**:
     - For `gid == 15` (Main Building):
       - If current Main Building level is `0`: `round(baseTime / Speed * 5)`
       - If current Main Building level is `> 0`: `round(baseTime / Speed)`
     - For `gid != 15`:
       - If current Main Building level $M > 0$: `round(baseTime * (MainBuildingLevelStats[M].AttributeValue / 100.0) / Speed)` (where `MainBuildingLevelStats[M].AttributeValue` is the exact percentage table `100, 96.4, 92.93, ..., 49.83` from `catalog-data.json`).
       - If current Main Building level $M == 0$: `round(baseTime * 5 / Speed)`.
  2. **Roman Dual-Queue & Plus Subscription Waiting Loop (`IsWaitingLoop`)**:
     - Non-Roman concurrency: `1` active (`basic`) + `1` Plus waiting loop (`IsWaitingLoop = true`) + up to `MAX_MASTER_BUILDER` (`IsMasterBuilder = true`) reserved slots.
     - Roman concurrency (`TribeId == 1` or `ServerConfig.AllowAllTribeRomanQueue`): `1` active Resource Field (`Slot 1..18`) + `1` active Inner Building (`Slot 19..40, 99`) + `1` Plus waiting loop (`IsWaitingLoop = true`).
     - Waiting loop start timestamp: chains onto the completion timestamp of the preceding job in the same queue partition (`Slot <= 18` vs. `Slot >= 19` for Romans; global active job for non-Romans) plus `ceil(60.0 / Speed)` seconds.
     - Multi-level same-slot queueing: Target level is `CurrentSlotLevel + 1 + CountOfExistingQueuedJobsForSlot`.
  3. **Timed & Instant Demolition (`Main Building >= 10`)**:
     - Timed 1-level demolition duration: `floor(CalculateBuildTimeSeconds(gid, currentLevel) / 2.0)` (half of the current level's construction time).
     - Embassy safeguard: If `gid == 18`, player is Alliance founder (`IsAllianceOwner`), `AllianceMemberCount > 1`, `CurrentEmbassyLevel == max(3, ceil(AllianceMemberCount / 3.0))`, and the player owns only `1` Embassy across all villages at `>= minEmbassyLevel`, demolition (both timed and 10-Gold instant) is blocked with error code `CannotDemolishLastAllianceEmbassy`.
     - 10-Gold Instant Complete Demolition: Immediately sets slot level and type to `0`, cancels any active demolition, subtracts $\sum_{i=1}^{L} \text{pop}(i)$ from village population, recalculates Warehouse/Granary capacity if `gid in {10, 11, 38, 39}`, and recounts village Culture Points.
- **Rationale**: Guarantees 100% deterministic numeric and behavioral accuracy (`Constitutional Principle VI`).
- **Alternatives Considered**:
  - *Computing Main Building time reduction via raw `Math.Pow(0.964, M - 1)` instead of `MainBuildingLevelStats[M].AttributeValue / 100.0`*: Rejected; the catalog stores rounded 2-decimal `AttributeValue` percentages (`92.93`, `89.58`, etc.), so using the exact catalog table avoids 1-second rounding drift in golden-vector tests.

---

## 4. Shared Client-Server Catalog Contract & ETag Caching

- **Decision**: Expose the complete compiled catalog via `GET /api/game/catalog` in `Terravia.WebAPI` with deterministic SHA-256 content hashing (`ETag` / `If-None-Match` returning `304 Not Modified`) and mirror the TypeScript contract types and pure client-side eligibility/cost helpers in `client/packages/game-contracts`.
- **Rationale**:
  - Both `client/apps/game-ui` (`BootAndPreloadScene`, `UIScene` building modal, troop training modal) and `client/apps/admin-ui` (Catalog Inspector) need instant local access to all 9 tribes, 90 units, and 50 buildings without round-tripping to the server on every tooltip or slot click.
- **Alternatives Considered**:
  - *Fetching building/unit stats per modal click over SignalR*: Rejected; adds unnecessary network chatter for static round data.
