using Terravia.Domain.Villages;

namespace Terravia.Actors.Villages;

/// <summary>
/// Event Transfer Object emitted when one or more units starve in a village.
/// </summary>
public sealed record VillageStarvationExecutedEto(
    int VillageId,
    long TimestampMs,
    IReadOnlyList<StarvationStepResult> Steps,
    double ResultingStoredCrop,
    int ResultingNetCropPerHour);
