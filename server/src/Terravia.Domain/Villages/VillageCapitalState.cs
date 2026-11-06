namespace Terravia.Domain.Villages;

/// <summary>
/// Snapshot of a village's building slots and Capital eligibility used to plan a Capital transfer (<c>FR-015</c>).
/// </summary>
public sealed record VillageCapitalState(
    int VillageId,
    bool IsCapital,
    bool IsArchonVillage,
    int[] SlotBuildingTypes,
    int[] SlotLevels);
