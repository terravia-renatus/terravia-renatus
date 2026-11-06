using Terravia.Domain;
using Terravia.Domain.Catalog;
using Terravia.Domain.Villages;

namespace Terravia.Domain.Tests.Villages;

public sealed class VillageConstructionManagerTests
{
    private readonly IGameCatalog _catalog = FrozenGameCatalog.Default;
    private readonly IConstructionCalculator _calculator;
    private readonly IBuildingRuleCatalog _ruleCatalog;
    private readonly VillageConstructionManager _manager;

    public VillageConstructionManagerTests()
    {
        _calculator = new ConstructionCalculator(_catalog);
        _ruleCatalog = new BuildingRuleCatalog(_catalog, _calculator);
        _manager = new VillageConstructionManager(_catalog, _ruleCatalog, _calculator);
    }

    private static VillageState CreateSampleVillage(int tribeId = 1, bool hasPlus = false)
    {
        var state = new VillageState(villageId: 100, ownerPlayerId: 1, tribeId: tribeId, isCapital: true)
        {
            HasPlusSubscription = hasPlus,
            Wood = 10_000,
            Clay = 10_000,
            Iron = 10_000,
            Crop = 10_000,
            WarehouseCapacity = 80_000,
            GranaryCapacity = 80_000,
            NetCropProductionPerHour = 200,
            PlayerGoldBalance = 50
        };
        // Main Building (15) at Level 5 on Slot 19, Rally Point (16) at Level 1 on Slot 39
        state.SetSlot(19, 15, 5);
        state.SetSlot(39, 16, 1);
        return state;
    }

    [Fact]
    public void RomanDualQueue_ShouldAllowConcurrentResourceFieldAndInnerBuilding_AndChainWaitingLoopByPartition()
    {
        var state = CreateSampleVillage(tribeId: 1, hasPlus: true);
        long nowMs = 1_000_000L;

        // 1. Start Woodcutter (gid 1) on Slot 1
        var (res1, job1, _) = _manager.EnqueueConstruction(
            state,
            new ConstructOrUpgradeBuildingCommand(1, 1, 100, SlotId: 1, BuildingTypeId: 1),
            nowMs);
        Assert.True(res1.Succeeded);
        Assert.NotNull(job1);
        Assert.True(job1.IsActive);

        // 2. Start Barracks (gid 19) on Slot 20 concurrently (Roman dual-queue)
        var (res2, job2, _) = _manager.EnqueueConstruction(
            state,
            new ConstructOrUpgradeBuildingCommand(1, 1, 100, SlotId: 20, BuildingTypeId: 19),
            nowMs);
        Assert.True(res2.Succeeded);
        Assert.NotNull(job2);
        Assert.True(job2.IsActive);
        Assert.Equal(nowMs, job2.StartedAtMs);

        // 3. Queue a second Woodcutter upgrade on Slot 1 (TargetLevel = 2) -> enters Plus Waiting Loop
        var (res3, job3, _) = _manager.EnqueueConstruction(
            state,
            new ConstructOrUpgradeBuildingCommand(1, 1, 100, SlotId: 1, BuildingTypeId: 1),
            nowMs);
        Assert.True(res3.Succeeded);
        Assert.NotNull(job3);
        Assert.True(job3.IsWaitingLoop);
        Assert.Equal(2, job3.TargetLevel);
        // Chains after job1.FinishesAtMs + ceil(60 / 1.0) * 1000
        Assert.Equal(job1.FinishesAtMs + 60_000L, job3.StartedAtMs);
    }

    [Fact]
    public void NonRomanQueue_WithoutPlus_ShouldRejectSecondConcurrentBuild_UnlessMasterBuilderUsed()
    {
        var state = CreateSampleVillage(tribeId: 2, hasPlus: false); // Teutons, no Plus
        state.MaxMasterBuilderSlots = 0; // Disable automatic Master Builder fallback to test pure rejection
        long nowMs = 1_000_000L;

        var (res1, _, _) = _manager.EnqueueConstruction(
            state,
            new ConstructOrUpgradeBuildingCommand(1, 1, 100, SlotId: 1, BuildingTypeId: 1),
            nowMs);
        Assert.True(res1.Succeeded);

        var (res2, _, _) = _manager.EnqueueConstruction(
            state,
            new ConstructOrUpgradeBuildingCommand(1, 1, 100, SlotId: 20, BuildingTypeId: 19),
            nowMs);
        Assert.False(res2.Succeeded);
        Assert.Equal("QueueFull", res2.ErrorCode);
    }

