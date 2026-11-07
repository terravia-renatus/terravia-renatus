# Phase 1 Contract: Pure C# Domain Services, Calculators & Managers (`Terravia.Domain.Villages`)

**Feature Branch**: `003-military-research-hospital`  
**Date**: 2026-11-07  
**Layer**: `server/src/Terravia.Domain/Villages/` (Zero external framework dependencies)

---

## 1. `ITrainingTimeCalculator` & `TrainingTimeCalculator`

Stateless, zero-allocation calculator implementing the 4-stage training speed pipeline (`FR-006`), Hospital healing duration (`FR-012`), and Academy/Smithy research durations (`FR-008`..`FR-009`).

```csharp
namespace Terravia.Domain.Villages;

public readonly record struct TrainingSpeedContext(
    int UnitTypeId,
    TrainingFacilityCategory FacilityCategory,
    int FacilityLevel,
    int HorseDrinkingTroughLevel = 0,
    double TrainerArtifactMultiplier = 1.0,
    bool HasStationedLivingHero = false,
    HeroRecruitmentHelmetKind StationedHeroHelmet = HeroRecruitmentHelmetKind.None,
    int AllianceRecruitmentBonusLevel = 0,
    long AllianceJoinedAtMs = 0,
    long CurrentTimestampMs = 0,
    double ServerSpeed = 1.0);

public interface ITrainingTimeCalculator
{
    int CalculateEachTimeSeconds(in TrainingSpeedContext context);
    int CalculateHospitalHealingEachTimeSeconds(int unitTypeId, int hospitalBuildingTypeId, int hospitalLevel, double serverSpeed = 1.0);
    int CalculateAcademyResearchDurationSeconds(int unitTypeId, int academyLevel, double serverSpeed = 1.0);
    int CalculateSmithyUpgradeDurationSeconds(int unitTypeId, int targetLevel, int smithyLevel, double serverSpeed = 1.0);
    int CalculateEffectiveAllianceBonusLevel(int allianceBonusLevel, long allianceJoinedAtMs, long currentTimestampMs, double serverSpeed = 1.0);
}
```

---

## 2. `ICombatUpgradeCalculator` & `CombatUpgradeCalculator`

Stateless calculator evaluating Blacksmith (`b1..b8`), Armoury (`a1..a8`), and Ram/Catapult structural upgrade multipliers (`FR-010`, `SC-004`).

```csharp
namespace Terravia.Domain.Villages;

public readonly record struct UpgradedUnitCombatStats(
    double EffectiveOffense,
    double EffectiveDefenseInfantry,
    double EffectiveDefenseCavalry,
    double SiegeDemolitionMultiplier);

public interface ICombatUpgradeCalculator
{
    double CalculateUpgradedStat(int baseStat, int cropUpkeep, int upgradeLevel);
    double CalculateSiegeDemolitionMultiplier(int upgradeLevel);
    UpgradedUnitCombatStats EvaluateUnitCombatStats(
        int unitTypeId,
        int offenseUpgradeLevel,
        int defenseUpgradeLevel,
        bool isReconnaissanceMission = false);
}
```

---

## 3. `MilitaryTrainingManager`

Orchestrates military unit recruitment, Great Military `3x` cost queues, civic expansion unit slot validation, batch merging, and sequential unit-by-unit graduation (`FR-001`..`FR-005`).

```csharp
namespace Terravia.Domain.Villages;

public sealed record EnqueueMilitaryTrainingCommand(
    int VillageId,
    TrainingFacilityCategory FacilityCategory,
    int UnitTypeId,
    int RequestedAmount,
    int? SecondExpansionUnitTypeId = null,
    int SecondExpansionRequestedAmount = 0);

public readonly record struct TrainingEnrollmentResult(
    bool Succeeded,
    string? ErrorCode,
    int EnrolledAmount,
    int EachTimeSeconds,
    ResourceCost DeductedCost,
    long FinishesAtMs,
    bool WasMergedIntoTrailingBatch);

public sealed class MilitaryTrainingManager
{
    public TrainingEnrollmentResult EnqueueTrainingBatch(
        VillageState state,
        EnqueueMilitaryTrainingCommand command,
        long nowMs,
        SitterPermissions? sitterPermissions = null);

    public int CompleteDueTrainingGraduations(VillageState state, long nowMs);
}
```

---

## 4. `TechnologyResearchManager`

Orchestrates Academy (`gid 22`) unit discovery (`t2..t9`), Blacksmith (`gid 12`) offense upgrades (`b1..b8`), Armoury (`gid 13`) defense upgrades (`a1..a8`), Plus 2nd Smithy waiting-loop slot (`+ ceil(60 / Speed)`s), and `2` Gold instant research completion (`FR-008`..`FR-009`).

```csharp
namespace Terravia.Domain.Villages;

public sealed record StartAcademyResearchCommand(
    int VillageId,
    int UnitTypeId);

public sealed record StartSmithyUpgradeCommand(
    int VillageId,
    ResearchCategory Category,
    int UnitTypeId);

public sealed record FinishAllResearchWithGoldCommand(
    int VillageId);

public readonly record struct ResearchEnrollmentResult(
    bool Succeeded,
    string? ErrorCode,
    ResearchCategory Category,
    int UnitTypeId,
    int TargetLevel,
    bool IsPlusWaitingLoop,
    ResourceCost DeductedCost,
    long FinishesAtMs);

public sealed class TechnologyResearchManager
{
    public ResearchEnrollmentResult StartAcademyResearch(
        VillageState state,
        StartAcademyResearchCommand command,
        long nowMs,
        SitterPermissions? sitterPermissions = null);

    public ResearchEnrollmentResult StartSmithyUpgrade(
        VillageState state,
        StartSmithyUpgradeCommand command,
        long nowMs,
        SitterPermissions? sitterPermissions = null);

    public (CommandResult Result, int GoldDeducted, int CompletedCount) FinishAllResearchWithGold(
        VillageState state,
        FinishAllResearchWithGoldCommand command,
        long nowMs,
        SitterPermissions? sitterPermissions = null);

    public int CompleteDueResearchItems(VillageState state, long nowMs);
}
```

