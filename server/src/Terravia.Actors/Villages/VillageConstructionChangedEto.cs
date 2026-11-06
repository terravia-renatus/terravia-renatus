using Terravia.Domain.Villages;

namespace Terravia.Actors.Villages;

/// <summary>
/// Cross-boundary Event Transfer Object emitted by <see cref="VillageActor"/> when construction or demolition state changes for SignalR client broadcast.
/// </summary>
public sealed record VillageConstructionChangedEto(
    int VillageId,
    long ServerTimeMs,
    int Wood,
    int Clay,
    int Iron,
    int Crop,
    int WarehouseCap,
    int GranaryCap,
    int NetCropPerHour,
    int Population,
    int CulturePointsPerDay,
    IReadOnlyList<VillageSlotDto> UpdatedSlots,
    IReadOnlyList<ConstructionQueueItem> ConstructionQueue,
    DemolitionQueueItem? ActiveDemolition);
