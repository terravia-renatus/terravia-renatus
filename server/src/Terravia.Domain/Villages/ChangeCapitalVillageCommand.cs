namespace Terravia.Domain.Villages;

/// <summary>
/// Command to designate a new Capital village via a Palace (<c>gid 26</c>) or Hun Command Center (<c>gid 44</c>).
/// </summary>
public sealed record ChangeCapitalVillageCommand(
    int ActorPlayerId,
    int OwnerPlayerId,
    int NewCapitalVillageId);
