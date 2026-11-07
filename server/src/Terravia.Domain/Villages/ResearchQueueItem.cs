namespace Terravia.Domain.Villages;

/// <summary>
/// Active or Plus-waiting-loop Academy discovery or Blacksmith/Armoury upgrade in <see cref="VillageState.ResearchQueue"/> (FR-008..FR-009).
/// </summary>
public sealed class ResearchQueueItem
{
    public long QueueItemId { get; set; }
    public int VillageId { get; set; }
    public ResearchCategory Category { get; set; }
    public int UnitTypeId { get; set; }
    public int TribeUnitSlot { get; set; }
    public int TargetLevel { get; set; }
    public bool IsPlusWaitingLoop { get; set; }
    public long StartedAtMs { get; set; }
    public long FinishesAtMs { get; set; }
}
