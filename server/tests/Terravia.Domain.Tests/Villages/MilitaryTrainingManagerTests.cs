using Terravia.Domain.Catalog;
using Terravia.Domain.Villages;
using Xunit;

namespace Terravia.Domain.Tests.Villages;

public sealed class MilitaryTrainingManagerTests
{
    private readonly IGameCatalog _catalog = FrozenGameCatalog.Default;
    private readonly MilitaryTrainingManager _manager = new();

    [Fact]
    public void EnqueueTrainingBatch_ConcurrentQueuesAcrossBarracksStableAndWorkshop_RunInParallel()
    {
        var state = CreatePopulatedVillageState(tribeId: 1, isCapital: false);
        state.SetSlot(19, 19, 10); // Barracks L10
        state.SetSlot(20, 20, 10); // Stable L10
        state.SetSlot(21, 21, 10); // Workshop L10
        state.UnlockedUnitSlots[5] = true; // Equites Imperatoris
        state.UnlockedUnitSlots[7] = true; // Battering Ram

        long nowMs = 1_000_000L;

        var r1 = _manager.EnqueueTrainingBatch(state, new EnqueueMilitaryTrainingCommand(state.VillageId, TrainingFacilityCategory.Barracks, 1, 5), nowMs);
        var r2 = _manager.EnqueueTrainingBatch(state, new EnqueueMilitaryTrainingCommand(state.VillageId, TrainingFacilityCategory.Stable, 5, 5), nowMs);
        var r3 = _manager.EnqueueTrainingBatch(state, new EnqueueMilitaryTrainingCommand(state.VillageId, TrainingFacilityCategory.Workshop, 7, 2), nowMs);

        Assert.True(r1.Succeeded);
        Assert.True(r2.Succeeded);
        Assert.True(r3.Succeeded);
        Assert.Equal(3, state.TrainingQueues.Count);

        // All 3 queues start immediately at nowMs in parallel
        Assert.All(state.TrainingQueues, q => Assert.Equal(nowMs, q.CommenceAtMs));
    }

    [Fact]
    public void EnqueueTrainingBatch_GreatBarracks_Deducts3xResourceCost_AndBlocksInCapital()
    {
        var capital = CreatePopulatedVillageState(tribeId: 1, isCapital: true);
        capital.SetSlot(19, 19, 20);
        capital.SetSlot(20, 29, 10);

        var capResult = _manager.EnqueueTrainingBatch(
            capital,
            new EnqueueMilitaryTrainingCommand(capital.VillageId, TrainingFacilityCategory.GreatBarracks, 1, 5),
            1_000L);
        Assert.False(capResult.Succeeded);
        Assert.Equal(TerraviaDomainErrorCodes.Villages.GreatMilitaryForbiddenInCapital, capResult.ErrorCode);

        var nonCapital = CreatePopulatedVillageState(tribeId: 1, isCapital: false);
        nonCapital.SetSlot(19, 19, 20);
        nonCapital.SetSlot(20, 29, 10);

        var unit1 = _catalog.GetUnit(1); // Legionnaire
        var nonCapResult = _manager.EnqueueTrainingBatch(
            nonCapital,
            new EnqueueMilitaryTrainingCommand(nonCapital.VillageId, TrainingFacilityCategory.GreatBarracks, 1, 4),
            1_000L);

        Assert.True(nonCapResult.Succeeded);
        Assert.Equal(4, nonCapResult.EnrolledAmount);
        Assert.Equal(unit1.Cost.Wood * 3 * 4, nonCapResult.DeductedCost.Wood);
        Assert.Equal(unit1.Cost.Clay * 3 * 4, nonCapResult.DeductedCost.Clay);
        Assert.Equal(unit1.Cost.Iron * 3 * 4, nonCapResult.DeductedCost.Iron);
        Assert.Equal(unit1.Cost.Crop * 3 * 4, nonCapResult.DeductedCost.Crop);
    }

    [Fact]
    public void CompleteDueTrainingGraduations_GraduatesUnitsSequentiallyOneByOne_AndMergesTrailingBatches()
    {
        var state = CreatePopulatedVillageState(tribeId: 1, isCapital: false);
        state.SetSlot(19, 19, 1); // Barracks L1 -> attri = 100%

        long t0 = 10_000L;
        var r1 = _manager.EnqueueTrainingBatch(state, new EnqueueMilitaryTrainingCommand(state.VillageId, TrainingFacilityCategory.Barracks, 1, 5), t0);
        Assert.True(r1.Succeeded);
        Assert.False(r1.WasMergedIntoTrailingBatch);

        // Submit second batch of same unit & same speed -> merges into trailing entry
        var r2 = _manager.EnqueueTrainingBatch(state, new EnqueueMilitaryTrainingCommand(state.VillageId, TrainingFacilityCategory.Barracks, 1, 5), t0 + 1_000L);
        Assert.True(r2.Succeeded);
        Assert.True(r2.WasMergedIntoTrailingBatch);
        Assert.Single(state.TrainingQueues);
        Assert.Equal(10, state.TrainingQueues[0].AmountRemaining);

        int eachTime = r1.EachTimeSeconds;
        long afterTwoAndHalfUnitsMs = t0 + (long)(eachTime * 2.5 * 1000);

        int graduated = _manager.CompleteDueTrainingGraduations(state, afterTwoAndHalfUnitsMs);
        Assert.Equal(2, graduated);
        Assert.Equal(8, state.TrainingQueues[0].AmountRemaining);
        Assert.Equal(t0 + 2L * eachTime * 1000L, state.TrainingQueues[0].CommenceAtMs);

        var homeStack = Assert.Single(state.TroopStacks);
        Assert.Equal(2, homeStack.UnitCounts[1]);
    }

