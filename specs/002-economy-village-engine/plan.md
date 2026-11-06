# Implementation Plan: Core Economy, Village Resource Engine, Oasis Ecology, Starvation Mechanics & Village Expansion Rules

**Branch**: `[002-economy-village-engine]` | **Date**: 2026-11-06 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-economy-village-engine/spec.md`

## Summary

Implement the authoritative Core Economy, Village Resource Engine, Oasis Ecology, Deterministic Starvation Resolution, Culture Points, Celebrations, Capital Transfer, and Village Expansion Engine for **Terravia Renatus** (`FR-001` through `FR-017`).

Technically, the feature is implemented using zero-allocation, stateless C# domain calculators and managers (`ResourceProductionCalculator`, `StarvationCalculator`, `OasisAnnexationManager`, `CelebrationManager`, `VillageExpansionManager`, `SiegeAndConquestCalculator`) in `Terravia.Domain`, coordinated via lazy $O(1)$ timestamp-delta accrual and exact `ITimerScheduler` timers across `VillageActor`, `OasisActor`, and `PlayerActor` in `Terravia.Actors`, persisted via Dapper TVP micro-batches (with immediate flushes on Capital transfer, village founding, conquest, and oasis annexation/abandonment) in `Terravia.Infrastructure`, and shared with `client/packages/game-contracts` for 60fps client-side resource and loyalty interpolation.

## Technical Context

**Language/Version**: C# 14 / .NET 10.0 (`net10.0` on backend) + TypeScript 5.x Strict (`client/packages/game-contracts`)

**Primary Dependencies**:
- `Terravia.Domain`: Pure .NET 10 BCL (`System.Collections.Frozen`, `System.Collections.Immutable`) — ZERO external framework dependencies
- `Terravia.Actors`: `Akka.NET` (`Akka`, `Akka.Hosting`)
- `Terravia.Infrastructure`: `Microsoft.EntityFrameworkCore.SqlServer` (migrations & world seeding) + `Dapper` (actor hydration & TVP write-behind)
- `Terravia.WebAPI`: ASP.NET Core Minimal APIs + SignalR (`GameHub`)

**Storage**: SQL Server (`Villages`, `VillageBuildings`, `VillageQueueItems`, `VillageTroopStacks`, `MapTiles`, `OasisState`, `Players`)

**Testing**: `xUnit`, `FsCheck` property-based tests (`Terravia.Domain.Tests`), `Akka.TestKit.Xunit2` virtual-time actor tests (`Terravia.Actors.Tests`), and Vitest (`client/packages/game-contracts`)

**Target Platform**: Single-World Linux/Windows container deployment (`server/`) + Browser SPA (`client/`)

**Project Type**: Distributed Actor-Model Real-Time Strategy Web Application (`server/` + `client/` monorepo)

**Performance Goals**:
- $O(1)$ lazy resource & loyalty accrual (`< 50ns` per village evaluation with zero heap allocations)
- `< 100µs` deterministic 5-tier starvation step resolution across all stationed/reinforcing/trapped/moving stacks
- `<= 250ms` Dapper TVP write-behind micro-batch flush for routine resource/loyalty updates; immediate transactional flush on Capital transfer, village settlement, village conquest, and oasis annexation/abandonment

**Constraints**:
- 100% mathematical parity with the canonical formulas, tables, and clarifications in `specs/002-economy-village-engine/spec.md` and `specs/001-core-game-catalog/catalog-data.json`
- Zero background polling loops (`Constitutional Principle III`): starvation and celebration completions use exact scheduled actor timers
- Strict adherence to **One Type Per File (`1:1`)** and **Vertical Feature-Folder Organization** (`Terravia.Domain.Villages`, `Terravia.Domain.Oases`, `Terravia.Domain.Players`)

**Scale/Scope**: 12 Village Field Topologies (`Type 1..12`), 12 Oasis Types (`Type 1..12`), 6-step production pipeline, 5-tier starvation engine, `Fast` & `Standard` monotonic CP curves (`1..125` villages), scaling across `25,000+` villages and `40,000+` oases per game round.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Constitutional Principle | Pre-Research Check | Post-Design Check | Compliance Evidence |
| :--- | :--- | :--- | :--- |
| **I. Actor Boundaries as DDD Aggregate Roots** | ✅ PASS | ✅ PASS | `VillageActor`, `OasisActor`, and `PlayerActor` each serve as the sole Aggregate Root for their state; cross-aggregate flows (Capital transfer, oasis annexation/release, village settlement/conquest) communicate exclusively via immutable `*Command` and `*Eto` messages. |
| **II. Zero-Allocation Hot Paths & Pure Domain Services** | ✅ PASS | ✅ PASS | `ResourceProductionCalculator`, `StarvationCalculator`, `OasisAnnexationManager`, `CelebrationManager`, `VillageExpansionManager`, and `SiegeAndConquestCalculator` are pure C# services in `Terravia.Domain` operating on `ReadOnlySpan<int>` slot/unit arrays and `readonly record struct` contexts. |
| **III. Lazy Evaluation Over Polling Loops** | ✅ PASS | ✅ PASS | Zero 1-second polling sweeps; resources and loyalty accrue lazily via `nowMs - lastUpdatedMs`, while crop-zero starvation (`TriggerVillageStarvationCommand`), `Town Hall` celebrations, `Brewery` Mead Festivals, and `Nature` fauna regeneration use exact `ITimerScheduler` timers. |
| **IV. Hybrid Data Access & Write-Behind Micro-Batching** | ✅ PASS | ✅ PASS | Routine resource/loyalty accrual flushes via 250ms Dapper TVP micro-batches (`DapperVillageRepository`, `DapperOasisRepository`); high-stakes state transitions (Capital transfer, village founding, village conquest, oasis annexation/abandonment) trigger immediate persistence flushes. |
| **V. Strict Rule & Fog-of-War Parity** | ✅ PASS | ✅ PASS | Players, Sitters, and Utility AI Bots invoke celebrations, Mead Festivals, Capital transfers, oasis abandonment, and expansion actions exclusively through `IGameCommandGateway`. |
| **VI. Test-First & Canonical Mathematical Parity (TDD Mandatory)** | ✅ PASS | ✅ PASS | Golden-vector unit tests (`Terravia.Domain.Tests`) and `Akka.TestKit` virtual-time suites (`Terravia.Actors.Tests`) verify `SC-001` through `SC-005` before implementation. |
| **Naming & Structural Conventions (v1.1.0)** | ✅ PASS | ✅ PASS | Enforces vertical feature folders (`Villages/`, `Oases/`, `Players/`), 1-file-per-type (`1:1`), `*Manager` / `*Calculator` / `*Catalog` suffixes, singular persistence nouns (`Village`, `OasisState`, `MapTile`, `VillageTroopStack`), `*Eto` / `*Dto` / `*Command` / `*Query`, and `TerraviaDomainErrorCodes`. |

## Project Structure

### Documentation (this feature)

```text
specs/002-economy-village-engine/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
│   ├── domain-services.md
│   └── economy-expansion-api.md
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
server/
├── Terravia.sln
├── src/
│   ├── Terravia.Domain/
│   │   ├── Villages/
│   │   │   ├── FieldTopologyDefinition.cs
│   │   │   ├── VillageFieldTopologyCatalog.cs
│   │   │   ├── HeroResourceFocusMode.cs
│   │   │   ├── VillageProductionContext.cs
│   │   │   ├── ResourceProductionSnapshot.cs
│   │   │   ├── VillageAccrualInput.cs
│   │   │   ├── LazyAccrualResult.cs
│   │   │   ├── IResourceProductionCalculator.cs
│   │   │   ├── ResourceProductionCalculator.cs
│   │   │   ├── TroopStackType.cs
│   │   │   ├── StarvationPriorityTier.cs
│   │   │   ├── StarvationCandidateStack.cs
│   │   │   ├── StarvationEvaluationContext.cs
│   │   │   ├── StarvationStepResult.cs
│   │   │   ├── IStarvationCalculator.cs
│   │   │   ├── StarvationCalculator.cs
│   │   │   ├── CelebrationType.cs
│   │   │   ├── CelebrationStartResult.cs
│   │   │   ├── MeadFestivalStartResult.cs
│   │   │   ├── CelebrationManager.cs
│   │   │   ├── CulturePointCurveMode.cs
│   │   │   ├── VillageExpansionCatalog.cs
│   │   │   ├── ExpansionQuotaContext.cs
│   │   │   ├── ExpansionTrainingQuota.cs
│   │   │   ├── VillageCapitalState.cs
│   │   │   ├── CapitalTransferPlan.cs
│   │   │   ├── VillageExpansionManager.cs
│   │   │   ├── ConquestEvaluationContext.cs
│   │   │   ├── ConquestResolutionResult.cs
│   │   │   ├── SiegeAndConquestCalculator.cs
│   │   │   ├── StartTownHallCelebrationCommand.cs
│   │   │   ├── StartBreweryMeadFestivalCommand.cs
│   │   │   ├── ChangeCapitalVillageCommand.cs
│   │   │   ├── FoundNewVillageCommand.cs
│   │   │   └── TriggerVillageStarvationCommand.cs
│   │   ├── Oases/
│   │   │   ├── OasisConsts.cs
│   │   │   ├── OasisFaunaSpawnRule.cs
│   │   │   ├── OasisTypeDefinition.cs
│   │   │   ├── OasisEcologyCatalog.cs
│   │   │   ├── OasisCoordinateBonus.cs
│   │   │   ├── HeroCageCaptureResult.cs
│   │   │   ├── OasisStrikeContext.cs
│   │   │   ├── OasisConquestEvaluation.cs
│   │   │   ├── OasisAnnexationManager.cs
│   │   │   └── AbandonAnnexedOasisCommand.cs
│   │   └── Players/
│   │       ├── PlayerConsts.cs
│   │       └── PlayerCultureLedger.cs
│   ├── Terravia.Actors/
│   │   ├── Villages/
│   │   │   ├── VillageActor.cs
│   │   │   ├── VillageEconomySnapshotDto.cs
│   │   │   ├── VillageEconomyChangedEto.cs
│   │   │   ├── VillageStarvationExecutedEto.cs
│   │   │   └── VillageConqueredEto.cs
│   │   ├── Oases/
│   │   │   ├── OasisActor.cs
│   │   │   ├── OasisStateFlushEto.cs
│   │   │   └── OasisOwnershipChangedEto.cs
│   │   └── Players/
│   │       ├── PlayerActor.cs
│   │       ├── PlayerCultureSnapshotDto.cs
│   │       └── CapitalVillageTransferredEto.cs
│   ├── Terravia.Infrastructure/
│   │   ├── Villages/
│   │   │   ├── Village.cs
│   │   │   ├── VillageTroopStack.cs
│   │   │   └── DapperVillageRepository.cs
│   │   ├── Oases/
│   │   │   ├── MapTile.cs
│   │   │   ├── OasisState.cs
│   │   │   └── DapperOasisRepository.cs
│   │   └── Players/
│   │       ├── Player.cs
│   │       └── DapperPlayerRepository.cs
│   └── Terravia.WebAPI/
│       └── Villages/
│           └── VillageEconomyEndpoints.cs
└── tests/
    ├── Terravia.Domain.Tests/
    │   ├── Villages/
    │   │   ├── VillageFieldTopologyAndCropFinderTests.cs
    │   │   ├── ResourceProductionAndStorageCalculatorTests.cs
    │   │   ├── StarvationCalculatorTests.cs
    │   │   ├── CelebrationAndMeadFestivalManagerTests.cs
    │   │   └── VillageExpansionAndConquestCalculatorTests.cs
    │   └── Oases/
    │       └── OasisEcologyAndAnnexationManagerTests.cs
    └── Terravia.Actors.Tests/
        ├── Villages/
        │   ├── VillageEconomyAndStarvationActorTests.cs
        │   └── VillageExpansionAndCapitalTransferActorTests.cs
        └── Oases/
            └── OasisLifecycleActorTests.cs

client/
└── packages/
    └── game-contracts/
        └── src/
            ├── economy.types.ts
            ├── economy.interpolation.ts
            └── economy.interpolation.test.ts
```

**Structure Decision**: Adheres strictly to the Constitutional 5-project backend (`server/src/Terravia.{Domain,Actors,BotAI,Infrastructure,WebAPI}` + `server/tests/`) and `client/packages/game-contracts` monorepo layout, organized into vertical bounded feature folders (`Villages/`, `Oases/`, `Players/`) with a strict `1:1` file-per-type mapping.

## Complexity Tracking

> Zero constitutional violations. No complexity exceptions required.
