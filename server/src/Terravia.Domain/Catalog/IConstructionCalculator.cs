namespace Terravia.Domain.Catalog;

/// <summary>
/// Stateless calculator for construction duration, demolition duration, village Culture Points, and unit crop upkeep.
/// </summary>
public interface IConstructionCalculator
{
    /// <summary>
    /// Calculates exact construction time in seconds for targetLevel, scaled by Main Building (gid 15)
    /// attribute percentage table (or 5x penalty when Main Building == 0) and server Speed multiplier.
    /// </summary>
    int CalculateBuildTimeSeconds(
        int buildingTypeId,
        int targetLevel,
        int mainBuildingLevel,
        double serverSpeed);

    /// <summary>
    /// Calculates timed 1-level demolition duration: floor(CalculateBuildTimeSeconds(gid, currentLevel) / 2.0).
    /// </summary>
    int CalculateDemolitionTimeSeconds(
        int buildingTypeId,
        int currentLevel,
        int mainBuildingLevel,
        double serverSpeed);

    /// <summary>
    /// Recounts total base Culture Points per day across slots 1..40 (and WW slot 99) before Alliance Philosophy bonus.
    /// </summary>
    int CalculateVillageCulturePoints(
        ReadOnlySpan<int> slotBuildingTypes,
        ReadOnlySpan<int> slotLevels,
        int worldWonderLevel = 0);

    /// <summary>
    /// Calculates effective hourly crop upkeep for a unit stack, applying Roman Horse Drinking Trough
    /// (gid 41 at levels 10, 15, 20 for u4, u5, u6) and Diet Artifact modifiers.
    /// </summary>
    double CalculateUnitUpkeepPerHour(
        int unitTypeId,
        int count,
        int horseDrinkingTroughLevel,
        double artifactDietMultiplier = 1.0);
}
