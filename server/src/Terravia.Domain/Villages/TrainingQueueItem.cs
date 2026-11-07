namespace Terravia.Domain.Villages;

/// <summary>
/// Active or queued unit recruitment, trap crafting, or Hospital healing batch in <see cref="VillageState.TrainingQueues"/> (FR-001..FR-003).
/// </summary>
public sealed class TrainingQueueItem
{
    public long QueueItemId { get; set; }
    public int VillageId { get; set; }
    public TrainingFacilityCategory FacilityCategory { get; set; }
    public int UnitTypeId { get; set; }
    public int AmountRemaining { get; set; }
    public int PopUpkeepPerUnit { get; set; }
    public int EachTimeSeconds { get; set; }
    public long CommenceAtMs { get; set; }
    public long FinishesAtMs { get; set; }

    public long NextUnitGraduatesAtMs => CommenceAtMs + (long)Math.Max(1, EachTimeSeconds) * 1000L;
}
