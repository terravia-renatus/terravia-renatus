namespace Terravia.Actors.Players;

/// <summary>
/// Outbound read projection / SignalR broadcast payload for a player's empire-wide Culture Point state.
/// </summary>
public sealed record PlayerCultureSnapshotDto(
    int PlayerId,
    double AccumulatedCulturePoints,
    double DailyCulturePointsRate,
    long LastCpUpdatedServerMs,
    int OwnedVillageCount,
    int NextVillageRequiredCp,
    int CapitalVillageId);