---

## 5. `HospitalHealingManager`

Orchestrates Hospital (`gid 46`, `40%`, `30*L`) and Spartan/Viking Big Hospital (`gid 48`, `50%`, `60*L`) post-battle casualty interception (with T4 Hero Bandage `id 206/207` precedence), soft-cap retention on building damage, conquest/razing cleanup, and sequential `50%`-cost healing queues (`FR-011`..`FR-012`).

```csharp
namespace Terravia.Domain.Villages;

public sealed record EnqueueHospitalHealingCommand(
    int VillageId,
    int UnitTypeId,
    int RequestedAmount);

public readonly record struct BattleCasualtyAdmissionContext(
    ReadOnlyMemory<int> DeadUnitsBySlot,
    bool HasSurvivingHeroWithBandages = false,
    HeroBandageTier BandageTier = HeroBandageTier.None,
    int AvailableBandageItemCount = 0);

public readonly record struct BattleCasualtyAdmissionResult(
    int[] BandagedUnitsBySlot,
    int TotalBandagesConsumed,
    int[] AdmittedWoundedBySlot,
    int TotalAdmittedToHospital,
    int RemainingFreeHospitalCapacity);

public sealed class HospitalHealingManager
{
    public int GetMaxHospitalCapacity(VillageState state);
    public double GetCasualtyInterceptionRate(VillageState state);

    public BattleCasualtyAdmissionResult AdmitBattleCasualties(
        VillageState state,
        in BattleCasualtyAdmissionContext context);

    public TrainingEnrollmentResult EnqueueHealingBatch(
        VillageState state,
        EnqueueHospitalHealingCommand command,
        long nowMs,
        SitterPermissions? sitterPermissions = null);

    public void ClearHospitalOnConquestOrRaze(VillageState state);
}
```

---

## 6. `TrapperManager`

Orchestrates Gaul Trapper (`gid 36`) capacity calculation across multiple Trappers, pre-combat hostile army interception (including Archon Capital `100%` auto-trap and Hero `t11` escape rule), and the three prisoner liberation/release/dissolution paths with automatic `0`-cost trap repairs (`FR-013`..`FR-015`).

```csharp
namespace Terravia.Domain.Villages;

public sealed record ReleasePrisonersCommand(
    int CaptorVillageId,
    long PrisonerStackId);

public sealed record DissolvePrisonersCommand(
    int CaptorVillageId,
    int OwnerPlayerId,
    long PrisonerStackId);

public readonly record struct TrapInterceptionResult(
    int[] RemainingAttackingUnitsBySlot,
    bool AttackingHeroRemains,
    int[] CapturedUnitsBySlot,
    bool HeroWasCaptured,
    int TotalCapturedUnits,
    PrisonerStack? CreatedOrUpdatedPrisonerStack);

public readonly record struct PrisonerLiberationOutcome(
    long PrisonerStackId,
    int OwnerVillageId,
    int[] SurvivingUnitsBySlot,
    bool HeroSurvived,
    bool MergedIntoAttackingArmyReturn,
    int FreedTrapCount);

public readonly record struct CombatPrisonerLiberationResult(
    IReadOnlyList<PrisonerLiberationOutcome> LiberatedStacks,
    int[] MergedIntoAttackerReturnBySlot,
    bool MergedHeroIntoAttackerReturn,
    int TotalDestroyedTraps,
    int FreeAutoRepairedTrapsQueued);

public sealed class TrapperManager
{
    public int CalculateTotalTrapCapacity(ReadOnlySpan<int> slotBuildingTypes, ReadOnlySpan<int> slotLevels);
    public int GetQueuedTrapsCount(VillageState state);

    public TrapInterceptionResult InterceptIncomingArmy(
        VillageState defenderState,
        int attackType,
        int attackerVillageId,
        int attackerPlayerId,
        int attackerAllianceId,
        int attackerTribeId,
        ReadOnlySpan<int> incomingUnitsBySlot,
        bool incomingHasHero,
        long nextPrisonerStackId);

    public CombatPrisonerLiberationResult LiberatePrisonersOnSuccessfulNormalAttack(
        VillageState defenderState,
        int attackerVillageId,
        int attackerPlayerId,
        int attackerAllianceId,
        IReadOnlySet<int>? alliedOrNapAllianceIds,
        long nowMs);

    public (CommandResult Result, PrisonerLiberationOutcome? Outcome, int FreeRepairsQueued) ReleasePrisonersByTrapOwner(
        VillageState defenderState,
        ReleasePrisonersCommand command,
        long nowMs,
        SitterPermissions? sitterPermissions = null);

    public (CommandResult Result, int DissolvedCount, int FreeRepairsQueued) DissolvePrisonersByTroopOwner(
        VillageState defenderState,
        DissolvePrisonersCommand command,
        long nowMs,
        SitterPermissions? sitterPermissions = null);
}
```
