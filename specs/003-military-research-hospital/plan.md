# Implementation Plan: Military Recruitment, Technological Research, Specialty Crafting, and Hospital Casualty Recovery

**Branch**: `[003-military-research-hospital]` | **Date**: 2026-11-07 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/003-military-research-hospital/spec.md`

## Summary

Implement the authoritative Military Recruitment Queues, Great Military Buildings (`3x` cost parallel lanes), Deterministic 4-Stage Training Speed Pipeline, Academy (`gid 22`) Unit Discovery, Blacksmith/Armoury (`gid 12/13`) Combat Upgrades, Hospital (`gid 46`) & Spartan/Viking Big Hospital (`gid 48`) Casualty Recovery, and Gaul Trapper (`gid 36`) Prisoner Lifecycle & Free Trap Auto-Repair for **Terravia Renatus** (`FR-001` through `FR-015`).

Technically, the feature is built using pure, zero-allocation C# domain calculators and managers (`TrainingTimeCalculator`, `CombatUpgradeCalculator`, `MilitaryTrainingManager`, `TechnologyResearchManager`, `HospitalHealingManager`, `TrapperManager`) in `Terravia.Domain.Villages`, orchestrated via exact `ITimerScheduler` single-unit graduation and research completion timers on `VillageActor` in `Terravia.Actors.Villages`, persisted via Dapper TVP micro-batches and immediate Gold/tactical flushes in `Terravia.Infrastructure.Villages`, exposed through Sitter-validated endpoints in `Terravia.WebAPI.Villages`, and mirrored in `client/packages/game-contracts` for real-time queue interpolation.

## Technical Context

**Language/Version**: C# 14 / .NET 10.0 (`net10.0` on backend) + TypeScript 5.x Strict (`client/packages/game-contracts`)  
**Primary Dependencies**:
- `Terravia.Domain`: Pure .NET 10 BCL (`System.Collections.Frozen`, `System.Collections.Immutable`) — ZERO external framework dependencies
- `Terravia.Actors`: `Akka.NET` (`Akka`, `Akka.Hosting`)
- `Terravia.Infrastructure`: `Microsoft.EntityFrameworkCore.SqlServer` + `Dapper` (TVP hydration & write-behind)
- `Terravia.WebAPI`: ASP.NET Core Minimal APIs + SignalR (`GameHub`)

**Storage**: SQL Server (`Villages`, `VillageBuildings`, `VillageTrainingQueues`, `VillageResearchQueues`, `VillageTechnologies`, `VillageTroopStacks`)  
**Testing**: `xUnit`, `FsCheck` property-based tests (`Terravia.Domain.Tests`), `Akka.TestKit.Xunit2` virtual-time actor tests (`Terravia.Actors.Tests`), and Vitest (`client/packages/game-contracts`)  
**Target Platform**: Single-World Linux/Windows container deployment (`server/`) + Browser SPA (`client/`)  
**Project Type**: Distributed Actor-Model Real-Time Strategy Web Application (`server/` + `client/` monorepo)  
**Performance Goals**:
- `< 50ns` zero-allocation execution for 4-stage `TrainingTimeCalculator` and `CombatUpgradeCalculator`
- $O(1)$ per-lane lazy unit graduation evaluation (`floor(elapsed / eachtime)`) with zero background polling loops
- `<= 250ms` Dapper TVP micro-batch write-behind flush for routine queue progress; immediate transactional flush on `2` Gold instant research completion and combat prisoner/casualty mutations

**Constraints**:
- 100% mathematical parity with `specs/003-military-research-hospital/spec.md` and `specs/001-core-game-catalog/catalog-data.json` (`SC-001`..`SC-005`)
- Strict adherence to **One Type Per File (`1:1`)** and **Vertical Feature-Folder Organization** (`Terravia.Domain.Villages`, `Terravia.Actors.Villages`, `Terravia.Infrastructure.Villages`, `Terravia.WebAPI.Villages`)
- Single-pass Trainer Artifact (`Kind 5`), sequential single-lane Hospital `Healing` queue, and Snapshot-at-Dispatch Smithy upgrade levels

**Scale/Scope**: 9 concurrent facility queue categories (`Barracks`, `GreatBarracks`, `Stable`, `GreatStable`, `Workshop`, `GreatWorkshop`, `ResidencePalace`, `Trapper`, `Healing`), 3 research lanes (`Academy`, `Blacksmith`, `Armoury`), 91 unit definitions across 9 tribes (`7` playable + `Nature` + `Archon`), scaling across `25,000+` active villages per game round.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Constitutional Principle | Pre-Research Check | Post-Design Check | Compliance Evidence |
| :--- | :--- | :--- | :--- |
| **I. Actor Boundaries as DDD Aggregate Roots** | ✅ PASS | ✅ PASS | `VillageActor` is the sole Aggregate Root for all training queues, research queues, Hospital wounded pools, and Trapper prisoner stacks; outbound armies snapshot `OffenseUpgradeLevels` / `DefenseUpgradeLevels` at dispatch (Clarification Q3) so zero cross-actor state reads occur during combat. |
| **II. Zero-Allocation Hot Paths & Pure Domain Services** | ✅ PASS | ✅ PASS | `TrainingTimeCalculator`, `CombatUpgradeCalculator`, `MilitaryTrainingManager`, `TechnologyResearchManager`, `HospitalHealingManager`, and `TrapperManager` are stateless C# services in `Terravia.Domain.Villages` operating on `ReadOnlySpan<int>` and `readonly record struct` inputs. |
| **III. Lazy Evaluation Over Polling Loops** | ✅ PASS | ✅ PASS | Zero 1-second database sweeps; unit graduations (`CommenceAtMs + k * EachTimeSeconds * 1000L`) and research completions are evaluated lazily on command arrival and triggered via exact `ITimerScheduler` timers (`MilitaryTrainingTimerKey`, `ResearchTimerKey`). |
| **IV. Hybrid Data Access & Write-Behind Micro-Batching** | ✅ PASS | ✅ PASS | Routine training/research queue updates batch via `DapperVillageRepository` TVPs; `2` Gold instant research completion and combat casualty/prisoner updates trigger immediate flushes. |
| **V. Strict Rule & Fog-of-War Parity** | ✅ PASS | ✅ PASS | All recruitment, healing, research, Gold completion, and prisoner commands enforce `SitterPermissions` (`BuildAndDemolish`, `BuyAndSpendGold`, `Reinforce`) prior to execution (Clarification Q4). |
| **VI. Test-First & Canonical Mathematical Parity (TDD Mandatory)** | ✅ PASS | ✅ PASS | Golden-vector unit tests (`Terravia.Domain.Tests`) and `Akka.TestKit` virtual-time suites (`Terravia.Actors.Tests`) validate `SC-001` through `SC-005` prior to implementation. |
| **Naming & Structural Conventions (v1.1.0)** | ✅ PASS | ✅ PASS | Enforces vertical feature folders (`Villages/`), strict `1:1` file-to-type mapping, `*Manager` / `*Calculator` suffixes, singular persistence nouns, `*Eto` / `*Dto` / `*Command` suffixes, and `TerraviaDomainErrorCodes.Villages` constants. |

## Project Structure

### Documentation (this feature)

```text
specs/003-military-research-hospital/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
│   ├── domain-services.md
│   └── military-research-api.md
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
server/
├── Terravia.sln
├── src/
│   ├── Terravia.Domain/
│   │   ├── TerraviaDomainErrorCodes.cs                  # Extended with military/research/hospital/trapper codes
│   │   └── Villages/
│   │       ├── TrainingFacilityCategory.cs
│   │       ├── ResearchCategory.cs
│   │       ├── HeroRecruitmentHelmetKind.cs
│   │       ├── HeroBandageTier.cs
│   │       ├── TrainingSpeedContext.cs
│   │       ├── ITrainingTimeCalculator.cs
│   │       ├── TrainingTimeCalculator.cs
│   │       ├── UpgradedUnitCombatStats.cs
│   │       ├── ICombatUpgradeCalculator.cs
│   │       ├── CombatUpgradeCalculator.cs
│   │       ├── TrainingQueueItem.cs
│   │       ├── ResearchQueueItem.cs
│   │       ├── PrisonerStack.cs
│   │       ├── EnqueueMilitaryTrainingCommand.cs
│   │       ├── TrainingEnrollmentResult.cs
│   │       ├── MilitaryTrainingManager.cs
│   │       ├── StartAcademyResearchCommand.cs
│   │       ├── StartSmithyUpgradeCommand.cs
│   │       ├── FinishAllResearchWithGoldCommand.cs
│   │       ├── ResearchEnrollmentResult.cs
│   │       ├── TechnologyResearchManager.cs
│   │       ├── EnqueueHospitalHealingCommand.cs
│   │       ├── BattleCasualtyAdmissionContext.cs
│   │       ├── BattleCasualtyAdmissionResult.cs
│   │       ├── HospitalHealingManager.cs
│   │       ├── ReleasePrisonersCommand.cs
│   │       ├── DissolvePrisonersCommand.cs
│   │       ├── TrapInterceptionResult.cs
│   │       ├── PrisonerLiberationOutcome.cs
│   │       ├── CombatPrisonerLiberationResult.cs
│   │       ├── TrapperManager.cs
│   │       └── VillageState.cs                          # Extended with training, research, hospital & trapper state
│   ├── Terravia.Actors/
│   │   └── Villages/
│   │       ├── VillageActor.cs                          # Extended with Training & Research timers + command handlers
│   │       ├── AdmitVillageBattleCasualtiesCommand.cs
│   │       ├── TrainingQueueItemDto.cs
│   │       ├── ResearchQueueItemDto.cs
│   │       ├── VillageMilitarySnapshotDto.cs
│   │       ├── VillageMilitaryStateChangedEto.cs
│   │       └── PrisonersReleasedOrLiberatedEto.cs
│   ├── Terravia.Infrastructure/
│   │   └── Villages/
│   │       ├── VillageTrainingQueueItem.cs
│   │       ├── VillageResearchQueueItem.cs
│   │       ├── VillageTechnology.cs
│   │       └── DapperVillageRepository.cs               # Extended with training/research/hospital/prisoner TVP flush
│   └── Terravia.WebAPI/
│       └── Villages/
│           └── VillageMilitaryEndpoints.cs
└── tests/
    ├── Terravia.Domain.Tests/
    │   └── Villages/
    │       ├── TrainingTimeCalculatorTests.cs
    │       ├── CombatUpgradeCalculatorTests.cs
    │       ├── MilitaryTrainingManagerTests.cs
    │       ├── TechnologyResearchManagerTests.cs
    │       ├── HospitalHealingManagerTests.cs
    │       └── TrapperManagerTests.cs
    └── Terravia.Actors.Tests/
        └── Villages/
            └── VillageMilitaryActorTests.cs

client/
└── packages/
    └── game-contracts/
        ├── src/
        │   └── military.ts                              # Shared TS DTOs & real-time training interpolation
        └── tests/
            └── military.test.ts
```

**Structure Decision**: Matches the existing `server/` + `client/` monorepo architecture ratified in the Terravia Renatus Constitution v1.1.0, placing all village-scoped recruitment, research, Hospital, and Trapper domain types in the `Villages/` vertical feature folder with strict `1:1` file-to-type correspondence.

## Complexity Tracking

> No Constitution violations exist. All gates pass without exception.
