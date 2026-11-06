using Terravia.Domain.Catalog;

namespace Terravia.Domain.Villages;

/// <summary>
/// Represents an active, Plus waiting-loop, or Master Builder reserved construction job in a village.
/// </summary>
public sealed record ConstructionQueueItem(
    long QueueItemId,
    int VillageId,
    int SlotId,
    int BuildingTypeId,
    int TargetLevel,
    bool IsWaitingLoop,
    bool IsMasterBuilder,
    long StartedAtMs,
    long FinishesAtMs,
    ResourceCost DeductedOrRequiredCost,
    int BuildDurationSeconds)
{
    public bool IsResourceField => SlotId is >= 1 and <= 18;
    public bool IsActive => !IsWaitingLoop && !IsMasterBuilder;
}
