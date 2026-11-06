namespace Terravia.Domain.Catalog;

/// <summary>
/// Hero revival cost and duration for a single hero level (0..60).
/// </summary>
public readonly record struct HeroReviveLevelStats(
    int Level,
    ResourceCost Cost,
    int BaseTimeSeconds);
