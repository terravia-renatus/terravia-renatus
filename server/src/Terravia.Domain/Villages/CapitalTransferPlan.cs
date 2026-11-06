namespace Terravia.Domain.Villages;

/// <summary>
/// Atomic mutation plan produced by <see cref="VillageExpansionManager.BuildCapitalTransferPlan"/> (<c>FR-015</c>).
/// </summary>
public sealed record CapitalTransferPlan(
    bool Succeeded,
    string? ErrorCode,
    int OldCapitalVillageId,
    int NewCapitalVillageId,
    IReadOnlyList<int> OldCapitalDowngradedOuterSlotsToLevel10,
    IReadOnlyList<int> OldCapitalDestroyedBuildingSlots,
    IReadOnlyList<int> NewCapitalDestroyedBuildingSlots);
