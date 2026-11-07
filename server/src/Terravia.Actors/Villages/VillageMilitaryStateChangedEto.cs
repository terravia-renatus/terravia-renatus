namespace Terravia.Actors.Villages;

/// <summary>
/// Event Transfer Object emitted when a village's military training, research, Hospital, or Trapper state changes.
/// </summary>
public sealed record VillageMilitaryStateChangedEto(
    int VillageId,
    long TimestampMs,
    VillageMilitarySnapshotDto Snapshot);
