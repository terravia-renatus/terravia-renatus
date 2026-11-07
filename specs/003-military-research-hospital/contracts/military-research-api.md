# Phase 1 Contract: Actor Messages, WebAPI Endpoints & Client TypeScript Contracts

**Feature Branch**: `003-military-research-hospital`  
**Date**: 2026-11-07  
**Layers**: `Terravia.Actors.Villages`, `Terravia.WebAPI.Villages`, `client/packages/game-contracts`

---

## 1. Akka.NET Actor Messages & ETOs (`Terravia.Actors.Villages`)

### 1.1 Commands Handled by `VillageActor`
- `VillageActorCommandEnvelope<EnqueueMilitaryTrainingCommand>` → Returns `TrainingEnrollmentResult`
- `VillageActorCommandEnvelope<EnqueueHospitalHealingCommand>` → Returns `TrainingEnrollmentResult`
- `VillageActorCommandEnvelope<StartAcademyResearchCommand>` → Returns `ResearchEnrollmentResult`
- `VillageActorCommandEnvelope<StartSmithyUpgradeCommand>` → Returns `ResearchEnrollmentResult`
- `VillageActorCommandEnvelope<FinishAllResearchWithGoldCommand>` → Returns `CommandResult`
- `VillageActorCommandEnvelope<ReleasePrisonersCommand>` → Returns `CommandResult`
- `VillageActorCommandEnvelope<DissolvePrisonersCommand>` → Returns `CommandResult`
- `AdmitVillageBattleCasualtiesCommand(BattleCasualtyAdmissionContext Context, long NowMs)` → Returns `BattleCasualtyAdmissionResult`

### 1.2 Outbound Event Transfer Objects (`*Eto`)
- `VillageMilitaryStateChangedEto(int VillageId, long TimestampMs, VillageMilitarySnapshotDto Snapshot)`
- `PrisonersReleasedOrLiberatedEto(int CaptorVillageId, IReadOnlyList<PrisonerLiberationOutcome> Outcomes, int FreeRepairsQueued, long TimestampMs)`

---

## 2. Minimal API & SignalR Endpoints (`Terravia.WebAPI.Villages.VillageMilitaryEndpoints`)

| HTTP Method & Route | SignalR Hub Method | Sitter Permission Gate | Command Dispatched |
| :--- | :--- | :--- | :--- |
| `POST /api/villages/{villageId:int}/training` | `EnqueueMilitaryTraining` | `SitterPermissions.BuildAndDemolish` | `EnqueueMilitaryTrainingCommand` |
| `POST /api/villages/{villageId:int}/hospital/heal` | `EnqueueHospitalHealing` | `SitterPermissions.BuildAndDemolish` | `EnqueueHospitalHealingCommand` |
| `POST /api/villages/{villageId:int}/academy/research` | `StartAcademyResearch` | `SitterPermissions.BuildAndDemolish` | `StartAcademyResearchCommand` |
| `POST /api/villages/{villageId:int}/smithy/upgrade` | `StartSmithyUpgrade` | `SitterPermissions.BuildAndDemolish` | `StartSmithyUpgradeCommand` |
| `POST /api/villages/{villageId:int}/research/finish-all-gold` | `FinishAllResearchWithGold` | `SitterPermissions.BuyAndSpendGold` | `FinishAllResearchWithGoldCommand` |
| `POST /api/villages/{villageId:int}/trapper/prisoners/{stackId:long}/release` | `ReleaseTrappedPrisoners` | `SitterPermissions.Reinforce` | `ReleasePrisonersCommand` |
| `POST /api/villages/{villageId:int}/trapper/prisoners/{stackId:long}/dissolve` | `DissolveTrappedPrisoners` | `SitterPermissions.Reinforce` | `DissolvePrisonersCommand` |

---

## 3. Shared TypeScript Contracts (`client/packages/game-contracts`)

```typescript
export enum TrainingFacilityCategory {
  Barracks = 1,
  GreatBarracks = 2,
  Stable = 3,
  GreatStable = 4,
  Workshop = 5,
  GreatWorkshop = 6,
  ResidencePalace = 7,
  Trapper = 8,
  Healing = 9,
}

export enum ResearchCategory {
  Academy = 1,
  Blacksmith = 2,
  Armoury = 3,
}

export interface TrainingQueueItemDto {
  readonly queueItemId: number;
  readonly facilityCategory: TrainingFacilityCategory;
  readonly unitTypeId: number;
  readonly amountRemaining: number;
  readonly eachTimeSeconds: number;
  readonly commenceAtMs: number;
  readonly finishesAtMs: number;
  readonly nextUnitGraduatesAtMs: number;
}

export interface ResearchQueueItemDto {
  readonly queueItemId: number;
  readonly category: ResearchCategory;
  readonly unitTypeId: number;
  readonly tribeUnitSlot: number;
  readonly targetLevel: number;
  readonly isPlusWaitingLoop: boolean;
  readonly startedAtMs: number;
  readonly finishesAtMs: number;
}

export interface VillageMilitarySnapshotDto {
  readonly villageId: number;
  readonly unlockedUnitSlots: readonly boolean[];
  readonly offenseUpgradeLevels: readonly number[];
  readonly defenseUpgradeLevels: readonly number[];
  readonly woundedUnitsBySlot: readonly number[];
  readonly maxHospitalCapacity: number;
  readonly totalTrapsCount: number;
  readonly occupiedTrapsCount: number;
  readonly maxTrapCapacity: number;
  readonly trainingQueues: readonly TrainingQueueItemDto[];
  readonly researchQueue: readonly ResearchQueueItemDto[];
}
```
