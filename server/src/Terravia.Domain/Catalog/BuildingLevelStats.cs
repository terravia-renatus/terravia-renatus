namespace Terravia.Domain.Catalog;

/// <summary>
/// Value object for a specific (BuildingTypeId, Level) containing cost, pop, CP, time, and attribute value.
/// </summary>
public readonly record struct BuildingLevelStats(
    int Level,
    ResourceCost Cost,
    int Population,
    int CulturePoints,
    int BaseTimeSeconds,
    double AttributeValue,
    double? TrainingMultiplier = null);