    [Fact]
    public void MasterBuilderReservation_ShouldNotDeductResourcesOrGoldUntilPromoted_AndCancelWithZeroRefund()
    {
        var state = CreateSampleVillage(tribeId: 2, hasPlus: false);
        state.Wood = 50; // Insufficient for Barracks Level 1 (requires 210 Wood)
        long nowMs = 1_000_000L;

        var (res, mbJob, usedGold) = _manager.EnqueueConstruction(
            state,
            new ConstructOrUpgradeBuildingCommand(1, 1, 100, SlotId: 20, BuildingTypeId: 19),
            nowMs);

        Assert.True(res.Succeeded);
        Assert.False(usedGold);
        Assert.NotNull(mbJob);
        Assert.True(mbJob.IsMasterBuilder);
        Assert.Equal(50, state.Wood); // Not deducted yet
        Assert.Equal(50, state.PlayerGoldBalance); // Not deducted yet

        // Cancelling an unstarted Master Builder reservation refunds 0 resources and 0 Gold
        var cancelRes = _manager.CancelConstructionJob(state, new CancelConstructionJobCommand(1, 1, 100, mbJob.QueueItemId), nowMs);
        Assert.True(cancelRes.Succeeded);
        Assert.Equal(50, state.Wood);
        Assert.Equal(50, state.PlayerGoldBalance);
        Assert.Equal(0, state.GetSlotBuildingType(20)); // Slot reset to empty
    }

    [Fact]
    public void FinishAllWithGold_ShouldBlockWorldWonderVillages_AndSkipResidenceAndPalace()
    {
        // 1. World Wonder village completely blocks Finish All
        var wwVillage = CreateSampleVillage(tribeId: 1);
        wwVillage.IsWorldWonderVillage = true;
        wwVillage.WorldWonderBuildingType = 40;
        _manager.EnqueueConstruction(wwVillage, new ConstructOrUpgradeBuildingCommand(1, 1, 100, SlotId: 1, BuildingTypeId: 1), 1000L);

        var (wwResult, wwGold) = _manager.FinishAllWithGold(wwVillage, new FinishAllConstructionsWithGoldCommand(1, 1, 100), 2000L);
        Assert.False(wwResult.Succeeded);
        Assert.Equal(TerraviaDomainErrorCodes.Villages.WorldWonderVillageFinishAllForbidden, wwResult.ErrorCode);
        Assert.Equal(0, wwGold);

        // 2. Normal village with Residence (gid 25) and Woodcutter (gid 1) only finishes Woodcutter
        var romanVillage = CreateSampleVillage(tribeId: 1);
        _manager.EnqueueConstruction(romanVillage, new ConstructOrUpgradeBuildingCommand(1, 1, 100, SlotId: 1, BuildingTypeId: 1), 1000L);
        _manager.EnqueueConstruction(romanVillage, new ConstructOrUpgradeBuildingCommand(1, 1, 100, SlotId: 20, BuildingTypeId: 25), 1000L);

        var (finishRes, goldSpent) = _manager.FinishAllWithGold(romanVillage, new FinishAllConstructionsWithGoldCommand(1, 1, 100), 2000L);
        Assert.True(finishRes.Succeeded);
        Assert.Equal(2, goldSpent);
        Assert.Equal(1, romanVillage.GetSlotLevel(1)); // Woodcutter finished
        Assert.Equal(0, romanVillage.GetSlotLevel(20)); // Residence NOT finished with Gold
        Assert.Single(romanVillage.ConstructionQueue); // Residence remains in queue
    }

