namespace Terravia.Infrastructure.Villages;

/// <summary>
/// Persistence model for a construction or demolition queue item (VillageQueueItems table).
/// </summary>
public sealed class VillageQueueItem
{
    public long QueueItemId { get; set; }
    public int VillageId { get; set; }
    public VillageQueueCategory QueueCategory { get; set; }
    public int SlotId { get; set; }
    public int BuildingTypeId { get; set; }
    public int TargetLevel { get; set; }
    public bool IsWaitingLoop { get; set; }
    public bool IsMasterBuilder { get; set; }
    public long StartedAtMs { get; set; }
    public long FinishesAtMs { get; set; }
}
