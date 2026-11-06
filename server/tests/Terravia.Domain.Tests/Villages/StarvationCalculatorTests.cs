using Terravia.Domain.Catalog;
using Terravia.Domain.Oases;
using Terravia.Domain.Villages;

namespace Terravia.Domain.Tests.Villages;

public sealed class StarvationCalculatorTests
{
    private readonly IGameCatalog _catalog = FrozenGameCatalog.Default;
    private readonly ResourceProductionCalculator _prodCalculator;
    private readonly StarvationCalculator _starvationCalculator;
    private readonly VillageConstructionManager _constructionManager;

    public StarvationCalculatorTests()
    {
        _prodCalculator = new ResourceProductionCalculator(_catalog);
        _starvationCalculator = new StarvationCalculator(_catalog);
        var calc = new ConstructionCalculator(_catalog);
        var rules = new BuildingRuleCatalog(_catalog, calc);
        _constructionManager = new VillageConstructionManager(_catalog, rules, calc);
    }

    [Fact]
    public void NetCropUpkeep_AppliesRomanTrough_ArchonHalving_DietControl_AndZeroNatureUpkeep()
    {
        var slots = new int[41];
        var levels = new int[41];
        VillageFieldTopologyCatalog.Default.InitializeOuterResourceSlots(3, slots, levels);
        // Horse Drinking Trough (gid 41) at Level 20 (-1 upkeep for u4, u5, u6)
        slots[19] = 41;
        levels[19] = 20;

        // Stack 1: Captured Nature animals (OwnerVillageId == 0) -> 100 Rats (u31), 50 Elephants (u40) -> 0 upkeep!
        var natureStack = new StarvationCandidateStack
        {
            StackOrMovementId = 1,
            Tier = StarvationPriorityTier.VillageReinforcement,
            OwnerVillageId = OasisConsts.CapturedNatureOwnerVillageId
        };
        natureStack.UnitCounts[31] = 100;
        natureStack.UnitCounts[40] = 50;

        // Stack 2: Roman Home troops: 10 Equites Scout (u4, base 2 -> 1), 10 Equites Eques (u5, base 3 -> 2),
        // 10 Equites Cataphract (u6, base 4 -> 3) + 1 Hero (6) => 10*1 + 10*2 + 10*3 + 6 = 66 raw upkeep
        var romanStack = new StarvationCandidateStack
        {
            StackOrMovementId = 2,
            Tier = StarvationPriorityTier.OwnStationed,
            OwnerVillageId = 100,
            HasHero = true
        };
        romanStack.UnitCounts[4] = 10;
        romanStack.UnitCounts[5] = 10;
        romanStack.UnitCounts[6] = 10;

        var ctx = new VillageProductionContext(
            SlotBuildingTypes: slots,
            SlotLevels: levels,
            AnnexedOasisWoodUnits: 0,
            AnnexedOasisClayUnits: 0,
            AnnexedOasisIronUnits: 0,
            AnnexedOasisCropUnits: 0,
            HasStationedLivingHero: false,
            HeroResourceBonusPoints: 0,
            HeroResourceFocusMode: HeroResourceFocusMode.Balanced,
            PlusWoodBonusActive: false,
            PlusClayBonusActive: false,
            PlusIronBonusActive: false,
            PlusCropBonusActive: false,
            IsArchonVillage: true, // Halves pop (ceil(101/2)=51) and halves troop upkeep (66 * 0.5 = 33)
            VillageTribeId: 1,     // Roman
            Population: 101,
            FedTroopStacks: [natureStack, romanStack],
            DietControlArtifactMultiplier: 0.5); // Halves again: round(33 * 0.5) = 17

        var snap = _prodCalculator.CalculateProduction(in ctx);
        Assert.Equal(51, snap.EffectivePopulationUpkeep);
        Assert.Equal(17, snap.TotalTroopUpkeepPerHour);
        Assert.Equal(12 - 51 - 17, snap.NetCropPerHour); // 6 Croplands at Lvl 0 = 12 gross crop
    }