    [Fact]
    public void CancelActiveAndWaitingLoopJobs_ShouldRefundFullCostCappedAtStorageCapacity()
    {
        var state = CreateSampleVillage(tribeId: 2, hasPlus: true);
        state.WarehouseCapacity = 500;
        state.GranaryCapacity = 500;
        state.Wood = 400;
        state.Clay = 400;
        state.Iron = 400;
        state.Crop = 400;
        long nowMs = 10_000L;

        // Enqueue Woodcutter Level 1 on Slot 1 (cost: 40w, 100c, 50i, 60cr)
        var (res1, job1, _) = _manager.EnqueueConstruction(
            state,
            new ConstructOrUpgradeBuildingCommand(1, 1, 100, SlotId: 1, BuildingTypeId: 1),
            nowMs);
        Assert.True(res1.Succeeded);
        Assert.NotNull(job1);
        Assert.Equal(360, state.Wood);
        Assert.Equal(300, state.Clay);

        // Enqueue Woodcutter Level 2 on Slot 1 in Plus waiting loop (cost: 65w, 165c, 85i, 100cr)
        var (res2, job2, _) = _manager.EnqueueConstruction(
            state,
            new ConstructOrUpgradeBuildingCommand(1, 1, 100, SlotId: 1, BuildingTypeId: 1),
            nowMs);
        Assert.True(res2.Succeeded);
        Assert.NotNull(job2);
        Assert.True(job2.IsWaitingLoop);
        Assert.Equal(2, job2.TargetLevel);

        // Simulate incoming resources filling storage to 480 before cancelling job1
        state.Wood = 480;
        state.Clay = 480;
        state.Iron = 480;
        state.Crop = 480;

        // Cancel active job1 -> refunds 100% capped at 500, and promotes job2 from TargetLevel 2 -> 1 and WaitingLoop -> Active
        var cancel1 = _manager.CancelConstructionJob(state, new CancelConstructionJobCommand(1, 1, 100, job1.QueueItemId), nowMs + 5000L);
        Assert.True(cancel1.Succeeded);
        Assert.Equal(500, state.Wood);
        Assert.Equal(500, state.Clay);
        Assert.Equal(500, state.Iron);
        Assert.Equal(500, state.Crop);

        Assert.Single(state.ConstructionQueue);
        var promoted = state.ConstructionQueue[0];
        Assert.Equal(job2.QueueItemId, promoted.QueueItemId);
        Assert.Equal(1, promoted.TargetLevel);
        Assert.True(promoted.IsActive);
    }

