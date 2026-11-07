using Terravia.Domain.Villages;

namespace Terravia.Infrastructure.Villages;

/// <summary>
/// Dapper persistence row model for <c>dbo.VillageTrainingQueues</c>.
/// </summary>
public sealed class VillageTrainingQueueItem
{
    public long QueueItemId { get; init; }
    public int VillageId { get; init; }
    public TrainingFacilityCategory FacilityCategory { get; init; }
    public int UnitTypeId { get; init; }
    public int AmountRemaining { get; init; }
    public int PopUpkeepPerUnit { get; init; }
    public int EachTimeSeconds { get; init; }
    public long CommenceAtMs { get; init; }
    public long FinishesAtMs { get; init; }
}
