namespace Terravia.Actors.Villages;

/// <summary>
/// Data Transfer Object representing a single populated village slot (1..40 or 99) included in <see cref="VillageConstructionChangedEto"/>.
/// </summary>
public sealed record VillageSlotDto(
    int SlotId,
    int BuildingTypeId,
    int Level);