    [Fact]
    public void RandomizedOneThousandPlusCommandSequences_ShouldMaintainDeterministicQueueAndResourceInvariants_SC003()
    {
        var rng = new Random(20261106);
        int totalCommandsExecuted = 0;

        for (int trial = 0; trial < 40; trial++)
        {
            int tribeId = (trial % 2 == 0) ? 1 : 2; // Alternate Romans (dual-queue) and Teutons (single-queue)
            bool hasPlus = (trial % 3 != 0);
            var state = CreateSampleVillage(tribeId: tribeId, hasPlus: hasPlus);
            state.SetSlot(19, 15, 10); // Main Building Level 10 so demolition is also eligible
            state.SetSlot(37, 10, 20); // Warehouse Level 20 (80,000 capacity)
            state.SetSlot(38, 11, 20); // Granary Level 20 (80,000 capacity)
            state.Wood = 50_000;
            state.Clay = 50_000;
            state.Iron = 50_000;
            state.Crop = 50_000;
            state.PlayerGoldBalance = 200;

            long nowMs = 1_000_000L;

            for (int step = 0; step < 30; step++)
            {
                totalCommandsExecuted++;
                int action = rng.Next(0, 6);

                switch (action)
                {
                    case 0: // Enqueue resource field (Slot 1..4 -> GID 1..4)
                    {
                        int slot = rng.Next(1, 5);
                        _manager.EnqueueConstruction(
                            state,
                            new ConstructOrUpgradeBuildingCommand(1, 1, 100, SlotId: slot, BuildingTypeId: slot),
                            nowMs);
                        break;
                    }
                    case 1: // Enqueue inner building (Slot 20 -> Barracks 19, or Slot 21 -> Warehouse 10, or Slot 22 -> Granary 11)
                    {
                        int choice = rng.Next(0, 3);
                        int slot = 20 + choice;
                        int gid = choice switch { 0 => 19, 1 => 10, _ => 11 };
                        bool useMb = rng.Next(0, 4) == 0;
                        _manager.EnqueueConstruction(
                            state,
                            new ConstructOrUpgradeBuildingCommand(1, 1, 100, SlotId: slot, BuildingTypeId: gid, UseMasterBuilder: useMb),
                            nowMs);
                        break;
                    }
                    case 2: // Advance virtual clock and complete due items
                    {
                        nowMs += rng.Next(30_000, 900_000);
                        _manager.CompleteDueConstructionsAndPromote(state, nowMs);
                        _manager.CompleteDueDemolition(state, nowMs);
                        break;
                    }
                    case 3: // Cancel a random queued construction job
                    {
                        if (state.ConstructionQueue.Count > 0)
                        {
                            var pick = state.ConstructionQueue[rng.Next(0, state.ConstructionQueue.Count)];
                            _manager.CancelConstructionJob(
                                state,
                                new CancelConstructionJobCommand(1, 1, 100, pick.QueueItemId),
                                nowMs);
                        }
                        break;
                    }
                    case 4: // Start or complete demolition on Slot 20..22
                    {
                        int slot = rng.Next(20, 23);
                        bool instant = rng.Next(0, 5) == 0;
                        _manager.StartDemolition(
                            state,
                            new StartBuildingDemolitionCommand(1, 1, 100, SlotId: slot, IsInstantCompleteWithGold: instant),
                            nowMs);
                        break;
                    }
                    case 5: // FinishAllWithGold
                    {
                        _manager.FinishAllWithGold(
                            state,
                            new FinishAllConstructionsWithGoldCommand(1, 1, 100),
                            nowMs);
                        break;
                    }
                }

                // Invariant 1: Resources & Gold are never negative and never exceed storage capacity
                Assert.InRange(state.Wood, 0, state.WarehouseCapacity);
                Assert.InRange(state.Clay, 0, state.WarehouseCapacity);
                Assert.InRange(state.Iron, 0, state.WarehouseCapacity);
                Assert.InRange(state.Crop, 0, state.GranaryCapacity);
                Assert.True(state.PlayerGoldBalance >= 0);

                // Invariant 2: Concurrency limits (Roman <= 1 active field + <= 1 active inner; Non-Roman <= 1 active total)
                int activeFields = state.ConstructionQueue.Count(q => q.IsActive && q.IsResourceField);
                int activeInners = state.ConstructionQueue.Count(q => q.IsActive && !q.IsResourceField);
                int waitingLoops = state.ConstructionQueue.Count(q => q.IsWaitingLoop);
                int masterBuilders = state.ConstructionQueue.Count(q => q.IsMasterBuilder);

                if (tribeId == 1)
                {
                    Assert.True(activeFields <= 1, $"Roman activeFields exceeded 1: {activeFields}");
                    Assert.True(activeInners <= 1, $"Roman activeInners exceeded 1: {activeInners}");
                }
                else
                {
                    Assert.True(activeFields + activeInners <= 1, $"Non-Roman active jobs exceeded 1: {activeFields + activeInners}");
                }

                Assert.True(waitingLoops <= (hasPlus ? 1 : 0));
                Assert.True(masterBuilders <= state.MaxMasterBuilderSlots);

                // Invariant 3: Waiting loop start timestamp is strictly after active job finish timestamp in same partition
                foreach (var loopJob in state.ConstructionQueue.Where(q => q.IsWaitingLoop))
                {
                    var precedingActive = state.ConstructionQueue
                        .Where(q => q.IsActive && (tribeId != 1 || q.IsResourceField == loopJob.IsResourceField))
                        .ToList();
                    foreach (var active in precedingActive)
                    {
                        Assert.True(loopJob.StartedAtMs >= active.FinishesAtMs);
                    }
                }
            }
        }

        Assert.True(totalCommandsExecuted >= 1000);
    }
}
