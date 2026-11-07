using Terravia.Domain.Villages;

namespace Terravia.Actors.Villages;

/// <summary>
/// Read-only projection of an active Academy discovery or Blacksmith/Armoury combat upgrade queue item.
/// </summary>
public sealed record ResearchQueueItemDto(
    long QueueItemId,
    ResearchCategory Category,
    int UnitTypeId,
    int TribeUnitSlot,
    int TargetLevel,
    bool IsPlusWaitingLoop,
    long StartedAtMs,
    long FinishesAtMs);
