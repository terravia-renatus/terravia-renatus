using Terravia.Domain.Catalog;

namespace Terravia.Domain.Villages;

/// <summary>
/// Outcome of attempting to enqueue a military training, trap crafting, or Hospital healing batch.
/// </summary>
public readonly record struct TrainingEnrollmentResult(
    bool Succeeded,
    string? ErrorCode,
    int EnrolledAmount,
    int EachTimeSeconds,
    ResourceCost DeductedCost,
    long FinishesAtMs,
    bool WasMergedIntoTrailingBatch);
