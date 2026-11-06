using Terravia.Domain.Villages;

namespace Terravia.Domain.Tests.Villages;

public sealed class ResourceProductionAndStorageCalculatorTests
{
    private readonly ResourceProductionCalculator _calculator = new();

    [Theory]
    [InlineData(0, 2, 800, 800)]
    [InlineData(1, 5, 1200, 3600)]
    [InlineData(5, 33, 4000, 12000)]
    [InlineData(10, 200, 11800, 35400)]
    [InlineData(15, 800, 31300, 93900)]
    [InlineData(20, 2450, 80000, 240000)]
    public void FieldProductionAndStorageCurves_MatchCanonicalTable(
        int level,
        int expectedFieldProd,
        int expectedStdStorage,
        int expectedGreatStorage)
    {
        Assert.Equal(expectedFieldProd, ResourceProductionCalculator.GetBaseFieldProductionPerHour(level));
        Assert.Equal(expectedStdStorage, ResourceProductionCalculator.GetStandardStorageCapacity(level));
        Assert.Equal(expectedGreatStorage, ResourceProductionCalculator.GetGreatStorageCapacity(level));
    }

    [Fact]
    public void CalculateProduction_SixStepStackingWithWaterworksMillBakeryHeroAndPlus_MatchesCanonicalMath()
    {
        var slots = new int[41];
        var levels = new int[41];
        VillageFieldTopologyCatalog.Default.InitializeOuterResourceSlots(6, slots, levels); // 15-cropper (1-1-1-15)

        // Set all 15 Croplands to Level 10 (200/hr each -> B_crop = 3,000)
        // Set Wood/Clay/Iron fields to Level 10 (200/hr each -> B_w=200, B_c=200, B_i=200)
        for (int s = 1; s <= 18; s++)
        {
            levels[s] = 10;
        }

        // Grain Mill (gid 8) Lvl 5 (+25%) + Bakery (gid 9) Lvl 5 (+25%) = +50%
        slots[19] = 8; levels[19] = 5;
        slots[20] = 9; levels[20] = 5;
        // Egyptian Waterworks (gid 45) Lvl 20 (+100% oasis amplification -> OasisFactor = 0.50)
        slots[21] = 45; levels[21] = 20;

        // 3 annexed +50% crop oases -> O_crop = 6 units of 25%
        // Step 1: B_crop = 3,000
        // Step 2: A1 = 3000 + 3000 * 0.50 * 6 = 12,000
        // Step 3: A2 = 12000 * 1.50 = 18,000
        // Step 4: Hero SingleResource Crop (100 pts * 10 = 1,000) -> A3 = 19,000
        // Step 5: Plus +25% Crop active -> A4 = 19000 * 1.25 = 23,750
        // Step 6: Speed 2.0x -> GrossCrop = 47,500
        var ctx = new VillageProductionContext(
            SlotBuildingTypes: slots,
            SlotLevels: levels,
            AnnexedOasisWoodUnits: 0,
            AnnexedOasisClayUnits: 0,
            AnnexedOasisIronUnits: 0,
            AnnexedOasisCropUnits: 6,
            HasStationedLivingHero: true,
            HeroResourceBonusPoints: 100,
            HeroResourceFocusMode: HeroResourceFocusMode.Crop,
            PlusWoodBonusActive: false,
            PlusClayBonusActive: false,
            PlusIronBonusActive: false,
            PlusCropBonusActive: true,
            IsArchonVillage: false,
            VillageTribeId: 7,
            Population: 500,
            FedTroopStacks: null,
            DietControlArtifactMultiplier: 1.0,
            ServerSpeed: 2.0);

        var snapshot = _calculator.CalculateProduction(in ctx);

        Assert.Equal(47_500, snapshot.GrossCropPerHour);
        Assert.Equal(500, snapshot.EffectivePopulationUpkeep);
        Assert.Equal(47_000, snapshot.NetCropPerHour);
        Assert.Equal(400, snapshot.GrossWoodPerHour);
    }

    [Fact]
    public void CalculateStorageCapacities_SumsMultipleStandardAndGreatStorages_AndEnforcesBaseFloor()
    {
        var slots = new int[41];
        var levels = new int[41];

        // Empty village -> BaseStorageCapacity floor (800 * 2 = 1600)
        var (emptyWh, emptyGr) = _calculator.CalculateStorageCapacities(slots, levels, storageMultiplier: 2.0);
        Assert.Equal(1600, emptyWh);
        Assert.Equal(1600, emptyGr);

        // Add Warehouse Lvl 20 (80,000) + Great Warehouse Lvl 20 (240,000) = 320,000 (at 1x)
        // Add Granary Lvl 10 (11,800) + Great Granary Lvl 10 (35,400) = 47,200 (at 1x)
        slots[19] = 10; levels[19] = 20;
        slots[20] = 38; levels[20] = 20;
        slots[21] = 11; levels[21] = 10;
        slots[22] = 39; levels[22] = 10;

        var (whCap, grCap) = _calculator.CalculateStorageCapacities(slots, levels, storageMultiplier: 1.0);
        Assert.Equal(320_000, whCap);
        Assert.Equal(47_200, grCap);
    }

    [Fact]
    public void AccrueVillageResourcesAndLoyalty_ClampsStrictlyAtZeroAndStorageCaps()
    {
        var input = new VillageAccrualInput(
            CurrentWood: 700,
            CurrentClay: 500,
            CurrentIron: 100,
            CurrentCrop: 200,
            WarehouseCap: 800,
            GranaryCap: 800,
            GrossWoodPerHour: 400,
            GrossClayPerHour: 200,
            GrossIronPerHour: 100,
            NetCropPerHour: -300,
            CurrentLoyalty: 80.0,
            LoyaltyRegenPerHour: 15.0,
            LastUpdatedMs: 1_000_000L);

        // Advance 2 hours (7,200,000 ms)
        var accrued = _calculator.AccrueVillageResourcesAndLoyalty(in input, 8_200_000L);

        Assert.Equal(800.0, accrued.Wood);      // 700 + 800 clamped at 800
        Assert.Equal(800.0, accrued.Clay);      // 500 + 400 clamped at 800
        Assert.Equal(300.0, accrued.Iron);      // 100 + 200 = 300
        Assert.Equal(0.0, accrued.Crop);        // 200 - 600 clamped at 0
        Assert.Equal(100.0, accrued.Loyalty);   // 80 + 30 clamped at 100
    }
}
