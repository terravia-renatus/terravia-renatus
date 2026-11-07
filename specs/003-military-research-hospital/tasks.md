---

description: "Task list for Military Recruitment, Technological Research, Specialty Crafting, and Hospital Casualty Recovery"
---

# Tasks: Military Recruitment, Technological Research, Specialty Crafting, and Hospital Casualty Recovery

**Input**: Design documents from `/specs/003-military-research-hospital/`
**Prerequisites**: [plan.md](./plan.md) (required), [spec.md](./spec.md) (required for user stories), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/domain-services.md](./contracts/domain-services.md), [contracts/military-research-api.md](./contracts/military-research-api.md), [quickstart.md](./quickstart.md)

**Tests**: Per **Constitutional Principle VI (Test-First & Canonical Mathematical Parity - TDD Mandatory)** and `quickstart.md`, golden-vector xUnit tests (`Terravia.Domain.Tests`) and `Akka.TestKit` virtual-time actor tests (`Terravia.Actors.Tests`) are included at the start of each User Story phase and MUST be written to fail before implementing the corresponding domain calculators, managers, and actors.

**Organization**: Tasks are grouped by user story (`US1` through `US5`) in strict adherence to the **One-Type-Per-File (`1:1`)** and **Vertical Feature-Folder** (`Villages/`) rules in the [Terravia Renatus Constitution](../../.specify/memory/constitution.md).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (`[US1]`, `[US2]`, `[US3]`, `[US4]`, `[US5]`)
- Every task includes its exact repository-relative file path and quotes constraints from `data-model.md` verbatim.

## Path Conventions

- **Domain Layer**: `server/src/Terravia.Domain/Villages/`
- **Actor Layer**: `server/src/Terravia.Actors/Villages/`
- **Infrastructure Layer**: `server/src/Terravia.Infrastructure/Villages/`
- **WebAPI Layer**: `server/src/Terravia.WebAPI/Villages/`
- **Backend Tests**: `server/tests/Terravia.Domain.Tests/Villages/` and `server/tests/Terravia.Actors.Tests/Villages/`
- **Shared Client Contracts**: `client/packages/game-contracts/src/`

---

## Phase 1: Setup (Shared Infrastructure & Constants)

**Purpose**: Extend village constants, error codes, and database table/TVP metadata required across all military, research, Hospital, and Trapper stories.

