namespace Terravia.Actors.Players;

/// <summary>
/// Event Transfer Object emitted when a player transfers their Capital to a new village
/// (or when a razed Capital is automatically reassigned to the highest-population remaining village).
/// </summary>
public sealed record CapitalVillageTransferredEto(
    int PlayerId,
    int OldCapitalVillageId,
    int NewCapitalVillageId,
    bool IsAutomaticReassignmentOnRazing,
    long TimestampMs);
