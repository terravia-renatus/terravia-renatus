namespace Terravia.Domain.Villages;

/// <summary>
/// Stateless, pure C# calculator for the 6-step village resource production pipeline,
/// Warehouse/Granary capacity aggregation, net crop upkeep, and lazy $O(1)$ timestamp-delta accrual.
/// </summary>
public interface IResourceProductionCalculator
{
    ResourceProductionSnapshot CalculateProduction(in VillageProductionContext context);

    (int WarehouseCap, int GranaryCap) CalculateStorageCapacities(
        ReadOnlySpan<int> slotBuildingTypes,
        ReadOnlySpan<int> slotLevels,
        double storageMultiplier = 1.0);

    LazyAccrualResult AccrueVillageResourcesAndLoyalty(
        in VillageAccrualInput input,
        long nowMs);
}
