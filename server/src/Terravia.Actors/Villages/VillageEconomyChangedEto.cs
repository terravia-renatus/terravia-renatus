namespace Terravia.Actors.Villages;

/// <summary>
/// Cross-actor and SignalR Event Transfer Object broadcast when a village's economy, loyalty, or expansion state changes.
/// </summary>
public sealed record VillageEconomyChangedEto(
    int VillageId,
    long ServerTimeMs,
    VillageEconomySnapshotDto Snapshot);
