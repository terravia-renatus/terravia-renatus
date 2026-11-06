# Phase 0 Research: Core Economy, Village Resource Engine, Oasis Ecology, Starvation Mechanics & Village Expansion Rules

**Feature**: `002-economy-village-engine`  
**Date**: 2026-11-06  
**Spec**: [spec.md](./spec.md)

## Overview

This document consolidates technical research and architectural decisions for implementing the Core Economy, Village Resource Engine, Oasis Ecology, Deterministic Starvation Resolution, Culture Points, Celebrations, Capital Transfer, and Village Expansion Rules in compliance with the [Terravia Renatus Constitution](../../.specify/memory/constitution.md) and [Architecture Blueprint](../../docs/architecture_and_features.md).

---

## 1. Lazy $O(1)$ Continuous Resource & Loyalty Accrual + Exact Starvation Wakeup Scheduling

- **Decision**: Implement continuous resource (`Wood`, `Clay`, `Iron`, `Crop`) and village/oasis `Loyalty` accrual using lazy timestamp-delta evaluation ($\Delta t = (\text{NowMs} - \text{LastUpdatedMs}) / 1000.0$) inside `ResourceProductionCalculator`, paired with a dynamically scheduled one-shot `ITimerScheduler` wakeup (`StarvationCheckTimer`) on `VillageActor` whenever `NetCrop < 0`.
- **Rationale**:
  - **Constitutional Principle III (Lazy Evaluation Over Polling Loops)** strictly forbids 1-second background database sweeps over villages or oases.
  - When `NetCrop >= 0`, stored resources increase monotonically up to `[WarehouseCap, GranaryCap]` and never trigger an asynchronous state transition on their own; therefore, computing exact stocks lazily on any incoming command, query, or building/training timer completion is $O(1)$ and drift-free.
  - When `NetCrop < 0`, stored `Crop` depletes toward `0` and must trigger starvation at the exact millisecond `Crop` hits `0` (`FR-012`), even if no player views or commands the village. Whenever `NetCrop < 0` and `IsPeaceMode == false`, `VillageActor` computes:
    $$\Delta t_{\text{starveMs}} = \max\left(0, \left\lceil \frac{\text{StoredCrop}}{-\text{NetCrop}} \times 3,600,000 \right\rceil\right)$$
    and schedules a single actor timer (`TriggerVillageStarvationCommand`) at `NowMs + \Delta t_{\text{starveMs}}`. When a unit starves and credits its base `CropCost` back to `StoredCrop`, `VillageActor` recomputes `NetCrop` and reschedules the next timer for the newly credited crop buffer.
- **Alternatives Considered**:
  - *Periodic 1-second tick loop over all villages*: Rejected; violates Constitutional Principle III and creates $O(N)$ CPU/DB load across `25,000+` villages.
  - *Purely passive lazy starvation (only starving when the village is next inspected)*: Rejected; foreign reinforcements or outgoing armies must starve at the true wall-clock depletion time so combat arrivals elsewhere do not fight with "zombie" troops that should have starved minutes earlier.

---

## 2. Stateless 6-Step Production Pipeline & Unified `GrossCrop` Invariant

- **Decision**: Implement the 6-step resource production formula (`FR-003`) in a single stateless calculator (`IResourceProductionCalculator` / `ResourceProductionCalculator` in `Terravia.Domain.Villages`), using `ReadOnlySpan<int>` for slot types/levels and annexed oasis bonus counters, and invoke the **exact same** `CalculateGrossCropPerHour` method in both standard village production updates and `StarvationCalculator` (`FR-010`, `FR-012`).
- **Rationale**:
  - **Constitutional Principle II (Zero-Allocation Hot Paths & Pure Domain Services)** requires zero heap allocations on resource recalculations triggered by field upgrades, oasis annexations, Hero movements, or starvation ticks.
  - Guarantees strict mathematical equivalence between standard crop accrual and starvation resolution by ensuring Egyptian `Waterworks` (`gid 45`) amplification and stationed `Hero` crop production are always included when evaluating `NetCrop` during starvation loops.
  - Precomputes `MapTiles.BestOasisCropBonus` (`0%..150%`) for `9-cropper` (`FieldTopologyId = 1`) and `15-cropper` (`FieldTopologyId = 6`) tiles during world generation by scanning the `7x7` toroidal neighborhood ($|\Delta x| \le 3 \land |\Delta y| \le 3$) and summing the top `3` crop oasis bonuses across `OasisTypeId = 12` (`+50%`) and **all five** `+25%` crop oasis types (`OasisTypeId = 3, 6, 9, 10, 11`).
