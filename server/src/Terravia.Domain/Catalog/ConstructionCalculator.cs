namespace Terravia.Domain.Catalog;

/// <summary>
/// Stateless, pure domain calculator for construction duration, demolition duration, Culture Points, and troop upkeep.
/// </summary>
public sealed class ConstructionCalculator : IConstructionCalculator
{
    private readonly IGameCatalog _catalog;

    public ConstructionCalculator(IGameCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public int CalculateBuildTimeSeconds(
        int buildingTypeId,
        int targetLevel,
        int mainBuildingLevel,
        double serverSpeed)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(serverSpeed);
        if (mainBuildingLevel is < 0 or > 20)
            throw new ArgumentOutOfRangeException(nameof(mainBuildingLevel), "Main Building level must be in range 0..20.");

        var levelStats = _catalog.GetBuildingLevelStats(buildingTypeId, targetLevel);
        double baseTime = levelStats.BaseTimeSeconds;

        // Canonical Main Building duration scaling:
        // If building Main Building (gid == 15):
        //   When current Main Building level == 0: round(baseTime / Speed * 5)
        //   When current Main Building level > 0:  round(baseTime * (bid15[mbLevel].attri / 100) / Speed)
        // For all buildings:
        //   When Main Building level == 0: 5x penalty (round(baseTime * 5.0 / serverSpeed))
        //   When Main Building level >= 1: round(baseTime * (bid15[mainBuildingLevel].AttributeValue / 100.0) / serverSpeed)
        if (mainBuildingLevel == 0)
        {
            return (int)Math.Round((baseTime * 5.0) / serverSpeed, MidpointRounding.AwayFromZero);
        }

        double mbAttriPercent = _catalog.GetBuildingLevelStats(15, mainBuildingLevel).AttributeValue;
        double scaledSeconds = (baseTime * (mbAttriPercent / 100.0)) / serverSpeed;
        return (int)Math.Round(scaledSeconds, MidpointRounding.AwayFromZero);
    }

    public int CalculateDemolitionTimeSeconds(
        int buildingTypeId,
        int currentLevel,
        int mainBuildingLevel,
        double serverSpeed)
    {
        int buildTime = CalculateBuildTimeSeconds(buildingTypeId, currentLevel, mainBuildingLevel, serverSpeed);
        return (int)Math.Floor(buildTime / 2.0);
    }

    public int CalculateVillageCulturePoints(
        ReadOnlySpan<int> slotBuildingTypes,
        ReadOnlySpan<int> slotLevels,
        int worldWonderLevel = 0)
    {
        int totalCp = 0;
        int len = Math.Min(slotBuildingTypes.Length, slotLevels.Length);
        for (int i = 1; i < len && i <= 40; i++)
        {
            int gid = slotBuildingTypes[i];
            int lvl = slotLevels[i];
            if (gid is >= 1 and <= 50 && lvl >= 1)
            {
                var building = _catalog.GetBuilding(gid);
                int clampedLevel = Math.Min(lvl, building.MaxLevel);
                totalCp += building.GetLevelStats(clampedLevel).CulturePoints;
            }
        }

        if (worldWonderLevel >= 1)
        {
            var ww = _catalog.GetBuilding(40);
            int clampedWw = Math.Min(worldWonderLevel, ww.MaxLevel);
            totalCp += ww.GetLevelStats(clampedWw).CulturePoints;
        }

        return totalCp;
    }

    public double CalculateUnitUpkeepPerHour(
        int unitTypeId,
        int count,
        int horseDrinkingTroughLevel,
        double artifactDietMultiplier = 1.0)
    {
        if (count <= 0)
            return 0.0;
        ArgumentOutOfRangeException.ThrowIfNegative(horseDrinkingTroughLevel);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(artifactDietMultiplier);

        var unit = _catalog.GetUnit(unitTypeId);
        int perUnitUpkeep = unit.UpkeepCropPerHour;

        if (unit.HorseDrinkingTroughThresholdLevel is { } threshold &&
            horseDrinkingTroughLevel >= threshold &&
            perUnitUpkeep > 0)
        {
            perUnitUpkeep -= 1;
        }

        return count * perUnitUpkeep * artifactDietMultiplier;
    }
}
