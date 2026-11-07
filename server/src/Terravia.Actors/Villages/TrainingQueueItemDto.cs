using Terravia.Domain.Villages;

namespace Terravia.Actors.Villages;

/// <summary>
/// Read-only projection of an active military training, Trapper, or Hospital healing queue item.
/// </summary>
public sealed record TrainingQueueItemDto(
    long QueueItemId,
    TrainingFacilityCategory FacilityCategory,
    int UnitTypeId,
    int AmountRemaining,
    int EachTimeSeconds,
    long CommenceAtMs,
    long FinishesAtMs,
    long NextUnitGraduatesAtMs);
