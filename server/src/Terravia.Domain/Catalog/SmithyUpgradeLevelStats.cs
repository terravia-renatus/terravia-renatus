namespace Terravia.Domain.Catalog;

/// <summary>
/// Smithy (Blacksmith/Armoury) upgrade cost and duration for a single level (1..20).
/// </summary>
public readonly record struct SmithyUpgradeLevelStats(
    int Level,
    ResourceCost Cost,
    int BaseTimeSeconds);
