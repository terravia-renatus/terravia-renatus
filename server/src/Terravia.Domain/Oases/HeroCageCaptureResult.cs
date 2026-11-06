namespace Terravia.Domain.Oases;

/// <summary>
/// Result of pre-combat Hero Cage (<c>ItemTypeId = 114</c>) animal capture at an unoccupied wild oasis.
/// </summary>
public sealed record HeroCageCaptureResult(
    int CagesConsumed,
    int[] CapturedAnimalsByUnitType31To40,
    int RemainingWildDefenders,
    bool SkipCombat);
