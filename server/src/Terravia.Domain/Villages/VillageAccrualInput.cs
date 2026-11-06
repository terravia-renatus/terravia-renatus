namespace Terravia.Domain.Villages;

/// <summary>
/// Input parameters for lazy $O(1)$ resource and loyalty accrual over elapsed milliseconds.
/// </summary>
public readonly record struct VillageAccrualInput(
    double CurrentWood,
    double CurrentClay,
    double CurrentIron,
    double CurrentCrop,
    int WarehouseCap,
    int GranaryCap,
    int GrossWoodPerHour,
    int GrossClayPerHour,
    int GrossIronPerHour,
    int NetCropPerHour,
    double CurrentLoyalty,
    double LoyaltyRegenPerHour,
    long LastUpdatedMs);
