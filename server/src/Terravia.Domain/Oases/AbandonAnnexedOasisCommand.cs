namespace Terravia.Domain.Oases;

/// <summary>
/// Command to manually abandon (release) an annexed oasis via Hero's Mansion (<c>gid 37</c>).
/// </summary>
public sealed record AbandonAnnexedOasisCommand(
    int ActorPlayerId,
    int OwnerPlayerId,
    int VillageId,
    int OasisTileId);
