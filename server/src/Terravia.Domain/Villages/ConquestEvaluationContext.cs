namespace Terravia.Domain.Villages;

/// <summary>
/// Input context for evaluating administrator persuasion and village conquest state resets (<c>FR-017</c>).
/// </summary>
public readonly record struct ConquestEvaluationContext(
    int AttackerPlayerId,
    int AttackerTribeId,
    int AttackerTotalAccountPopulation,
    int AttackerLaunchingVillageFreeExpansionSlots,
    double AttackerAccumulatedCulturePoints,
    double RequiredCulturePointsForNextVillage,
    bool AttackerVillageHasActiveGreatCelebration,
    bool AttackerHasActiveTeutonMeadFestival,
    int SurvivingAdministratorUnitTypeId,
    int SurvivingAdministratorCount,
    int DefenderPlayerId,
    int DefenderTribeId,
    int DefenderTotalAccountPopulation,
    int DefenderOwnedVillageCount,
    bool TargetIsCapital,
    bool TargetIsArchonVillage,
    bool TargetIsWwBuildingPlanVillage,
    bool TargetHasStandingExpansionBuilding,
    bool DefenderVillageHasActiveGreatCelebration,
    double CurrentTargetLoyalty,
    double MoraleBonus = 1.0);
