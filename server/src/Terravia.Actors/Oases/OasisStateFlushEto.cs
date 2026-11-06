namespace Terravia.Actors.Oases;

/// <summary>
/// Event Transfer Object emitted when an <c>OasisActor</c> requires write-behind or immediate persistence flush.
/// </summary>
public sealed record OasisStateFlushEto(
    int TileId,
    bool IsImmediateFlush,
    long TimestampMs);
