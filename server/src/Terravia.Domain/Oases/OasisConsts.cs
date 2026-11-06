namespace Terravia.Domain.Oases;

/// <summary>
/// Domain constants for oasis types, richness tiers, toroidal annexation distance, Hero Cage capture, and Nature faction state.
/// </summary>
public static class OasisConsts
{
    public const int MinOasisTypeId = 1;
    public const int MaxOasisTypeId = 12;
    public const int FiftyPercentCropOasisTypeId = 12;

    public const int MinRichnessTier = 0;
    public const int MaxRichnessTier = 2;

    public const int MaxToroidalAnnexationDistance = 3;
    public const int MaxOasisSlotsPerVillage = 3;

    public const double WildOasisMaxResourceStock = 800.0;
    public const double WildOasisBaseHourlyResourceGen = 10.0;
    public const double WildOasisFiftyCropHourlyGen = 20.0;
    public const double AnnexedOasisRaidLootFraction = 0.10;

    public const double MinLoyalty = 0.0;
    public const double MaxLoyalty = 100.0;

    public const int HeroCageItemTypeId = 114;
    public const int NatureTribeId = 4;
    public const int NaturePlayerId = 2;
    public const int CapturedNatureOwnerVillageId = 0;
    public const int MinNatureUnitTypeId = 31;
    public const int MaxNatureUnitTypeId = 40;

    public const double MinGrowthFactor = 0.3;
    public const double MaxGrowthFactor = 3.0;
}
