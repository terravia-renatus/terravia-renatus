namespace Terravia.Actors.Oases;

/// <summary>
/// Event Transfer Object emitted when an oasis is annexed, conquered, or released back to wild <c>Nature</c> state.
/// </summary>
public sealed record OasisOwnershipChangedEto(
    int OasisTileId,
    byte OasisTypeId,
    int? PreviousOwnerVillageId,
    int? NewOwnerVillageId,
    int NewOwnerPlayerId,
    IReadOnlyList<long> ReturnedReinforcementStackIds,
    long TimestampMs);
