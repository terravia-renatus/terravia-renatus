namespace Terravia.Domain.Villages;

/// <summary>
/// Clamped resource stocks and loyalty returned by <see cref="IResourceProductionCalculator.AccrueVillageResourcesAndLoyalty"/>.
/// </summary>
public readonly record struct LazyAccrualResult(
    double Wood,
    double Clay,
    double Iron,
    double Crop,
    double Loyalty,
    long UpdatedAtMs);
