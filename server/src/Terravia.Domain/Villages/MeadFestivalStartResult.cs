using Terravia.Domain.Catalog;

namespace Terravia.Domain.Villages;

/// <summary>
/// Validation and modifier result for starting a Teuton Brewery (<c>gid 35</c>) Mead Festival in the Capital.
/// </summary>
public readonly record struct MeadFestivalStartResult(
    bool Succeeded,
    string? ErrorCode,
    ResourceCost DeductedCost,
    int DurationSeconds,
    int AttackBonusPercent,
    bool ForceRandomCatapultTargets,
    double ChiefPersuasionMultiplier);
