using Terravia.Domain.Catalog;
using Terravia.Domain.Villages;
using Xunit;

namespace Terravia.Domain.Tests.Villages;

public sealed class TechnologyResearchManagerTests
{
    private readonly IGameCatalog _catalog = FrozenGameCatalog.Default;
    private readonly TechnologyResearchManager _manager = new();

    [Fact]
    public void StartAcademyResearch_EnforcesPrerequisites_SingleProjectLimit_AndUnlocksUnitOnCompletion()
    {
        var state = CreateRichVillageState(tribeId: 1);

        // Slot 1 (Legionnaire) and Slot 10 (Settler) are unlocked by default
        Assert.True(state.UnlockedUnitSlots[1]);
        Assert.True(state.UnlockedUnitSlots[10]);
        Assert.False(state.UnlockedUnitSlots[2]);

        // Without Academy (gid 22) and Armoury (gid 13), Praetorian (u2) fails prerequisites
        var failNoReq = _manager.StartAcademyResearch(state, new StartAcademyResearchCommand(state.VillageId, 2), 1_000L);
        Assert.False(failNoReq.Succeeded);
        Assert.Equal(TerraviaDomainErrorCodes.Villages.ResearchPrerequisitesNotMet, failNoReq.ErrorCode);

        // Build Academy L5 and Armoury L1, Blacksmith L1
        state.SetSlot(22, 22, 5);
        state.SetSlot(23, 13, 1);
        state.SetSlot(24, 12, 1);

        var r1 = _manager.StartAcademyResearch(state, new StartAcademyResearchCommand(state.VillageId, 2), 1_000L);
        Assert.True(r1.Succeeded);
        Assert.Equal(1, r1.TargetLevel);

        // Attempting a second Academy research (Imperian u3) while u2 is active is rejected even with Plus
        state.HasPlusSubscription = true;
        var r2 = _manager.StartAcademyResearch(state, new StartAcademyResearchCommand(state.VillageId, 3), 2_000L);
        Assert.False(r2.Succeeded);
        Assert.Equal(TerraviaDomainErrorCodes.Villages.AcademyResearchAlreadyActive, r2.ErrorCode);

        // Demolish Academy to 0 while research is active -> active paid research still completes!
        state.SetSlot(22, 0, 0);
        int completed = _manager.CompleteDueResearchItems(state, r1.FinishesAtMs);
        Assert.Equal(1, completed);
        Assert.True(state.UnlockedUnitSlots[2]);
    }

