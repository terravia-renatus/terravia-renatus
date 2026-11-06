namespace Terravia.Domain.Villages;

/// <summary>
/// Canonical status codes returned when evaluating whether a village building can be demolished.
/// </summary>
public enum DemolitionStatus
{
    Allowed = 1,
    MainBuildingBelowLevel10 = 2,
    InvalidSlot = 3,
    EmptySlot = 4,
    SlotHasActiveUpgrade = 5,
    DemolitionAlreadyActive = 6,
    CannotDemolishLastAllianceEmbassy = 7,
    InsufficientGold = 8
}
