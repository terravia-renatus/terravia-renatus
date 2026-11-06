namespace Terravia.Domain.Oases;

/// <summary>
/// Per-tick Nature fauna spawn increment rule for a specific wild oasis type (<c>UnitTypeId in 31..40</c>).
/// </summary>
public readonly record struct OasisFaunaSpawnRule(
    int UnitTypeId,
    int BaseMinIncrement,
    int RandomRangeMax);
