namespace Terravia.Domain.Villages;

/// <summary>
/// Command to start a Small or Great Celebration in a village's Town Hall (<c>gid 24</c>).
/// </summary>
public sealed record StartTownHallCelebrationCommand(
    int ActorPlayerId,
    int OwnerPlayerId,
    int VillageId,
    CelebrationType CelebrationType);
