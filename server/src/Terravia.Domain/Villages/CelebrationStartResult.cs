using Terravia.Domain.Catalog;

namespace Terravia.Domain.Villages;

/// <summary>
/// Validation and duration result for starting a Town Hall (<c>gid 24</c>) celebration.
/// </summary>
public readonly record struct CelebrationStartResult(
    bool Succeeded,
    string? ErrorCode,
    CelebrationType CelebrationType,
    ResourceCost DeductedCost,
    int DurationSeconds,
    int CompletionCulturePointsAward);
