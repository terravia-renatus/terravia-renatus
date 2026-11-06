using Akka.Actor;
using Akka.TestKit.Xunit2;
using Terravia.Actors.Villages;
using Terravia.Domain;
using Terravia.Domain.Villages;

namespace Terravia.Actors.Tests.Villages;

public sealed class VillageConstructionQueueActorTests : TestKit
{
    private static VillageState CreateInitialVillageState(int tribeId = 1, bool hasPlus = true)
    {
        var state = new VillageState(villageId: 500, ownerPlayerId: 10, tribeId: tribeId, isCapital: true)
        {
            HasPlusSubscription = hasPlus,
            Wood = 20_000,
            Clay = 20_000,
            Iron = 20_000,
            Crop = 20_000,
            WarehouseCapacity = 80_000,
            GranaryCapacity = 80_000,
            NetCropProductionPerHour = 300,
            PlayerGoldBalance = 25
        };
        state.SetSlot(19, 15, 5); // Main Building Level 5
        state.SetSlot(39, 16, 1); // Rally Point Level 1
        return state;
    }

    [Fact]
    public async Task VillageActor_ShouldExecuteRomanDualQueue_PromoteMasterBuilder_AndEnforceSitterGoldPermissions()
    {
        var flushProbe = CreateTestProbe();
        var signalRProbe = CreateTestProbe();
        var state = CreateInitialVillageState(tribeId: 1, hasPlus: true);
        var villageActor = Sys.ActorOf(VillageActor.Props(
            state,
            persistenceObserver: flushProbe.Ref,
            signalRBridgeObserver: signalRProbe.Ref));

        long t0 = 1_000_000L;

        // 1. Start Woodcutter (Slot 1) and Barracks (Slot 20) concurrently
        var res1 = await villageActor.Ask<CommandResult>(
            new VillageActorCommandEnvelope<ConstructOrUpgradeBuildingCommand>(
                new ConstructOrUpgradeBuildingCommand(10, 10, 500, SlotId: 1, BuildingTypeId: 1),
                t0));
        Assert.True(res1.Succeeded);

        var delta1 = signalRProbe.ExpectMsg<VillageConstructionChangedEto>();
        Assert.Equal(500, delta1.VillageId);
        Assert.Single(delta1.ConstructionQueue);

        var res2 = await villageActor.Ask<CommandResult>(
            new VillageActorCommandEnvelope<ConstructOrUpgradeBuildingCommand>(
                new ConstructOrUpgradeBuildingCommand(10, 10, 500, SlotId: 20, BuildingTypeId: 19),
                t0));
        Assert.True(res2.Succeeded);

        var delta2 = signalRProbe.ExpectMsg<VillageConstructionChangedEto>();
        Assert.Equal(2, delta2.ConstructionQueue.Count);

        // 2. Reserve a Master Builder job on Slot 2 (Clay Pit) explicitly
        var resMb = await villageActor.Ask<CommandResult>(
            new VillageActorCommandEnvelope<ConstructOrUpgradeBuildingCommand>(
                new ConstructOrUpgradeBuildingCommand(10, 10, 500, SlotId: 2, BuildingTypeId: 2, UseMasterBuilder: true),
                t0));
        Assert.True(resMb.Succeeded);

        var snapshot1 = await villageActor.Ask<VillageState>(new GetVillageStateQuery());
        Assert.Equal(3, snapshot1.ConstructionQueue.Count);
        Assert.Contains(snapshot1.ConstructionQueue, q => q.SlotId == 2 && q.IsMasterBuilder);
        Assert.Equal(25, snapshot1.PlayerGoldBalance); // Gold not yet charged for Master Builder

        // 3. Advance time past Slot 1 completion -> Slot 1 reaches Level 1 and Slot 2 Master Builder promotes (charging 1 Gold)
        long slot1Finish = snapshot1.ConstructionQueue.First(q => q.SlotId == 1).FinishesAtMs;
        villageActor.Tell(new ProcessDueVillageTimersCommand(slot1Finish));

        var snapshot2 = await villageActor.Ask<VillageState>(new GetVillageStateQuery());
        Assert.Equal(1, snapshot2.GetSlotLevel(1));
        var promotedJob = snapshot2.ConstructionQueue.First(q => q.SlotId == 2);
        Assert.False(promotedJob.IsMasterBuilder);
        Assert.True(promotedJob.IsActive);
        Assert.Equal(24, snapshot2.PlayerGoldBalance); // 1 Gold charged on Master Builder promotion

        // 4. Verify Sitter without GoldMask cannot invoke FinishAllConstructionsWithGoldCommand
        var sitterDenied = await villageActor.Ask<CommandResult>(
            new VillageActorCommandEnvelope<FinishAllConstructionsWithGoldCommand>(
                new FinishAllConstructionsWithGoldCommand(ActorPlayerId: 99, OwnerPlayerId: 10, VillageId: 500),
                slot1Finish + 1000L,
                SitterPermissions: SitterPermissions.BuildAndDemolish));
        Assert.False(sitterDenied.Succeeded);
        Assert.Equal(TerraviaDomainErrorCodes.Sitters.GoldPermissionDenied, sitterDenied.ErrorCode);

        // 5. Sitter WITH GoldMask succeeds and triggers an immediate Gold persistence flush
        var sitterAllowed = await villageActor.Ask<CommandResult>(
            new VillageActorCommandEnvelope<FinishAllConstructionsWithGoldCommand>(
                new FinishAllConstructionsWithGoldCommand(ActorPlayerId: 99, OwnerPlayerId: 10, VillageId: 500),
                slot1Finish + 1000L,
                SitterPermissions: SitterPermissions.BuildAndDemolish | SitterPermissions.BuyAndSpendGold));
        Assert.True(sitterAllowed.Succeeded);

        var snapshot3 = await villageActor.Ask<VillageState>(new GetVillageStateQuery());
        Assert.Empty(snapshot3.ConstructionQueue);
        Assert.Equal(1, snapshot3.GetSlotLevel(2));
        Assert.Equal(1, snapshot3.GetSlotLevel(20));
    }
}