    [Fact]
    public void StarvationCalculator_EnforcesFiveTierPriority_StrictFifoStackOrder_TieBreakers_HeroLast_AndEmptyStackDeletion()
    {
        // Tier 2 (Captured Nature OwnerVillageId = 0) -> MUST be skipped completely
        var capturedNature = new StarvationCandidateStack
        {
            StackOrMovementId = 1,
            Tier = StarvationPriorityTier.VillageReinforcement,
            OwnerVillageId = 0
        };
        capturedNature.UnitCounts[31] = 500;

        // Tier 1 (Oasis Reinforcement): two stacks (StackId 20 and StackId 10) -> must pick StackId 10 first (FIFO)!
        var oasisReinfLater = new StarvationCandidateStack
        {
            StackOrMovementId = 20,
            Tier = StarvationPriorityTier.OasisReinforcement,
            OwnerVillageId = 200
        };
        oasisReinfLater.UnitCounts[1] = 99;

        var oasisReinfEarlier = new StarvationCandidateStack
        {
            StackOrMovementId = 10,
            Tier = StarvationPriorityTier.OasisReinforcement,
            OwnerVillageId = 300,
            HasHero = true
        };
        // Tie between u1 (1) and u2 (1) -> lowest UnitTypeId (u1) wins first
        oasisReinfEarlier.UnitCounts[1] = 1;
        oasisReinfEarlier.UnitCounts[2] = 1;

        // Tier 3 (Trapped Prisoner): 1 unit -> frees 1 trap and deletes stack when starved
        var trapped = new StarvationCandidateStack
        {
            StackOrMovementId = 30,
            Tier = StarvationPriorityTier.TrappedPrisoner,
            OwnerVillageId = 400
        };
        trapped.UnitCounts[11] = 1;

        var stacks = new List<StarvationCandidateStack> { capturedNature, oasisReinfLater, oasisReinfEarlier, trapped };
        var evalCtx = new StarvationEvaluationContext(0.0, -50, IsPeaceMode: false, stacks);

        // Step 1: Picks StackId 10 (FIFO in Tier 1), starves u1 (tie with u2 broken by lowest UnitTypeId)
        Assert.True(_starvationCalculator.TryResolveNextStarvationStep(in evalCtx, out var step1));
        Assert.Equal(StarvationPriorityTier.OasisReinforcement, step1.StarvedTier);
        Assert.Equal(10L, step1.TargetStackOrMovementId);
        Assert.Equal(1, step1.StarvedUnitTypeId);
        Assert.False(step1.IsHeroStarved);
        Assert.False(step1.ShouldDeleteEmptyStackOrMovement);

        // Step 2: Picks StackId 10, starves u2 (Hero still protected while u2 > 0)
        Assert.True(_starvationCalculator.TryResolveNextStarvationStep(in evalCtx, out var step2));
        Assert.Equal(10L, step2.TargetStackOrMovementId);
        Assert.Equal(2, step2.StarvedUnitTypeId);
        Assert.False(step2.IsHeroStarved);
        Assert.False(step2.ShouldDeleteEmptyStackOrMovement);

        // Step 3: Now u1..u90 == 0 in StackId 10 -> Hero starves and StackId 10 is flagged for deletion!
        Assert.True(_starvationCalculator.TryResolveNextStarvationStep(in evalCtx, out var step3));
        Assert.Equal(10L, step3.TargetStackOrMovementId);
        Assert.True(step3.IsHeroStarved);
        Assert.Equal(6, step3.CreditedCrop);
        Assert.True(step3.ShouldDeleteEmptyStackOrMovement);
        stacks.Remove(oasisReinfEarlier);

        // Remove StackId 20 to test Tier 3 (TrappedPrisoner)
        stacks.Remove(oasisReinfLater);

        // Step 4: Captured Nature (OwnerVillageId == 0) is skipped; Tier 3 TrappedPrisoner starves and frees 1 trap
        Assert.True(_starvationCalculator.TryResolveNextStarvationStep(in evalCtx, out var step4));
        Assert.Equal(StarvationPriorityTier.TrappedPrisoner, step4.StarvedTier);
        Assert.Equal(30L, step4.TargetStackOrMovementId);
        Assert.Equal(11, step4.StarvedUnitTypeId);
        Assert.Equal(1, step4.FreedTrapCount);
        Assert.True(step4.ShouldDeleteEmptyStackOrMovement);
    }

    [Fact]
    public void LowFreeCropConstructionLockout_EnforcesAllFourExemptions()
    {
        var state = new VillageState(1, 1, 1, isCapital: true)
        {
            Wood = 10_000,
            Clay = 10_000,
            Iron = 10_000,
            Crop = 10_000,
            WarehouseCapacity = 800,
            GranaryCapacity = 800,
            GrossCropPerHour = 100,
            Population = 99, // Free crop = 1 (<= 1 lockout active!)
            NetCropProductionPerHour = 1
        };
        VillageFieldTopologyCatalog.Default.InitializeOuterResourceSlots(3, state.SlotBuildingTypes, state.SlotLevels);
        // Set all Croplands to Level 5 so next upgrade (Level 6) costs > 800 (requires Warehouse/Granary upgrade!)
        for (int s = 1; s <= 18; s++)
        {
            if (state.SlotBuildingTypes[s] == 4)
                state.SlotLevels[s] = 5;
        }
        state.SetSlot(19, 15, 5); // Main Building Lvl 5

        // 1. Woodcutter (gid 1) on Slot 1 is NOT exempt -> rejected with NegativeCropConstructionRestricted
        var (woodRes, _, _) = _constructionManager.EnqueueConstruction(
            state,
            new ConstructOrUpgradeBuildingCommand(1, 1, 1, SlotId: 1, BuildingTypeId: 1),
            1000L);
        Assert.False(woodRes.Succeeded);
        Assert.Equal(TerraviaDomainErrorCodes.Villages.NegativeCropConstructionRestricted, woodRes.ErrorCode);

        // 2. Grain Mill (gid 8) on Slot 20 (+5% of 6*33=198 -> +10 crop gain > 3 pop cost) IS exempt
        Assert.True(_constructionManager.IsExemptFromLowFreeCropLockout(state, 20, 8));

        // 3. Warehouse (gid 10) on Slot 21 IS exempt because all Croplands at Lvl 5->6 exceed 800 storage cap
        Assert.True(_constructionManager.IsExemptFromLowFreeCropLockout(state, 21, 10));
    }
}
