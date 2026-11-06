# Implementation Plan: Core Game Catalog (Tribes, Units, Buildings & Construction Rules)

**Branch**: `[001-core-game-catalog]` | **Date**: 2026-11-06 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-core-game-catalog/spec.md`

## Summary

Implement the authoritative Core Game Catalog and Construction/Demolition Rule Engine for **Terravia Renatus**, encompassing all 9 tribes (`Romans, Teutons, Gauls, Nature, Archons, Huns, Egyptians, Spartans, Vikings`), 90 unit types (`u1..u90`) + Gaul Traps (`u99`) + 35 Hero unit templates (`h1..h86`), all 50 building types (`gid 1..50`) with level progressions (`Level 1..20` and `Level 1..100` for World Wonder) and prerequisite trees, and the deterministic Village Construction & Demolition Queue state machine (Roman dual-queue, Plus Subscription waiting loop, Master Builder reservations, Gold instant-completion exclusions, and timed/10-Gold instant demolition with Embassy safeguards).

Technically, the catalog is implemented as zero-allocation, immutable 1-based lookup tables (`ImmutableArray<T>` / `FrozenDictionary`) and pure stateless C# domain services (`IGameCatalog`, `IBuildingRuleCatalog`, `IConstructionCalculator`) in `Terravia.Domain`, integrated with `VillageActor` timers in `Terravia.Actors`, seeded/persisted via EF Core & Dapper TVPs in `Terravia.Infrastructure`, and exposed to `client/packages/game-contracts` via an `ETag`-cached `GET /api/game/catalog` endpoint in `Terravia.WebAPI`.

## Technical Context

**Language/Version**: C# 14 / .NET 10.0 (`net10.0` on backend) + TypeScript 5.x Strict (`client/packages/game-contracts`)

**Primary Dependencies**:
- `Terravia.Domain`: Pure .NET 10 BCL (`System.Collections.Frozen`, `System.Collections.Immutable`, `System.Text.Json`) — ZERO external framework dependencies
- `Terravia.Actors`: `Akka.NET` (`Akka`, `Akka.Hosting`)
- `Terravia.Infrastructure`: `Microsoft.EntityFrameworkCore.SqlServer` (catalog seeding/migrations) + `Dapper` (actor hydration & TVP write-behind)
- `Terravia.WebAPI`: ASP.NET Core Minimal APIs / Controllers + SignalR (`GameHub`)

**Storage**: Versioned embedded/seeded JSON balance catalog hydrated into read-only RAM tables at startup + SQL Server (`Villages`, `VillageBuildings`, `VillageQueueItems`, `GameAuditLogs`)

**Testing**: `xUnit`, `FsCheck` / property-based equivalence tests (`Terravia.Domain.Tests`), and `Akka.TestKit.Xunit2` (`Terravia.Actors.Tests`)

**Target Platform**: Single-World Linux/Windows container deployment (`server/`) + Browser SPA (`client/`)

**Project Type**: Distributed Actor-Model Real-Time Strategy Web Application (`server/` + `client/` monorepo)

**Performance Goals**:
- $O(1)$ catalog lookups (`< 10ns` per unit/building lookup via contiguous array indexing)
- `< 1ms` full-village building prerequisite and slot eligibility evaluation across all 40 slots + WW (`SC-004`)
- `< 250ms` micro-batch TVP persistence flush for routine queue mutations; immediate flush on Gold expenditure (`FinishAll`, `10-Gold Instant Demolition`, `Master Builder Activation`)

**Constraints**:
- 100% mathematical and rule parity with the canonical Terravia Renatus catalog specification (`specs/001-core-game-catalog/catalog-data.json` and `spec.md`)
- Zero heap allocations on hot prerequisite/cost evaluation paths (`ReadOnlySpan<int>` slot arrays)
- Strict Single-World schema (no `WorldId` multi-tenant columns)

**Scale/Scope**: 9 Tribes, 91 Unit definitions (`u1..u90, u99`), 35 Hero templates, 50 Building definitions (`1,020` total building-level stat rows across `gid 1..50`), 41 building slots per village across up to `25,000+` villages per game round.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Constitutional Principle | Pre-Research Check | Post-Design Check | Compliance Evidence |
| :--- | :--- | :--- | :--- |
| **I. Actor Boundaries as DDD Aggregate Roots** | ✅ PASS | ✅ PASS | `VillageActor` is the sole Aggregate Root mutating village building slots (`Slot 1..40, 99`), resources, and construction/demolition queues via serialized actor commands. |
| **II. Zero-Allocation Hot Paths & Pure Domain Services** | ✅ PASS | ✅ PASS | `IGameCatalog`, `IBuildingRuleCatalog`, and `IConstructionCalculator` are stateless pure C# services in `Terravia.Domain` operating over contiguous `int[41]` / `ReadOnlySpan<int>` slot arrays and 1-based `ImmutableArray` tables. |
| **III. Lazy Evaluation Over Polling Loops** | ✅ PASS | ✅ PASS | Zero background polling loops; resources are evaluated lazily at command arrival, and construction/demolition completions use exact `ITimerScheduler` timestamps in `VillageActor`. |
| **IV. Hybrid Data Access & Write-Behind Micro-Batching** | ✅ PASS | ✅ PASS | EF Core seeds static catalogs/migrations; Dapper hydrates `VillageBuildings` + `VillageQueueItems`; routine builds micro-batch via TVPs (`<= 250ms`) while Gold actions (`FinishAll`, `10-Gold Instant Demolish`, `Master Builder` activation) trigger immediate flushes. |
| **V. Strict Rule & Fog-of-War Parity** | ✅ PASS | ✅ PASS | Players, Sitters (validated against `SITTER_PERM_GOLD`), and Utility AI Bots all issue `ConstructOrUpgradeBuildingCommand` and `StartBuildingDemolitionCommand` through `IGameCommandGateway`. |
| **VI. Test-First & Canonical Mathematical Parity (TDD Mandatory)** | ✅ PASS | ✅ PASS | Full xUnit golden-vector suites assert 1:1 parity against the canonical Terravia Renatus catalog schema (`catalog-data.json`) and rule specification before implementation. |
| **Single-World Topology & Layering Boundaries** | ✅ PASS | ✅ PASS | No `WorldId` columns; `Terravia.Domain` has zero dependencies on Akka.NET, EF Core, or ASP.NET Core. |

## Project Structure

### Documentation (this feature)

```text
specs/001-core-game-catalog/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
│   ├── domain-services.md
│   └── catalog-api.md
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
server/
├── Terravia.sln
├── src/
│   ├── Terravia.Domain/
│   │   ├── Catalog/
│   │   │   ├── catalog-data.json
│   │   │   ├── CatalogConsts.cs
│   │   │   ├── CatalogJsonLoader.cs
│   │   │   ├── ResourceCost.cs
│   │   │   ├── TribeDefinition.cs
│   │   │   ├── UnitDefinition.cs
│   │   │   ├── HeroUnitTemplate.cs
│   │   │   ├── BuildingDefinition.cs
│   │   │   ├── BuildingLevelStats.cs
│   │   │   ├── IGameCatalog.cs
│   │   │   ├── FrozenGameCatalog.cs
│   │   │   ├── IBuildingRuleCatalog.cs
│   │   │   ├── BuildingRuleCatalog.cs
│   │   │   ├── IConstructionCalculator.cs
│   │   │   └── ConstructionCalculator.cs
│   │   ├── Villages/
│   │   │   ├── VillageConsts.cs
│   │   │   ├── VillageState.cs
│   │   │   ├── ConstructionQueueItem.cs
│   │   │   ├── DemolitionQueueItem.cs
│   │   │   ├── ConstructOrUpgradeBuildingCommand.cs
│   │   │   ├── CancelConstructionJobCommand.cs
│   │   │   ├── FinishAllConstructionsWithGoldCommand.cs
│   │   │   ├── StartBuildingDemolitionCommand.cs
│   │   │   ├── CancelBuildingDemolitionCommand.cs
│   │   │   └── VillageConstructionManager.cs
│   │   ├── IGameCommandGateway.cs
│   │   ├── CommandResult.cs
│   │   ├── SitterPermissions.cs
│   │   ├── TerraviaDomainErrorCodes.cs
│   │   └── TerraviaPermissions.cs
│   ├── Terravia.Actors/
│   │   └── Villages/
│   │       ├── VillageActor.cs
│   │       ├── GetVillageStateQuery.cs
│   │       ├── ProcessDueVillageTimersCommand.cs
│   │       ├── VillageConstructionChangedEto.cs
│   │       ├── VillageStateFlushEto.cs
│   │       └── VillageSlotDto.cs
│   ├── Terravia.Infrastructure/
│   │   ├── TerraviaDbProperties.cs
│   │   └── Villages/
│   │       ├── VillageBuilding.cs
│   │       ├── VillageQueueItem.cs
│   │       ├── VillageQueueCategory.cs
│   │       └── DapperVillageRepository.cs
│   └── Terravia.WebAPI/
│       └── Catalog/
│           └── GameCatalogEndpoints.cs
└── tests/
    ├── Terravia.Domain.Tests/
    │   └── Catalog/
    │       ├── TribeAndUnitCatalogEquivalenceTests.cs
    │       ├── BuildingCatalogAndPrerequisiteTests.cs
    │       └── ConstructionCalculatorTests.cs
    └── Terravia.Actors.Tests/
        └── Villages/
            └── VillageConstructionQueueActorTests.cs

client/
└── packages/
    └── game-contracts/
        └── src/
            ├── catalog.types.ts
            └── construction.helpers.ts
```

**Structure Decision**: Adheres strictly to the Constitutional 5-project backend (`server/src/Terravia.{Domain,Actors,BotAI,Infrastructure,WebAPI}` + `server/tests/`) and `client/packages/game-contracts` monorepo layout defined in `docs/architecture_and_features.md` Section 2.

## Complexity Tracking

> Zero constitutional violations. No complexity exceptions required.
