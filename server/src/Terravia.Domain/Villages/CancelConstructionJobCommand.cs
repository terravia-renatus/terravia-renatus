namespace Terravia.Domain.Villages;

/// <summary>
/// Command to cancel a queued active, Plus waiting-loop, or Master Builder construction job.
/// </summary>
public sealed record CancelConstructionJobCommand(
    int ActorPlayerId,
    int OwnerPlayerId,
    int VillageId,
    long QueueItemId);
