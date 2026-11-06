namespace Terravia.Domain.Villages;

/// <summary>
/// Shared domain and validation constants for village slots, capacities, topologies, loyalty, and expansion rules.
/// </summary>
public static class VillageConsts
{
    public const int MaxNameLength = 64;
    public const int MinFieldTopologyId = 1;
    public const int MaxFieldTopologyId = 12;
    public const int DefaultStartingCapitalTopologyId = 3;

    public const int MinResourceFieldSlotId = 1;
    public const int MaxResourceFieldSlotId = 18;
    public const int MinInnerBuildingSlotId = 19;
    public const int MaxStandardSlotId = 40;
    public const int DefaultMainBuildingSlotId = 26;
    public const int RallyPointSlotId = 39;
    public const int WallSlotId = 40;
    public const int WorldWonderSlotId = 99;

    public const int MinResourceFieldLevel = 0;
    public const int MaxNonCapitalResourceFieldLevel = 10;
    public const int MaxCapitalResourceFieldLevel = 20;

    public const int BaseStorageCapacity = 800;
    public const int FoundingResourceAmount = 750;
    public const int FoundingPopulation = 2;
    public const int FoundingCulturePointsPerDay = 1;
    public const int SettlersRequiredForFounding = 3;

    public const double MinLoyalty = 0.0;
    public const double MaxLoyalty = 100.0;

    public const int MaxTrapsPerVillage = 400;
    public const int HeroBaseCropUpkeep = 6;
    public const int HeroStarvationCropCredit = 6;

    public const int InstantDemolitionGoldCost = 10;
    public const int MinMainBuildingLevelForDemolition = 10;
}
