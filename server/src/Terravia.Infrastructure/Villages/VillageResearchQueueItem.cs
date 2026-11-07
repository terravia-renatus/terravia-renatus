using Terravia.Domain.Villages;

namespace Terravia.Infrastructure.Villages;

/// <summary>
/// Dapper persistence row model for <c>dbo.VillageResearchQueues</c>.
/// </summary>
public sealed class VillageResearchQueueItem
{
    public long QueueItemId { get; init; }
    public int VillageId { get; init; }
    public ResearchCategory Category { get; init; }
    public int UnitTypeId { get; init; }
    public int TribeUnitSlot { get; init; }
    public int TargetLevel { get; init; }
    public bool IsPlusWaitingLoop { get; init; }
    public long StartedAtMs { get; init; }
    public long FinishesAtMs { get; init; }
}
