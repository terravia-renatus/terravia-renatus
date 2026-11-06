using Akka.Actor;
using Akka.TestKit.Xunit2;
using Terravia.Actors.Villages;
using Terravia.Domain;
using Terravia.Domain.Villages;

namespace Terravia.Actors.Tests.Villages;

public sealed class VillageDemolitionActorTests : TestKit
{
    private static VillageState CreateInitialDemolitionState()
    {
        var state = new VillageState(villageId: 600, ownerPlayerId: 20, tribeId: 1, isCapital: true)
        {
            PlayerGoldBalance = 30,
            Population = 120
        };
        state.SetSlot(19, 15, 10); // Main Building Level 10
        state.SetSlot(20, 19, 2);  // Barracks Level 2
        state.SetSlot(21, 22, 5);  // Academy Level 5
        return state;
    }

    [Fact]
    public async Task VillageActor_ShouldHandleTimedDemolition_Cancel_AndTenGoldInstantCompleteDemolition()
    {
        var flushProbe = CreateTestProbe();
        var state = CreateInitialDemolitionState();
        var villageActor = Sys.ActorOf(VillageActor.Props(state, persistenceObserver: flushProbe.Ref));

        long t0 = 2_000_000L;

        // 1. Start timed demolition on Slot 20 (Barracks Level 2 -> 1)
        var startTimed = await villageActor.Ask<CommandResult>(
            new VillageActorCommandEnvelope<StartBuildingDemolitionCommand>(
                new StartBuildingDemolitionCommand(20, 20, 600, SlotId: 20, IsInstantCompleteWithGold: false),
                t0));
        Assert.True(startTimed.Succeeded);

        var snap1 = await villageActor.Ask<VillageState>(new GetVillageStateQuery());
        Assert.NotNull(snap1.ActiveDemolition);
        long finishMs = snap1.ActiveDemolition.FinishesAtMs;

        // 2. Advance virtual timer to completion -> Barracks drops from Level 2 to Level 1
        villageActor.Tell(new ProcessDueVillageTimersCommand(finishMs));
        var snap2 = await villageActor.Ask<VillageState>(new GetVillageStateQuery());
        Assert.Null(snap2.ActiveDemolition);
        Assert.Equal(1, snap2.GetSlotLevel(20));

        // 3. Start and cancel timed demolition on Slot 20
        await villageActor.Ask<CommandResult>(
            new VillageActorCommandEnvelope<StartBuildingDemolitionCommand>(
                new StartBuildingDemolitionCommand(20, 20, 600, SlotId: 20, IsInstantCompleteWithGold: false),
                finishMs + 1000L));
        var cancelRes = await villageActor.Ask<CommandResult>(
            new VillageActorCommandEnvelope<CancelBuildingDemolitionCommand>(
                new CancelBuildingDemolitionCommand(20, 20, 600),
                finishMs + 2000L));
        Assert.True(cancelRes.Succeeded);

        // 4. Execute 10-Gold Instant Complete Demolition on Slot 21 (Academy Level 5 -> Level 0 immediately)
        var instantRes = await villageActor.Ask<CommandResult>(
            new VillageActorCommandEnvelope<StartBuildingDemolitionCommand>(
                new StartBuildingDemolitionCommand(20, 20, 600, SlotId: 21, IsInstantCompleteWithGold: true),
                finishMs + 3000L));
        Assert.True(instantRes.Succeeded);

        var snap3 = await villageActor.Ask<VillageState>(new GetVillageStateQuery());
        Assert.Equal(0, snap3.GetSlotLevel(21));
        Assert.Equal(0, snap3.GetSlotBuildingType(21));
        Assert.Equal(20, snap3.PlayerGoldBalance); // 30 - 10 = 20 Gold
    }
}
