namespace Terravia.Domain.Villages;

/// <summary>
/// Deterministic hourly gross production and net crop upkeep snapshot returned by <see cref="IResourceProductionCalculator"/>.
/// </summary>
public readonly record struct ResourceProductionSnapshot(
    int GrossWoodPerHour,
    int GrossClayPerHour,
    int GrossIronPerHour,
    int GrossCropPerHour,
    int EffectivePopulationUpkeep,
    int TotalTroopUpkeepPerHour,
    int NetCropPerHour);
