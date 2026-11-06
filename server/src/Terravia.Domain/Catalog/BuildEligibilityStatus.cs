namespace Terravia.Domain.Catalog;

/// <summary>
/// Canonical status codes returned when evaluating whether a village slot can be built or upgraded.
/// </summary>
public enum BuildEligibilityStatus
{
    CanBuildActive = 1,
    CanBuildWaitingLoop = 2,
    CanReserveMasterBuilder = 3,
    InvalidSlotPlacement = 4,
    PrerequisitesNotMet = 5,
    SlotBeingDemolished = 6,
    MaxLevelReached = 7,
    MaxLevelUnderConstruction = 8,
    QueueFull = 9,
    WarehouseCapacityInsufficient = 10,
    GranaryCapacityInsufficient = 11,
    InsufficientResources = 12,
    CropStarvationBlock = 13
}
