using Terravia.Domain;
using Terravia.Domain.Catalog;
using Terravia.Domain.Villages;
using Xunit;

namespace Terravia.Domain.Tests.Villages;

public sealed class VillageExpansionAndConquestCalculatorTests
{
    private readonly VillageExpansionManager _expansionManager = new();
    private readonly SiegeAndConquestCalculator _conquestCalculator = new(FrozenGameCatalog.Default);

    [Fact]
    public void CulturePointProgressionCurves_AreStrictlyMonotonicAcross1To125_AndMatchCanonicalAnchors()
    {
        for (int n = 2; n <= 125; n++)
        {
            int prevStd = _expansionManager.GetRequiredCulturePointsForVillageNumber(n - 1, CulturePointCurveMode.Standard);
            int currStd = _expansionManager.GetRequiredCulturePointsForVillageNumber(n, CulturePointCurveMode.Standard);
            Assert.True(currStd > prevStd, $"Standard curve not strictly monotonic at N={n}: {prevStd} -> {currStd}");

            int prevFast = _expansionManager.GetRequiredCulturePointsForVillageNumber(n - 1, CulturePointCurveMode.Fast);
            int currFast = _expansionManager.GetRequiredCulturePointsForVillageNumber(n, CulturePointCurveMode.Fast);
            Assert.True(currFast > prevFast, $"Fast curve not strictly monotonic at N={n}: {prevFast} -> {currFast}");
        }

        Assert.Equal(2_000, _expansionManager.GetRequiredCulturePointsForVillageNumber(2, CulturePointCurveMode.Standard));
        Assert.Equal(500, _expansionManager.GetRequiredCulturePointsForVillageNumber(2, CulturePointCurveMode.Fast));
        Assert.Equal(251_000, _expansionManager.GetRequiredCulturePointsForVillageNumber(10, CulturePointCurveMode.Standard));
        Assert.Equal(83_500, _expansionManager.GetRequiredCulturePointsForVillageNumber(10, CulturePointCurveMode.Fast));
        Assert.Equal(2_391_000, _expansionManager.GetRequiredCulturePointsForVillageNumber(25, CulturePointCurveMode.Standard));
        Assert.Equal(797_000, _expansionManager.GetRequiredCulturePointsForVillageNumber(25, CulturePointCurveMode.Fast));
        Assert.Equal(1_544_700, _expansionManager.GetRequiredCulturePointsForVillageNumber(33, CulturePointCurveMode.Fast));
        Assert.Equal(104_470_000, _expansionManager.GetRequiredCulturePointsForVillageNumber(125, CulturePointCurveMode.Standard));
        Assert.Equal(34_823_200, _expansionManager.GetRequiredCulturePointsForVillageNumber(125, CulturePointCurveMode.Fast));
    }

    [Fact]
    public void EvaluateExpansionTrainingQuota_EnforcesResidenceAndPalaceAndCommandCenterSlots()
    {
        // Palace Level 15 unlocks 2 slots (6 settler-equivalents)
        // 1 occupied slot (3 equivalents) + 1 existing Settler (1 equivalent) => 2 remaining trainable Settlers (0 Admins)
        var quota = _expansionManager.EvaluateExpansionTrainingQuota(
            new ExpansionQuotaContext(
                ResidenceLevel: 0,
                PalaceLevel: 15,
                CommandCenterLevel: 0,
                OccupiedExpansionSlotsCount: 1,
                ExistingOrQueuedSettlersCount: 1,
                ExistingOrQueuedAdministratorsCount: 0));

        Assert.Equal(2, quota.UnlockedExpansionSlots);
        Assert.Equal(6, quota.TotalCapacityInSettlerEquivalents);
        Assert.Equal(4, quota.ConsumedInSettlerEquivalents);
        Assert.Equal(2, quota.RemainingTrainableSettlers);
        Assert.Equal(0, quota.RemainingTrainableAdministrators);
    }

