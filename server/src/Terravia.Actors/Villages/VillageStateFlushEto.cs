namespace Terravia.Actors.Villages;

/// <summary>
/// Cross-boundary Event Transfer Object sent from <see cref="VillageActor"/> to the write-behind persistence coordinator.
/// </summary>
public sealed record VillageStateFlushEto(
    int VillageId,
    bool IsImmediateGoldFlush,
    long TimestampMs);