- [X] T001 Extend `VillageConsts` with military training, research, Hospital, and Trapper constants (`GreatMilitaryCostMultiplier = 3`, `ResearchFinishAllGoldCost = 2`, `PlusSmithyLoopBaseDelaySeconds = 60`, `HospitalSaveRate = 0.40`, `BigHospitalSaveRate = 0.50`, `HospitalCapacityPerLevel = 30`, `BigHospitalCapacityPerLevel = 60`, `MaxTrapperLevelCapacity = 400`, `SmallBandageSaveRate = 0.25`, `LargeBandageSaveRate = 0.33`) in `server/src/Terravia.Domain/Villages/VillageConsts.cs`
- [X] T002 [P] Register structured military, research, Hospital, and Trapper error codes (`Terravia:Villages:InvalidUnitForFacility`, `Terravia:Villages:UnitNotResearched`, `Terravia:Villages:TrainingFacilityNotBuilt`, `Terravia:Villages:GreatMilitaryForbiddenInCapital`, `Terravia:Villages:SimultaneousChiefAndSettlerForbidden`, `Terravia:Villages:NegativeCropTrainingRestricted`, `Terravia:Villages:AcademyResearchAlreadyActive`, `Terravia:Villages:UnitAlreadyResearched`, `Terravia:Villages:ResearchPrerequisitesNotMet`, `Terravia:Villages:SmithyQueueFull`, `Terravia:Villages:SmithyLevelCapReached`, `Terravia:Villages:InsufficientWoundedUnits`, `Terravia:Villages:TrapperCapacityExceeded`, `Terravia:Villages:PrisonerStackNotFound`) in `server/src/Terravia.Domain/TerraviaDomainErrorCodes.cs`
- [X] T003 [P] Register table names and TVP type names for `VillageTrainingQueues`, `VillageResearchQueues`, and `VillageTechnologies` in `server/src/Terravia.Infrastructure/TerraviaDbProperties.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core domain enumerations, queue/prisoner state models, and `VillageState` extensions that MUST be complete before user story implementation begins.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T004 [P] Create `TrainingFacilityCategory` enum (`Barracks = 1, GreatBarracks = 2, Stable = 3, GreatStable = 4, Workshop = 5, GreatWorkshop = 6, ResidencePalace = 7, Trapper = 8, Healing = 9`) in `server/src/Terravia.Domain/Villages/TrainingFacilityCategory.cs`
- [X] T005 [P] Create `ResearchCategory` enum (`Academy = 1, Blacksmith = 2, Armoury = 3`) in `server/src/Terravia.Domain/Villages/ResearchCategory.cs`
- [X] T006 [P] Create `HeroRecruitmentHelmetKind` enum (`None = 0, Horseman10 = 10, Cavalry15 = 11, HeavyCavalry20 = 12, Mercenary10 = 13, Warrior15 = 14, Ruler20 = 15`) in `server/src/Terravia.Domain/Villages/HeroRecruitmentHelmetKind.cs`
- [X] T007 [P] Create `HeroBandageTier` enum (`None = 0, SmallBandage25 = 206, LargeBandage33 = 207`) in `server/src/Terravia.Domain/Villages/HeroBandageTier.cs`
- [X] T008 [P] Create `TrainingQueueItem` model with constraints (`QueueItemId` `> 0`, `VillageId` `> 0`, `FacilityCategory` `1..9`, `UnitTypeId` `1..90 or 99`, `AmountRemaining` `> 0`, `PopUpkeepPerUnit` `>= 0 (0 for u99 Traps)`, `EachTimeSeconds` `>= 1`, `CommenceAtMs`, `FinishesAtMs = CommenceAtMs + (long)AmountRemaining * EachTimeSeconds * 1000L`, `NextUnitGraduatesAtMs = CommenceAtMs + EachTimeSeconds * 1000L`) in `server/src/Terravia.Domain/Villages/TrainingQueueItem.cs`
- [X] T009 [P] Create `ResearchQueueItem` model with constraints (`QueueItemId` `> 0`, `VillageId` `> 0`, `Category` `Academy, Blacksmith, or Armoury`, `UnitTypeId` `1..90`, `TribeUnitSlot` `1..9`, `TargetLevel` `1 for Academy; 1..20 for Smithy`, `IsPlusWaitingLoop` `false for Academy and 1st Smithy slot; true for 2nd Smithy slot`, `StartedAtMs`, `FinishesAtMs`) in `server/src/Terravia.Domain/Villages/ResearchQueueItem.cs`
- [X] T010 [P] Create `PrisonerStack` model with constraints (`PrisonerStackId` `> 0`, `CaptorVillageId` `> 0`, `OwnerVillageId` `> 0`, `OwnerPlayerId` `> 0`, `OwnerAllianceId` `>= 0`, `OwnerTribeId` `1..9`, `UnitCountsBySlot` `int[11] indices 1..10`, `HasCapturedHero` `bool`, `TotalOccupiedTraps` `sum(UnitCountsBySlot[1..10]) + (HasCapturedHero ? 1 : 0)`) in `server/src/Terravia.Domain/Villages/PrisonerStack.cs`
- [X] T011 Extend `VillageState` with military, research, Hospital, and Trapper state (`TrainerArtifactMultiplier = 1.0`, `StationedHeroHelmet = HeroRecruitmentHelmetKind.None`, `AllianceRecruitmentBonusLevel` `0..5`, `AllianceJoinedAtMs`, `BlockTrainingOnNegativeCrop = false`, `UnlockedUnitSlots` `bool[11] with slots 1 and 10 true by default`, `OffenseUpgradeLevels` `int[9] 0..20`, `DefenseUpgradeLevels` `int[9] 0..20`, `WoundedUnitsBySlot` `int[9] >= 0`, `HeldPrisoners` `List<PrisonerStack>`, `TrainingQueues` `List<TrainingQueueItem>`, `ResearchQueue` `List<ResearchQueueItem>`) in `server/src/Terravia.Domain/Villages/VillageState.cs`
- [X] T012 [P] Extend `StarvationCandidateStack` and `VillageTroopStack` with dispatch-snapshotted `OffenseUpgradeLevels` (`int[9]`, `0..20`) and `DefenseUpgradeLevels` (`int[9]`, `0..20`) per Clarification Q3 in `server/src/Terravia.Domain/Villages/StarvationCandidateStack.cs` and `server/src/Terravia.Infrastructure/Villages/VillageTroopStack.cs`

**Checkpoint**: Foundation ready — User Story phases can now proceed.

---

## Phase 3: User Story 1 - Military Training Queues, Great Military Buildings & Civic Expansion Training (Priority: P1) 🎯 MVP

**Goal**: Implement concurrent per-facility FIFO training queues (`Barracks`, `GreatBarracks`, `Stable`, `GreatStable`, `Workshop`, `GreatWorkshop`, `ResidencePalace`), Great Military `3x` resource cost deduction, civic expansion slot gating (`Residence` `10/20`, `Palace` & Hun `Command Center` `10/15/20`) with simultaneous Chief+Settler guard, trailing batch merging, non-cancellable queue enforcement, Sitter `BuildAndDemolish` validation, and sequential unit-by-unit graduation (`floor(elapsed / eachtime)`) updating garrison and crop upkeep in real time.

**Independent Test**: Run `MilitaryTrainingManagerTests` to verify concurrent queues across standard and Great Military buildings (`3x` cost), sequential 1-by-1 unit graduation, trailing batch merging, expansion slot limits (`chiefSlots` and `settlerSlots`), simultaneous Chief+Settler rejection, crop stockpile/negative-crop gating, and non-cancellable queue rules (`SC-002`, `SC-003`).

### Tests for User Story 1 (TDD — Write First & Verify Fail) ⚠️

- [X] T013 [P] [US1] Write unit tests for `MilitaryTrainingManager` covering concurrent `Barracks`/`Stable`/`Workshop` queues, `GreatBarracks`/`GreatStable`/`GreatWorkshop` `3x` cost and non-capital enforcement, trailing batch merging (`same UnitTypeId && same EachTimeSeconds`), sequential single-unit graduation (`floor(elapsed / eachtime)`), real-time crop upkeep updates, `Residence`/`Palace`/`Command Center` expansion slot clamping, simultaneous Chief+Settler submission guard, `BlockTrainingOnNegativeCrop` default vs strict mode, and `SitterPermissions.BuildAndDemolish` validation in `server/tests/Terravia.Domain.Tests/Villages/MilitaryTrainingManagerTests.cs`

### Implementation for User Story 1

- [X] T014 [P] [US1] Create `TrainingSpeedContext` readonly record struct `(int UnitTypeId, TrainingFacilityCategory FacilityCategory, int FacilityLevel, int HorseDrinkingTroughLevel = 0, double TrainerArtifactMultiplier = 1.0, bool HasStationedLivingHero = false, HeroRecruitmentHelmetKind StationedHeroHelmet = HeroRecruitmentHelmetKind.None, int AllianceRecruitmentBonusLevel = 0, long AllianceJoinedAtMs = 0, long CurrentTimestampMs = 0, double ServerSpeed = 1.0)` in `server/src/Terravia.Domain/Villages/TrainingSpeedContext.cs`
- [X] T015 [P] [US1] Define `ITrainingTimeCalculator` interface (`CalculateEachTimeSeconds`, `CalculateHospitalHealingEachTimeSeconds`, `CalculateAcademyResearchDurationSeconds`, `CalculateSmithyUpgradeDurationSeconds`, `CalculateEffectiveAllianceBonusLevel`) in `server/src/Terravia.Domain/Villages/ITrainingTimeCalculator.cs`
- [X] T016 [US1] Implement `TrainingTimeCalculator` with the deterministic 4-stage rounding pipeline (`Stage 1: Building Level & Roman Horse Drinking Trough gid 41 -> Stage 2: Single-Pass Trainer Artifact Kind 5 -> Stage 3: Stationed Living Hero Helmet -> Stage 4: Progressive T4 Alliance Recruitment Bonus`, enforcing `max(1, round(..., AwayFromZero))` at every stage) in `server/src/Terravia.Domain/Villages/TrainingTimeCalculator.cs`
- [X] T017 [P] [US1] Create `EnqueueMilitaryTrainingCommand` record and `TrainingEnrollmentResult` readonly record struct in `server/src/Terravia.Domain/Villages/EnqueueMilitaryTrainingCommand.cs` and `server/src/Terravia.Domain/Villages/TrainingEnrollmentResult.cs`
- [X] T018 [US1] Implement `MilitaryTrainingManager` with facility-to-unit eligibility validation (`FR-001`), Great Military `3x` cost & non-capital checks (`FR-002`), trailing batch merging & sequential per-unit graduation `CompleteDueTrainingGraduations` (`FR-003`), civic expansion slot clamping & simultaneous Chief+Settler guard (`FR-004`), granary/negative-crop safety & `SitterPermissions.BuildAndDemolish` checks (`FR-005`) in `server/src/Terravia.Domain/Villages/MilitaryTrainingManager.cs`

**Checkpoint**: User Story 1 is complete and independently testable (`SC-002` and `SC-003` verified).

---

## Phase 4: User Story 2 - Training Accelerators & Stacking Multipliers (Priority: P1)

**Goal**: Verify and enforce 100% golden-vector parity across all 4 stages of the training speed pipeline (`FR-006`) — building level `attri(L)`, Roman `Horse Drinking Trough` (`gid 41`) `-1%`/level cavalry training reduction (`1.01..1.20`), single-pass Trainer Artifact (`Kind 5` `1.0/0.75/0.50`), stationed living Hero Recruitment Helmets (`ItemId 10..15` `-10%/-15%/-20%` on infantry/cavalry only), progressive Alliance Recruitment Bonus (`Levels 1..5` unlocked at `0h, 24h, 48h, 72h, 96h / Speed`) — and Roman `Horse Drinking Trough` cavalry upkeep reduction (`-1` crop/hr for `u4` @ L10, `u5` @ L15, `u6` @ L20, `FR-007`).

**Independent Test**: Run `TrainingTimeCalculatorTests` across all 10 facilities (`gid 19, 20, 21, 25, 26, 29, 30, 36, 44, 49`), building levels `1..20`, `Horse Drinking Trough` levels `0..20`, Trainer Artifacts, Hero Helmets, and Alliance Recruitment Bonus levels `0..5` to prove `100%` integer-second parity (`SC-001`).

### Tests for User Story 2 (TDD — Write First & Verify Fail) ⚠️

- [X] T019 [P] [US2] Write golden-vector and property-based tests for `TrainingTimeCalculator` and `ResourceProductionCalculator` verifying all 4 pipeline stages (`FR-006`), single-pass Trainer Artifact (`Kind 5`), dead/unstationed Hero helmet exclusion, Workshop/Residence/Trapper/Hospital helmet exclusion, progressive Alliance Recruitment Bonus member unlock timers (`(k - 1) * 86400 / Speed`), `1`-second minimum floor on `100x+` speed servers, and Roman `Horse Drinking Trough` (`gid 41`) `-1` crop/hr upkeep reduction at Levels `10/15/20` (`FR-007`) in `server/tests/Terravia.Domain.Tests/Villages/TrainingTimeCalculatorTests.cs`

### Implementation for User Story 2

- [X] T020 [US2] Ensure `MilitaryTrainingManager` and `ResourceProductionCalculator` wire `VillageState` accelerator fields (`TrainerArtifactMultiplier`, `HasStationedLivingHero`, `StationedHeroHelmet`, `AllianceRecruitmentBonusLevel`, `AllianceJoinedAtMs`, and `HorseDrinkingTrough` `gid 41` level) when computing `EachTimeSeconds` and `PopUpkeepPerUnit` in `server/src/Terravia.Domain/Villages/MilitaryTrainingManager.cs`

**Checkpoint**: User Stories 1 AND 2 are complete and independently testable (`SC-001` verified).

---

## Phase 5: User Story 3 - Academy Unit Discovery & Blacksmith / Armoury Combat Upgrades (Priority: P1)

**Goal**: Implement Academy (`gid 22`) unit discovery (`t2..t9` with default `t1` & `t10` unlocked, server-side prerequisite/tribe/single-queue guards, `FR-008`), Blacksmith (`gid 12`, `b1..b8`) and Armoury (`gid 13`, `a1..a8`) combat upgrades (`Levels 1..20`, capped at building level, `1` active + `1` Plus waiting-loop slot with upfront cost deduction and `+ ceil(60 / Speed)`s delay, Snapshot-at-Dispatch rule, `FR-009`), `2` Gold instant research completion (`FinishAllResearchWithGoldCommand`), and `CombatUpgradeCalculator` (`1.007^L` pop-weighted stat scaling and `round(200 * 1.0205^L) / 200` siege demolition factor, `FR-010`).

**Independent Test**: Run `CombatUpgradeCalculatorTests` and `TechnologyResearchManagerTests` to verify Academy prerequisite checks, Blacksmith/Armoury concurrent queues, Plus 2nd upgrade slot timing & immediate cost deduction, `2` Gold instant research completion, Snapshot-at-Dispatch upgrade propagation, and `0.0001` combat stat accuracy across levels `0..20` (`SC-004`).

### Tests for User Story 3 (TDD — Write First & Verify Fail) ⚠️

- [X] T021 [P] [US3] Write golden-vector unit tests for `CombatUpgradeCalculator` validating upgraded `atk`, `di`, `dc` stats (`round(S0 + (S0 + 300 * pop / 7) * (1.007^L - 1), 4)`), reconnaissance scout base stats (`35` atk / `20` def), and Ram/Catapult siege demolition multipliers (`round(200 * 1.0205^L) / 200`) across levels `0..20` for all tribes in `server/tests/Terravia.Domain.Tests/Villages/CombatUpgradeCalculatorTests.cs`
- [X] T022 [P] [US3] Write unit tests for `TechnologyResearchManager` covering default unlocked slots (`X1` and `X10`), Academy (`gid 22`) building prerequisite and single-project guards, Blacksmith (`gid 12`) & Armoury (`gid 13`) concurrent queues, `CurrentLevel + QueuedUpgrades <= BuildingLevel` cap, Plus 2nd upgrade slot immediate cost deduction and `+ ceil(60 / Speed)`s delay, `2` Gold instant research completion (`SitterPermissions.BuyAndSpendGold`), and active research surviving building demolition in `server/tests/Terravia.Domain.Tests/Villages/TechnologyResearchManagerTests.cs`

### Implementation for User Story 3

- [X] T023 [P] [US3] Create `UpgradedUnitCombatStats` readonly record struct and `ICombatUpgradeCalculator` interface in `server/src/Terravia.Domain/Villages/UpgradedUnitCombatStats.cs` and `server/src/Terravia.Domain/Villages/ICombatUpgradeCalculator.cs`
- [X] T024 [US3] Implement `CombatUpgradeCalculator` evaluating `CalculateUpgradedStat`, `CalculateSiegeDemolitionMultiplier`, and `EvaluateUnitCombatStats` (`FR-010`) in `server/src/Terravia.Domain/Villages/CombatUpgradeCalculator.cs`
- [X] T025 [P] [US3] Create `StartAcademyResearchCommand`, `StartSmithyUpgradeCommand`, `FinishAllResearchWithGoldCommand`, and `ResearchEnrollmentResult` records in `server/src/Terravia.Domain/Villages/StartAcademyResearchCommand.cs`, `server/src/Terravia.Domain/Villages/StartSmithyUpgradeCommand.cs`, `server/src/Terravia.Domain/Villages/FinishAllResearchWithGoldCommand.cs`, and `server/src/Terravia.Domain/Villages/ResearchEnrollmentResult.cs`
- [X] T026 [US3] Implement `TechnologyResearchManager` with `StartAcademyResearch`, `StartSmithyUpgrade` (including Plus waiting-loop upfront cost deduction and `+ ceil(60 / Speed)`s delay), `FinishAllResearchWithGold` (`2` Gold cost, `SitterPermissions.BuyAndSpendGold`), and `CompleteDueResearchItems` (updating `VillageState.UnlockedUnitSlots`, `OffenseUpgradeLevels`, `DefenseUpgradeLevels`, and `Home` stack upgrade snapshots) in `server/src/Terravia.Domain/Villages/TechnologyResearchManager.cs`

**Checkpoint**: User Stories 1, 2, AND 3 are complete and independently testable (`SC-004` verified).

---

## Phase 6: User Story 4 - Hospital & Big Hospital Casualty Interception & Healing Queue (Priority: P2)

**Goal**: Implement Hospital (`gid 46`, non-Spartan/Viking, `40%` save rate, `30 * Level` capacity) and Spartan/Viking Big Hospital (`gid 48`, `50%` save rate, `60 * Level` capacity) casualty interception (`FR-011`), T4 Hero Bandage (`ItemId 206` `25%`, `ItemId 207` `33%`) pre-interception sequencing, sequential slot `1..8` capacity clamping, soft-cap retention when the Hospital is damaged/demolished (Clarification Q2), conquest/razing cleanup (`ClearHospitalOnConquestOrRaze`), `0` crop upkeep while wounded, and the sequential single-lane `50%`-cost Healing Queue (`FR-012`).

**Independent Test**: Run `HospitalHealingManagerTests` to verify `40%` vs `50%` casualty interception, Hero Bandage (`25%`/`33%`) pre-deduction, slot `1..8` overflow clamping, non-combat/reinforcement exclusion, soft-cap retention on Hospital damage, conquest/razing cleanup, and sequential `50%`-cost healing graduation (`SC-005`).

### Tests for User Story 4 (TDD — Write First & Verify Fail) ⚠️

- [X] T027 [P] [US4] Write unit tests for `HospitalHealingManager` covering Hospital (`gid 46`, `40%`, `30*L`) vs Spartan/Viking Big Hospital (`gid 48`, `50%`, `60*L`), T4 Hero Bandages (`25%` / `33%`) applied before Hospital interception, ascending slot `1..8` capacity clamping, exclusion of Chiefs/Settlers/Traps/Heroes/foreign reinforcements, soft-cap retention of existing `w1..w8` when Hospital level drops, conquest/razing `ClearHospitalOnConquestOrRaze`, and sequential single-lane `50%`-cost healing batches in `server/tests/Terravia.Domain.Tests/Villages/HospitalHealingManagerTests.cs`

### Implementation for User Story 4

- [X] T028 [P] [US4] Create `EnqueueHospitalHealingCommand`, `BattleCasualtyAdmissionContext`, and `BattleCasualtyAdmissionResult` records in `server/src/Terravia.Domain/Villages/EnqueueHospitalHealingCommand.cs`, `server/src/Terravia.Domain/Villages/BattleCasualtyAdmissionContext.cs`, and `server/src/Terravia.Domain/Villages/BattleCasualtyAdmissionResult.cs`
- [X] T029 [US4] Implement `HospitalHealingManager` with `GetMaxHospitalCapacity`, `GetCasualtyInterceptionRate`, `AdmitBattleCasualties` (Bandage revival first, then slot `1..8` clamping), `EnqueueHealingBatch` (`ceil(cost * amt / 2)`, sequential tail-chaining in `TrainingFacilityCategory.Healing`, `max(1, round(BaseTime * attri_Hospital(L) / Speed))`), and `ClearHospitalOnConquestOrRaze` in `server/src/Terravia.Domain/Villages/HospitalHealingManager.cs`
- [X] T030 [US4] Update `SiegeAndConquestCalculator.ApplyVillageConquestStateReset` to clear `WoundedUnitsBySlot`, `TrainingQueues`, and `ResearchQueue` when a village is conquered or razed in `server/src/Terravia.Domain/Villages/SiegeAndConquestCalculator.cs`

**Checkpoint**: User Stories 1 through 4 are complete and independently testable.

---

## Phase 7: User Story 5 - Gaul Trapper, Prisoner Capture, Combat Liberation & Auto-Repair (Priority: P2)

**Goal**: Implement Gaul Trapper (`gid 36`) multi-building construction rules (additional Trappers unlocked once an existing Trapper reaches Level `20`, `400` traps per Level `20`), Trap (`u99`) crafting in `MilitaryTrainingManager` (`FR-013`), pre-combat hostile army interception (Reconnaissance `AttackType = 1` bypass, Archon Capital `100%` auto-trap, Hero `t11` escape when `freeTraps < totalTroops`, sequential `1..10` remainder clamping with `trapped[i] < count[i]`, `FR-014`), and all 3 prisoner release paths (Combat Liberation with `25%` prisoner mortality, same-village returning army merge, and `round(totalFreed / 3)` free trap repair; Trap Owner Manual Release with `100%` survival and `100%` free trap repair; Troop Owner Dissolution with `100%` death and `round(totalFreed / 3)` free trap repair, `FR-015`).

**Independent Test**: Run `TrapperManagerTests` to verify multi-Trapper capacity aggregation, Reconnaissance bypass, Archon Capital `100%` auto-trap, Hero escape on partial capture, remainder clamping, and all 3 release/liberation/dissolution paths with free trap repair clamping (`SC-005`).

### Tests for User Story 5 (TDD — Write First & Verify Fail) ⚠️

- [X] T031 [P] [US5] Write unit tests for `TrapperManager` covering multiple Trapper (`gid 36`) capacity aggregation (`10..400` per building), Reconnaissance (`AttackType = 1`) bypass vs Normal/Raid interception, Archon Capital (`IsArchonVillage && IsCapital`) `100%` auto-trap, Hero (`t11`) capture when `freeTraps >= totalTroops` vs escape when `freeTraps < totalTroops`, remainder clamping (`trapped[i] < count[i]`), Combat Liberation (`25%` mortality `round(count / 4)`, same-village returning army merge vs foreign return, `round(totalFreed / 3)` free trap repair), Defender Manual Release (`100%` survival, `100%` free trap repair), Attacker Manual Dissolution (`100%` death including Hero, `round(totalFreed / 3)` free trap repair), and `SitterPermissions.Reinforce` validation in `server/tests/Terravia.Domain.Tests/Villages/TrapperManagerTests.cs`

### Implementation for User Story 5

- [X] T032 [P] [US5] Create `ReleasePrisonersCommand`, `DissolvePrisonersCommand`, `TrapInterceptionResult`, `PrisonerLiberationOutcome`, and `CombatPrisonerLiberationResult` records in `server/src/Terravia.Domain/Villages/ReleasePrisonersCommand.cs`, `server/src/Terravia.Domain/Villages/DissolvePrisonersCommand.cs`, `server/src/Terravia.Domain/Villages/TrapInterceptionResult.cs`, `server/src/Terravia.Domain/Villages/PrisonerLiberationOutcome.cs`, and `server/src/Terravia.Domain/Villages/CombatPrisonerLiberationResult.cs`
- [X] T033 [US5] Implement `TrapperManager` with `CalculateTotalTrapCapacity`, `GetQueuedTrapsCount`, `InterceptIncomingArmy`, `LiberatePrisonersOnSuccessfulNormalAttack`, `ReleasePrisonersByTrapOwner`, and `DissolvePrisonersByTroopOwner` (enqueuing `0`-cost free repair batches into `TrainingFacilityCategory.Trapper` clamped to `TotalTrapCapacity`) in `server/src/Terravia.Domain/Villages/TrapperManager.cs`

**Checkpoint**: All 5 User Stories (`US1`..`US5`) domain managers and calculators are complete and independently verified (`SC-001`..`SC-005`).

---

## Phase 8: Actor Orchestration, Persistence, WebAPI & Shared Contracts (Cross-Cutting Integration)

**Purpose**: Wire all military, research, Hospital, and Trapper domain managers into `VillageActor` with virtual-time `ITimerScheduler` tests, extend `DapperVillageRepository` persistence models, expose `VillageMilitaryEndpoints`, and add TypeScript contracts in `client/packages/game-contracts`.

- [X] T034 [P] Write `Akka.TestKit` virtual-time actor integration tests in `VillageMilitaryActorTests` covering concurrent training graduation ticks, Academy/Smithy research completion timers, `2` Gold instant research completion, Hospital casualty admission & healing timers, and Trapper prisoner release/dissolution in `server/tests/Terravia.Actors.Tests/Villages/VillageMilitaryActorTests.cs`
- [X] T035 [P] Create `AdmitVillageBattleCasualtiesCommand`, `TrainingQueueItemDto`, `ResearchQueueItemDto`, `VillageMilitarySnapshotDto`, `VillageMilitaryStateChangedEto`, and `PrisonersReleasedOrLiberatedEto` records in `server/src/Terravia.Actors/Villages/AdmitVillageBattleCasualtiesCommand.cs`, `server/src/Terravia.Actors/Villages/TrainingQueueItemDto.cs`, `server/src/Terravia.Actors/Villages/ResearchQueueItemDto.cs`, `server/src/Terravia.Actors/Villages/VillageMilitarySnapshotDto.cs`, `server/src/Terravia.Actors/Villages/VillageMilitaryStateChangedEto.cs`, and `server/src/Terravia.Actors/Villages/PrisonersReleasedOrLiberatedEto.cs`
- [X] T036 Integrate `MilitaryTrainingManager`, `TechnologyResearchManager`, `HospitalHealingManager`, and `TrapperManager` into `VillageActor` with exact `MilitaryTrainingTimerKey` (scheduled at next single-unit graduation `min(NextUnitGraduatesAtMs)`) and `ResearchTimerKey` (scheduled at `min(FinishesAtMs)`) in `server/src/Terravia.Actors/Villages/VillageActor.cs`
- [X] T037 [P] Create `VillageTrainingQueueItem`, `VillageResearchQueueItem`, and `VillageTechnology` persistence models in `server/src/Terravia.Infrastructure/Villages/VillageTrainingQueueItem.cs`, `server/src/Terravia.Infrastructure/Villages/VillageResearchQueueItem.cs`, and `server/src/Terravia.Infrastructure/Villages/VillageTechnology.cs`
- [X] T038 Extend `DapperVillageRepository` to hydrate and flush `VillageTrainingQueueItem`, `VillageResearchQueueItem`, and `VillageTechnology` records alongside `VillageState` in `server/src/Terravia.Infrastructure/Villages/DapperVillageRepository.cs`
- [X] T039 [P] Implement `VillageMilitaryEndpoints` (`POST /api/villages/{villageId:int}/training`, `/hospital/heal`, `/academy/research`, `/smithy/upgrade`, `/research/finish-all-gold`, `/trapper/prisoners/{stackId:long}/release`, `/trapper/prisoners/{stackId:long}/dissolve`) with `SitterPermissions` gates in `server/src/Terravia.WebAPI/Villages/VillageMilitaryEndpoints.cs` and register in `server/src/Terravia.WebAPI/Program.cs`
- [X] T040 [P] Implement shared TypeScript DTOs (`TrainingFacilityCategory`, `ResearchCategory`, `TrainingQueueItemDto`, `ResearchQueueItemDto`, `VillageMilitarySnapshotDto`) and real-time per-unit graduation interpolation helper `interpolateTrainingQueues` with Vitest unit tests in `client/packages/game-contracts/src/military.ts`, `client/packages/game-contracts/src/index.ts`, and `client/packages/game-contracts/tests/military.test.ts`
- [X] T041 Execute full validation suite from `quickstart.md` (`dotnet test` across `Terravia.Domain.Tests` and `Terravia.Actors.Tests` + `pnpm test` in `client/packages/game-contracts`) to verify zero warnings/errors and 100% pass rate

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — starts immediately.
- **Phase 2 (Foundational)**: Depends on Phase 1 — **BLOCKS** all user story phases.
- **Phase 3 (`US1` - Military Training Queues & Civic Expansion)**: Depends on Phase 2.
- **Phase 4 (`US2` - Training Accelerators & Stacking Multipliers)**: Depends on `T016`/`T018` in Phase 3 (`US1`).
- **Phase 5 (`US3` - Academy Discovery & Blacksmith/Armoury Upgrades)**: Depends on Phase 2 and `T016` (`TrainingTimeCalculator`); can run in parallel with `US4` and `US5`.
- **Phase 6 (`US4` - Hospital & Big Hospital Casualty Recovery)**: Depends on Phase 2 and `T016`/`T018`; can run in parallel with `US3` and `US5`.
- **Phase 7 (`US5` - Gaul Trapper & Prisoner Lifecycle)**: Depends on Phase 2 and `T016`/`T018`; can run in parallel with `US3` and `US4`.
- **Phase 8 (Actor, Persistence, WebAPI & Client Integration)**: Depends on Phases 3–7.

### Parallel Opportunities

- **Phase 1 & 2**: `T002`–`T010` and `T012` touch distinct files (`1:1` file-to-type rule) and can be executed in parallel.
- **Domain Tests (`T013`, `T019`, `T021`, `T022`, `T027`, `T031`)**: Each user story's test suite resides in its own dedicated test file under `server/tests/Terravia.Domain.Tests/Villages/`.
- **Stories `US3`, `US4`, and `US5`**: Once `TrainingTimeCalculator` (`T016`) and `MilitaryTrainingManager` (`T018`) are in place, `TechnologyResearchManager` (`US3`), `HospitalHealingManager` (`US4`), and `TrapperManager` (`US5`) have zero mutual dependencies and can be developed in parallel.
- **Phase 8**: `T035` (Actor DTOs/ETOs), `T037` (Infrastructure models), `T039` (WebAPI endpoints), and `T040` (TypeScript contracts) can be developed in parallel.

---

## Parallel Example: User Stories 3, 4 & 5

```bash
# Run TDD test authoring across US3, US4, and US5 in parallel:
Task T021: "Write golden-vector unit tests for CombatUpgradeCalculator in server/tests/Terravia.Domain.Tests/Villages/CombatUpgradeCalculatorTests.cs"
Task T022: "Write unit tests for TechnologyResearchManager in server/tests/Terravia.Domain.Tests/Villages/TechnologyResearchManagerTests.cs"
Task T027: "Write unit tests for HospitalHealingManager in server/tests/Terravia.Domain.Tests/Villages/HospitalHealingManagerTests.cs"
Task T031: "Write unit tests for TrapperManager in server/tests/Terravia.Domain.Tests/Villages/TrapperManagerTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 + User Story 2)

1. Complete **Phase 1: Setup** (`T001`–`T003`) and **Phase 2: Foundational** (`T004`–`T012`).
2. Complete **Phase 3: User Story 1** (`T013`–`T018`) and **Phase 4: User Story 2** (`T019`–`T020`).
3. **STOP and VALIDATE**: Run `MilitaryTrainingManagerTests` and `TrainingTimeCalculatorTests` (`SC-001`, `SC-002`, `SC-003`).

### Incremental Delivery

1. Add **Phase 5: User Story 3** (`Academy` & `Smithy` upgrades, `T021`–`T026`) → Verify `SC-004`.
2. Add **Phase 6: User Story 4** (`Hospital` & `Big Hospital`, `T027`–`T030`) → Verify casualty recovery (`SC-005`).
3. Add **Phase 7: User Story 5** (`Gaul Trapper` & `Prisoners`, `T031`–`T033`) → Verify prisoner capture/liberation (`SC-005`).
4. Complete **Phase 8: Integration** (`T034`–`T041`) → Full end-to-end actor, persistence, API, and client contract validation.