- **Alternatives Considered**:
  - *Separate crop calculation helper inside the starvation service*: Rejected; causes formula divergence and maintenance bugs.

---

## 3. Oasis Actor Lifecycle, Pre-Combat Hero Cage Capture & Annexation Protocol

- **Decision**: Model each oasis tile as an on-demand `OasisActor` (or managed sub-state coordinated with its parent `VillageActor` via `OasisAnnexationManager` in `Terravia.Domain.Oases`) with lazy resource/loyalty accrual and scheduled `NextNatureRegenAtMs` timers for unoccupied wild oases (`AnnexedByVillageId == null`).
- **Rationale**:
  - Unoccupied wild oases (`Tribe 4: Nature`) accrue up to `800` of each resource (`10 * Speed`/hr, or `20 * Speed`/hr for `OasisTypeId = 12`) and regenerate defending `Nature` fauna (`u31..u40`) on `NextNatureRegenAtMs` intervals scaled by $\text{growthFactor} = \min(3.0, \max(0.3, \text{avgPlayerProgress} / 1000))$ and `RichnessTier` (`0, 1, 2`).
  - **Hero Cage Capture (`FR-008`)**: Executed as a deterministic pre-combat phase in `OasisAnnexationManager.ResolveHeroCageCapture` when an attacking Hero arrives at an unoccupied oasis (`AnnexedByVillageId == null`) with unequipped Cages (`ItemTypeId = 114`). Captures `1` animal per Cage sequentially from `u31` to `u40`, removes consumed Cages from Hero inventory, and dispatches a `StationCapturedNatureAnimalsCommand` to the Hero's home `VillageActor` (`StackType = Reinforcement`, `OwnerVillageId = 0`, `0` crop upkeep, starvation-exempt).
  - **Annexation, Conquest & Release (`FR-009`)**:
    - Toroidal `7x7` distance check: $\min(|x_1 - x_2|, W - |x_1 - x_2|) \le 3$ and $\min(|y_1 - y_2|, H - |y_1 - y_2|) \le 3$, where $W = H = 2 \times \text{MapRadius} + 1$.
    - Slot formula: $\text{MaxOasisSlots} = \lfloor (\text{HeroMansionLevel} - 5) / 5 \rfloor \in \{0, 1, 2, 3\}$ at Levels `<10, 10..14, 15..19, 20`.
    - Player-annexed oasis loyalty hit: $\Delta \text{Loyalty} = \lfloor 100 / \min(3, 4 - N_{\text{defOases}}) \rfloor \in \{33, 50, 100\}$.
    - Manual abandonment (`AbandonAnnexedOasisCommand`) and automatic release on village conquest/razing return all stationed `Reinforcement` stacks to their `OwnerVillageId` and reset `AnnexedByVillageId = null, OwnerPlayerId = Nature, Loyalty = 100`.
- **Alternatives Considered**:
  - *Storing oasis state inside `VillageActor` even when unoccupied*: Rejected; wild oases have no owning village (`AnnexedByVillageId == null`), so `OasisState` must exist independently by `TileId` while notifying its owning `VillageActor` via immutable `*Eto` messages when annexed, raided, or conquered.

---

## 4. Deterministic 5-Tier Starvation Engine & Empty-Stack Cleanup (`StarvationCalculator`)

- **Decision**: Implement `IStarvationCalculator` / `StarvationCalculator` in `Terravia.Domain.Villages` as a pure, deterministic state evaluator operating over the 5 ordered candidate tiers:
  1. `OasisReinforcements` (foreign/allied `Reinforcement` stacks stationed at oases annexed by this village)
  2. `VillageReinforcements` (foreign/allied `Reinforcement` stacks in this village with `OwnerVillageId != 0`, excluding captured `Nature` animals `u31..u40`)
  3. `TrappedPrisoners` (`Trapped` stacks in Gaul `Trappers`, decrementing both total traps `u99` and occupied traps by `1`)
  4. `OwnStationedUnits` (`Home` troop stack in this village)
  5. `OwnMovingUnits` (outgoing/returning `Movements` belonging to this village)
