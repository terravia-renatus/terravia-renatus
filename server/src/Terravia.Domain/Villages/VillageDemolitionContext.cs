namespace Terravia.Domain.Villages;

/// <summary>
/// Snapshot of village and alliance state used to evaluate building demolition eligibility.
/// </summary>
public readonly record struct VillageDemolitionContext(
    int MainBuildingLevel,
    int TargetSlotBuildingTypeId,
    int TargetSlotCurrentLevel,
    bool SlotHasQueuedUpgrade,
    bool VillageHasActiveDemolition,
    bool IsAllianceOwner,
    int AllianceMemberCount,
    int QualifyingEmbassyCountAcrossPlayerVillages,
    int PlayerGoldBalance = 0);
