namespace Terravia.Actors.Villages;

/// <summary>
/// Real-time snapshot of a village's unlocked unit slots, Smithy upgrade levels, Hospital wounded reserves,
/// Gaul Trapper capacity, and active training/research queues.
/// </summary>
public sealed record VillageMilitarySnapshotDto(
    int VillageId,
    IReadOnlyList<bool> UnlockedUnitSlots,
    IReadOnlyList<int> OffenseUpgradeLevels,
    IReadOnlyList<int> DefenseUpgradeLevels,
    IReadOnlyList<int> WoundedUnitsBySlot,
    int MaxHospitalCapacity,
    int TotalTrapsCount,
    int OccupiedTrapsCount,
    int MaxTrapCapacity,
    IReadOnlyList<TrainingQueueItemDto> TrainingQueues,
    IReadOnlyList<ResearchQueueItemDto> ResearchQueue);