    [Fact]
    public void BuildCapitalTransferPlan_RejectsArchon_DowngradesResourceFieldsAbove10_AndDestroysMutuallyExclusiveBuildings()
    {
        var oldTypes = new int[41];
        var oldLevels = new int[41];
        oldTypes[1] = 1; oldLevels[1] = 15; // Woodcutter Lvl 15 -> downgraded to 10
        oldTypes[20] = 34; oldLevels[20] = 10; // Stonemason's Lodge (gid 34) -> destroyed
        oldTypes[21] = 35; oldLevels[21] = 5;  // Brewery (gid 35) -> destroyed

        var oldCap = new VillageCapitalState(
            VillageId: 1,
            IsCapital: true,
            IsArchonVillage: false,
            SlotBuildingTypes: oldTypes,
            SlotLevels: oldLevels);

        var newTypes = new int[41];
        var newLevels = new int[41];
        newTypes[25] = 26; newLevels[25] = 1;  // Palace Lvl 1 in new Capital
        newTypes[26] = 29; newLevels[26] = 10; // Great Barracks (gid 29) -> destroyed
        newTypes[27] = 38; newLevels[27] = 20; // Great Warehouse (gid 38) -> destroyed
        newTypes[28] = 49; newLevels[28] = 5;  // Great Workshop (gid 49) -> destroyed

        var newCap = new VillageCapitalState(
            VillageId: 2,
            IsCapital: false,
            IsArchonVillage: false,
            SlotBuildingTypes: newTypes,
            SlotLevels: newLevels);

        var plan = _expansionManager.BuildCapitalTransferPlan(oldCap, newCap);
        Assert.True(plan.Succeeded);
        Assert.Contains(1, plan.OldCapitalDowngradedOuterSlotsToLevel10);
        Assert.Contains(20, plan.OldCapitalDestroyedBuildingSlots); // Stonemason
        Assert.Contains(21, plan.OldCapitalDestroyedBuildingSlots); // Brewery
        Assert.Contains(26, plan.NewCapitalDestroyedBuildingSlots); // Great Barracks
        Assert.Contains(27, plan.NewCapitalDestroyedBuildingSlots); // Great Warehouse
        Assert.Contains(28, plan.NewCapitalDestroyedBuildingSlots); // Great Workshop

        // Archon village cannot become Capital
        var archonCandidate = newCap with { IsArchonVillage = true };
        var archonPlan = _expansionManager.BuildCapitalTransferPlan(oldCap, archonCandidate);
        Assert.False(archonPlan.Succeeded);
        Assert.Equal(TerraviaDomainErrorCodes.Villages.CannotMakeWorldWonderVillageCapital, archonPlan.ErrorCode);
    }

    [Fact]
    public void SiegeAndConquestCalculator_EnforcesMeadFestivalPersuasionPenalty_PreConquestPopPenalty_AndTribeCleanup()
    {
        var targetVillage = new VillageState(villageId: 50, ownerPlayerId: 2, tribeId: 3, isCapital: false)
        {
            Loyalty = 10.0,
            Population = 100
        };
        targetVillage.SetSlot(1, 1, 5);    // Woodcutter Lvl 5 -> Lvl 4 (AttackerTotalPop > DefenderTotalPop)
        targetVillage.SetSlot(19, 15, 1);  // Main Building Lvl 1 -> stays 1
        targetVillage.SetSlot(20, 36, 5);  // Trapper (Gallic gid 36) -> destroyed on conquest
        targetVillage.SetSlot(40, 33, 10); // Palisade (Wall on Slot 40) -> destroyed on conquest
        targetVillage.AnnexedOasisTileIds.Add(501);
        targetVillage.AnnexedOasisTileIds.Add(502);

        var ctx = new ConquestEvaluationContext(
            AttackerPlayerId: 1,
            AttackerTribeId: 1, // Roman conquering Gaul
            AttackerTotalAccountPopulation: 1000, // > Defender (500) => -1 level penalty applies!
            AttackerLaunchingVillageFreeExpansionSlots: 1,
            AttackerAccumulatedCulturePoints: 5000,
            RequiredCulturePointsForNextVillage: 2000,
            AttackerVillageHasActiveGreatCelebration: true,
            AttackerHasActiveTeutonMeadFestival: true,
            SurvivingAdministratorUnitTypeId: 9, // Roman Senator (20..30)
            SurvivingAdministratorCount: 1,
            DefenderPlayerId: 2,
            DefenderTribeId: 3,
            DefenderTotalAccountPopulation: 500,
            DefenderOwnedVillageCount: 2,
            TargetIsCapital: false,
            TargetIsArchonVillage: false,
            TargetIsWwBuildingPlanVillage: false,
            TargetHasStandingExpansionBuilding: false,
            DefenderVillageHasActiveGreatCelebration: false,
            CurrentTargetLoyalty: 10.0,
            MoraleBonus: 1.0);

        var res = _conquestCalculator.EvaluateAdministratorConquest(in ctx, (_, _) => 25);

        Assert.True(res.CanAttemptPersuasion);
        Assert.True(res.WasVillageConquered);
        Assert.True(res.ApplyBiggerPlayerMinusOneBuildingLevelPenalty);
        // (25 + 5) * 0.5 (Mead Festival) = 15.0 persuasion => 10.0 - 15.0 <= 0.0
        Assert.Equal(15.0, res.TotalLoyaltyReduction, precision: 3);
        Assert.Equal(0.0, res.ResultingLoyalty, precision: 3);

        var releasedOases = _conquestCalculator.ApplyVillageConquestStateReset(
            targetVillage,
            newOwnerPlayerId: 1,
            newOwnerTribeId: 1,
            applyBiggerPlayerMinusOnePenalty: res.ApplyBiggerPlayerMinusOneBuildingLevelPenalty);

        Assert.Equal(4, targetVillage.SlotLevels[1]); // Woodcutter 5 -> 4
        Assert.Equal(1, targetVillage.SlotLevels[19]); // Main Building stays >= 1
        Assert.Equal(0, targetVillage.SlotBuildingTypes[20]); // Gallic Trapper destroyed
        Assert.Equal(0, targetVillage.SlotBuildingTypes[40]); // Wall destroyed
        Assert.Equal(2, releasedOases.Count);
    }
}