    [Fact]
    public void StartSmithyUpgrade_SupportsConcurrentBlacksmithAndArmoury_Plus2ndSlotWith60sDelay_AndSnapshotAtDispatch()
    {
        var state = CreateRichVillageState(tribeId: 1);
        state.HasPlusSubscription = true;
        state.SetSlot(20, 12, 2); // Blacksmith L2
        state.SetSlot(21, 13, 2); // Armoury L2

        // Station Home stack and an already-dispatched Reinforcement stack
        var homeStack = new StarvationCandidateStack
        {
            StackOrMovementId = 1,
            Tier = StarvationPriorityTier.OwnStationed,
            OwnerVillageId = state.VillageId
        };
        var dispatchedReinforcementStack = new StarvationCandidateStack
        {
            StackOrMovementId = 2,
            Tier = StarvationPriorityTier.VillageReinforcement,
            OwnerVillageId = state.VillageId
        };
        state.TroopStacks.Add(homeStack);
        state.TroopStacks.Add(dispatchedReinforcementStack);

        long nowMs = 10_000L;

        // Queue Level 1 offense upgrade (b1) and Level 2 Plus waiting-loop offense upgrade (b1) in Blacksmith
        var bL1 = _manager.StartSmithyUpgrade(state, new StartSmithyUpgradeCommand(state.VillageId, ResearchCategory.Blacksmith, 1), nowMs);
        Assert.True(bL1.Succeeded);
        Assert.Equal(1, bL1.TargetLevel);
        Assert.False(bL1.IsPlusWaitingLoop);

        var bL2 = _manager.StartSmithyUpgrade(state, new StartSmithyUpgradeCommand(state.VillageId, ResearchCategory.Blacksmith, 1), nowMs);
        Assert.True(bL2.Succeeded);
        Assert.Equal(2, bL2.TargetLevel);
        Assert.True(bL2.IsPlusWaitingLoop);

        // Verify Plus 2nd slot starts at EndAt_1 + ceil(60 / Speed)s
        var calc = new TrainingTimeCalculator(_catalog);
        int expectedL2Duration = calc.CalculateSmithyUpgradeDurationSeconds(1, 2, 2, state.ServerSpeed);
        Assert.Equal(bL1.FinishesAtMs + (60 + expectedL2Duration) * 1000L, bL2.FinishesAtMs);

        // Queue Level 3 exceeds Blacksmith L2 cap
        _manager.CompleteDueResearchItems(state, bL1.FinishesAtMs);
        Assert.Equal(1, state.OffenseUpgradeLevels[1]);

        // Snapshot at Dispatch Rule: Home stack gets updated to L1, while already-dispatched reinforcement stack stays at 0
        Assert.Equal(1, homeStack.OffenseUpgradeLevels[1]);
        Assert.Equal(0, dispatchedReinforcementStack.OffenseUpgradeLevels[1]);

        // Concurrent Armoury upgrade (a1) runs in parallel
        var aL1 = _manager.StartSmithyUpgrade(state, new StartSmithyUpgradeCommand(state.VillageId, ResearchCategory.Armoury, 1), nowMs);
        Assert.True(aL1.Succeeded);
        Assert.Equal(ResearchCategory.Armoury, aL1.Category);
    }

    [Fact]
    public void FinishAllResearchWithGold_CompletesActiveAcademyAndSmithyProjectsFor2Gold_AndEnforcesSitterPermission()
    {
        var state = CreateRichVillageState(tribeId: 1);
        state.PlayerGoldBalance = 10;
        state.SetSlot(20, 22, 5);
        state.SetSlot(21, 12, 5);
        state.SetSlot(22, 13, 5);

        _manager.StartAcademyResearch(state, new StartAcademyResearchCommand(state.VillageId, 2), 1_000L);
        _manager.StartSmithyUpgrade(state, new StartSmithyUpgradeCommand(state.VillageId, ResearchCategory.Blacksmith, 1), 1_000L);

        // Sitter without BuyAndSpendGold is denied
        var denied = _manager.FinishAllResearchWithGold(state, new FinishAllResearchWithGoldCommand(state.VillageId), 2_000L, SitterPermissions.BuildAndDemolish);
        Assert.False(denied.Result.Succeeded);
        Assert.Equal(TerraviaDomainErrorCodes.Sitters.GoldPermissionDenied, denied.Result.ErrorCode);

        var ok = _manager.FinishAllResearchWithGold(state, new FinishAllResearchWithGoldCommand(state.VillageId), 2_000L, SitterPermissions.BuyAndSpendGold);
        Assert.True(ok.Result.Succeeded);
        Assert.Equal(2, ok.GoldDeducted);
        Assert.Equal(2, ok.CompletedCount);
        Assert.Equal(8, state.PlayerGoldBalance);
        Assert.True(state.UnlockedUnitSlots[2]);
        Assert.Equal(1, state.OffenseUpgradeLevels[1]);
        Assert.Empty(state.ResearchQueue);
    }

    private static VillageState CreateRichVillageState(int tribeId)
    {
        var state = new VillageState(1, 100, tribeId, isCapital: true)
        {
            WarehouseCapacity = 500_000,
            GranaryCapacity = 500_000,
            Wood = 200_000,
            Clay = 200_000,
            Iron = 200_000,
            Crop = 200_000,
            ServerSpeed = 1.0
        };
        state.SyncExactResourcesFromInteger();
        return state;
    }
}