    [Fact]
    public void EnqueueTrainingBatch_ExpansionSlotGating_EnforcesResidencePalaceAndCommandCenterQuotas_AndBlocksSimultaneousChiefAndSettler()
    {
        // Hun village with Command Center (gid 44) at Level 15 -> 2 unlocked slots
        var hunState = CreatePopulatedVillageState(tribeId: 6, isCapital: false);
        hunState.SetSlot(25, 44, 15);
        hunState.UnlockedUnitSlots[9] = true; // Logades (u59)
        hunState.ExpansionSlot1VillageId = 999; // 1 slot already used by daughter village -> 1 slot left

        // Simultaneous Chief (u59) + Settler (u60) in one request is rejected
        var simResult = _manager.EnqueueTrainingBatch(
            hunState,
            new EnqueueMilitaryTrainingCommand(hunState.VillageId, TrainingFacilityCategory.ResidencePalace, 59, 1, SecondExpansionUnitTypeId: 60, SecondExpansionRequestedAmount: 1),
            1_000L);
        Assert.False(simResult.Succeeded);
        Assert.Equal(TerraviaDomainErrorCodes.Villages.SimultaneousChiefAndSettlerForbidden, simResult.ErrorCode);

        // Queue 1 Settler (u60) -> leaves 2 settler slots and 0 chief slots (since 1 settler occupies the remaining slot against a Chief)
        var s1 = _manager.EnqueueTrainingBatch(
            hunState,
            new EnqueueMilitaryTrainingCommand(hunState.VillageId, TrainingFacilityCategory.ResidencePalace, 60, 1),
            1_000L);
        Assert.True(s1.Succeeded);
        Assert.Equal(1, s1.EnrolledAmount);

        var chiefAttempt = _manager.EnqueueTrainingBatch(
            hunState,
            new EnqueueMilitaryTrainingCommand(hunState.VillageId, TrainingFacilityCategory.ResidencePalace, 59, 1),
            1_000L);
        Assert.False(chiefAttempt.Succeeded);
        Assert.Equal(TerraviaDomainErrorCodes.Villages.ExpansionUnitQuotaExceeded, chiefAttempt.ErrorCode);

        // Requesting 5 more Settlers clamps to remaining 2 Settlers
        var s2 = _manager.EnqueueTrainingBatch(
            hunState,
            new EnqueueMilitaryTrainingCommand(hunState.VillageId, TrainingFacilityCategory.ResidencePalace, 60, 5),
            1_000L);
        Assert.True(s2.Succeeded);
        Assert.Equal(2, s2.EnrolledAmount);
    }

    [Fact]
    public void EnqueueTrainingBatch_EnforcesGranaryGate_OptionalNegativeCropGate_AndSitterPermissions()
    {
        var state = CreatePopulatedVillageState(tribeId: 1, isCapital: true);
        state.SetSlot(19, 19, 5);
        state.NetCropProductionPerHour = -500; // Negative net crop (hammer village)

        // Sitter without BuildAndDemolish is rejected
        var sitterDenied = _manager.EnqueueTrainingBatch(
            state,
            new EnqueueMilitaryTrainingCommand(state.VillageId, TrainingFacilityCategory.Barracks, 1, 2),
            1_000L,
            SitterPermissions.Raid);
        Assert.False(sitterDenied.Succeeded);
        Assert.Equal(TerraviaDomainErrorCodes.Sitters.PermissionDenied, sitterDenied.ErrorCode);

        // By default (BlockTrainingOnNegativeCrop = false), negative net crop is allowed as long as Granary crop covers cost
        var allowedOnDefault = _manager.EnqueueTrainingBatch(
            state,
            new EnqueueMilitaryTrainingCommand(state.VillageId, TrainingFacilityCategory.Barracks, 1, 2),
            1_000L,
            SitterPermissions.BuildAndDemolish);
        Assert.True(allowedOnDefault.Succeeded);

        // When BlockTrainingOnNegativeCrop = true, negative net crop blocks training
        state.BlockTrainingOnNegativeCrop = true;
        var blockedOnStrict = _manager.EnqueueTrainingBatch(
            state,
            new EnqueueMilitaryTrainingCommand(state.VillageId, TrainingFacilityCategory.Barracks, 1, 2),
            2_000L);
        Assert.False(blockedOnStrict.Succeeded);
        Assert.Equal(TerraviaDomainErrorCodes.Villages.NegativeCropTrainingRestricted, blockedOnStrict.ErrorCode);
    }

    private static VillageState CreatePopulatedVillageState(int tribeId, bool isCapital)
    {
        var state = new VillageState(1, 100, tribeId, isCapital)
        {
            WarehouseCapacity = 500_000,
            GranaryCapacity = 500_000,
            Wood = 200_000,
            Clay = 200_000,
            Iron = 200_000,
            Crop = 200_000,
            NetCropProductionPerHour = 1_000,
            ServerSpeed = 1.0
        };
        state.SyncExactResourcesFromInteger();
        return state;
    }
}