- **Rationale**:
  - Within the first non-empty stack of the highest-priority non-empty tier, `StarvationCalculator` scans `u1..u90` (excluding `u31..u40`) and picks the unit type $u_k$ with the **strictly largest count** (ties broken by lowest `UnitTypeId`). A `Hero` in that stack is selected **only** when all `u1..u90` in that stack are `0` (setting `Hero.IsDead = true, Health = 0` and crediting `6` crop).
  - Whenever a `Reinforcement` stack, `Trapped` prisoner stack, or `Movement` reaches `0` total units and `0` Hero after a starvation decrement, the engine emits a `RemoveEmptyTroopStackDirective` / `CancelEmptyMovementDirective` so no orphaned zero-unit records remain in memory or SQL Server (`FR-012`).
- **Alternatives Considered**:
  - *Proportional or random starvation across stacks*: Rejected; violates `FR-012` and breaks deterministic testability (`SC-004`).

---

## 5. Culture Points, Celebrations, Capital Transfer & Conquest State Machine

- **Decision**: Coordinate multi-village Culture Points, Town Hall Celebrations, Teuton Brewery Mead Festivals, Capital transfers, expansion unit training caps, and village founding/conquest through `PlayerActor`, `VillageActor`, and three focused domain services in `Terravia.Domain.Villages`: `CelebrationManager`, `VillageExpansionManager`, and `SiegeAndConquestCalculator`.
- **Rationale**:
  - **Culture Points & Monotonic Curves (`FR-013`, `FR-014`)**:
    - Conquered `Archon` World Wonder villages (`IsArchon == true`) always report `0` passive daily CP toward `PlayerCultureLedger` (`FR-013`), while retaining their `50%` population and troop crop upkeep reduction (`FR-010`).
    - Both `Fast` and `Standard` CP threshold tables (`1..125` villages) are pre-populated in `VillageExpansionCatalog` using the strictly monotonic curve (`FR-014`), guaranteeing $\text{CP}(N+1) > \text{CP}(N)$ for all $N \ge 1$.
  - **Expansion Unit Training Cap (`FR-014`)**:
    - `VillageExpansionManager.CalculateAvailableExpansionUnitCapacity` awards `1, 2, 3` slots for `Palace` (`gid 26`) AND Hun `Command Center` (`gid 44`) at Levels `10, 15, 20` (and `1, 2` slots for `Residence gid 25` at Levels `10, 20`), converts unlocked slots to `SettlerEquivalents = UnlockedSlots * 3`, and subtracts `3 *OccupiedExpansionSlots` + `3 * TotalAdministrators` + `1 * TotalSettlers` across `Home`, `TrainingQueue`, `Movements`, `Reinforcements`, and `Trapped` stacks.
  - **Atomic Capital Transfer (`FR-015`)**:
    - Coordinated via `PlayerActor` (`ChangeCapitalVillageCommand`) between the old Capital `VillageActor` and new Capital `VillageActor`. Rejects if target `IsArchon == true` or lacks `Palace (26) / Command Center (44) >= 1`. Atomically downgrades old Capital `Slot 1..18` (`> 10` $\rightarrow$ `10`), destroys old Capital `gid 34` and `gid 35`, destroys new Capital `gid 29, 30, 38, 39, 49`, cancels invalidated build queue items, recalculates `Population`, `CulturePointsPerDay`, `WarehouseCap`, and `GranaryCap` (clamping stored resources), and triggers an **immediate persistence flush** (`Constitutional Principle IV`).
  - **Conquest Persuasion & Capture Reset (`FR-017`)**:
    - Evaluates `AttackerTotalPop > DefenderTotalPop` **before** transferring village ownership so the bigger-player `-1` building level penalty executes reliably.
    - Destroys all tribe-incompatible buildings (`gid 35, 36, 41, 44, 45, 48`) and Wall (`Slot 40`), releases all annexed oases to `Nature` (returning oasis reinforcements), cancels all active village queues (`Construction`, `Demolition`, `Training`, `Research`, `Celebrations`), clears Smithy/Academy upgrades, `Hospital` queues, Trade Routes, and own troop stacks, frees the prior parent village's `ExpansionSlot`, binds the attacker's launching village `ExpansionSlot`, and flushes immediately to SQL Server.
- **Alternatives Considered**:
  - *Allowing direct cross-village state mutation inside `VillageActor` during Capital transfer or conquest*: Rejected; violates **Constitutional Principle I (Actor Boundaries as DDD Aggregate Roots)**. Multi-village transitions are orchestrated via `PlayerActor` / `WorldCoordinatorActor` using immutable command/event messages (`*Command` / `*Eto`).
