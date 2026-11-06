namespace Terravia.Domain.Villages;

/// <summary>
/// Shared domain and validation constants for village slots, capacities, and construction rules.
/// </summary>
public static class VillageConsts
{
    public const int MaxNameLength = 40;
    public const int MinResourceFieldSlotId = 1;
    public const int MaxResourceFieldSlotId = 18;
    public const int MinInnerBuildingSlotId = 19;
    public const int MaxStandardSlotId = 40;
    public const int RallyPointSlotId = 39;
    public const int WallSlotId = 40;
    public const int WorldWonderSlotId = 99;
    public const int BaseStorageCapacity = 800;
    public const int InstantDemolitionGoldCost = 10;
    public const int MinMainBuildingLevelForDemolition = 10;
}
