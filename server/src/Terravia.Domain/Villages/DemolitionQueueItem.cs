namespace Terravia.Domain.Villages;

/// <summary>
/// Active timed demolition job in a village (at most 1 per village).
/// </summary>
public sealed record DemolitionQueueItem(
    int VillageId,
    int SlotId,
    int BuildingTypeId,
    int TargetLevel,
    long FinishesAtMs);

