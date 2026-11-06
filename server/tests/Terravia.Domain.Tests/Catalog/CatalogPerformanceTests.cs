using System.Diagnostics;
using Terravia.Domain.Catalog;

namespace Terravia.Domain.Tests.Catalog;

public sealed class CatalogPerformanceTests
{
    private readonly IGameCatalog _catalog = FrozenGameCatalog.Default;
    private readonly IConstructionCalculator _calculator;
    private readonly IBuildingRuleCatalog _ruleCatalog;

    public CatalogPerformanceTests()
    {
        _calculator = new ConstructionCalculator(_catalog);
        _ruleCatalog = new BuildingRuleCatalog(_catalog, _calculator);
    }

    [Fact]
    public void FullVillage40SlotAndAll50BuildingsEvaluation_ShouldCompleteUnderOneMillisecond_SC004()
    {
        var slotTypes = new int[41];
        var slotLevels = new int[41];
        for (int i = 1; i <= 18; i++)
        {
            slotTypes[i] = ((i - 1) % 4) + 1;
            slotLevels[i] = 10;
        }
        slotTypes[19] = 15; slotLevels[19] = 15; // Main Building
        slotTypes[20] = 10; slotLevels[20] = 20; // Warehouse
        slotTypes[21] = 11; slotLevels[21] = 20; // Granary
        slotTypes[22] = 19; slotLevels[22] = 15; // Barracks
        slotTypes[23] = 22; slotLevels[23] = 15; // Academy
        slotTypes[24] = 12; slotLevels[24] = 10; // Blacksmith
        slotTypes[25] = 20; slotLevels[25] = 10; // Stable
        slotTypes[39] = 16; slotLevels[39] = 10; // Rally Point
        slotTypes[40] = 31; slotLevels[40] = 10; // City Wall

        var prereqCtx = new VillagePrerequisiteContext(
            TribeId: 1,
            IsCapital: true,
            IsArchonVillage: false,
            IsWorldWonderVillage: false,
            HasAccountPalace: false,
            HasGreatStorageArtifact: false,
            OwnedWwPlanCount: 0,
            AllianceWwPlanCount: 0,
            GreatWorkshopEnabled: true,
            SlotBuildingTypes: slotTypes,
            SlotLevels: slotLevels);

        var constructCtx = new VillageConstructionContext(
            PrerequisiteContext: prereqCtx,
            AvailableWood: 50_000,
            AvailableClay: 50_000,
            AvailableIron: 50_000,
            AvailableCrop: 50_000,
            WarehouseCapacity: 80_000,
            GranaryCapacity: 80_000,
            NetCropProductionPerHour: 500,
            HasPlusSubscription: true,
            AllowAllTribeRomanQueue: false,
            MaxMasterBuilderSlots: 3,
            ActiveResourceFieldJobs: 0,
            ActiveInnerBuildingJobs: 0,
            WaitingLoopJobs: 0,
            MasterBuilderJobs: 0,
            QueuedJobsForTargetSlot: 0,
            HasCroplandUpgradeQueued: false);

        // Warmup JIT
        for (int w = 0; w < 10; w++)
        {
            for (int gid = 1; gid <= 50; gid++)
                _ = _ruleCatalog.MeetsPrerequisites(gid, in prereqCtx);
            for (int slot = 1; slot <= 40; slot++)
                _ = _ruleCatalog.EvaluateSlotUpgrade(slot, slotTypes[slot] > 0 ? slotTypes[slot] : 23, in constructCtx);
        }

        const int iterations = 100;
        var sw = Stopwatch.StartNew();
        for (int iter = 0; iter < iterations; iter++)
        {
            for (int gid = 1; gid <= 50; gid++)
                _ = _ruleCatalog.MeetsPrerequisites(gid, in prereqCtx);
            for (int slot = 1; slot <= 40; slot++)
                _ = _ruleCatalog.EvaluateSlotUpgrade(slot, slotTypes[slot] > 0 ? slotTypes[slot] : 23, in constructCtx);
        }
        sw.Stop();

        double avgMsPerVillage = sw.Elapsed.TotalMilliseconds / iterations;
        Assert.True(avgMsPerVillage < 1.0, $"Expected < 1.0ms per full village evaluation (SC-004), but took {avgMsPerVillage:F4}ms.");
    }
}
