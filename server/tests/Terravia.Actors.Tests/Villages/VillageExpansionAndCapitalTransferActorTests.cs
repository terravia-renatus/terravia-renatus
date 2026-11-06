using Akka.Actor;
using Akka.TestKit.Xunit2;
using Terravia.Actors.Players;
using Terravia.Actors.Villages;
using Terravia.Domain;
using Terravia.Domain.Catalog;
using Terravia.Domain.Players;
using Terravia.Domain.Villages;
using Xunit;

namespace Terravia.Actors.Tests.Villages;

public sealed class VillageExpansionAndCapitalTransferActorTests : TestKit
{
    [Fact]
    public async Task PlayerActor_OrchestratesCapitalTransferAcrossOldAndNewCapitalVillageActors_AndExcludesArchonCp()
    {
        var ledger = new PlayerCultureLedger(playerId: 1, capitalVillageId: 100, initialTimestampMs: 1_000L);
        var playerActor = Sys.ActorOf(PlayerActor.Props(ledger));

        var oldCapState = new VillageState(villageId: 100, ownerPlayerId: 1, tribeId: 2, isCapital: true)
        {
            Population = 400,
            CulturePointsPerDay = 200
        };
        oldCapState.SetSlot(1, 1, 14);   // Woodcutter Lvl 14 -> downgraded to 10
        oldCapState.SetSlot(20, 34, 8);  // Stonemason Lvl 8 -> destroyed
        oldCapState.SetSlot(21, 35, 5);  // Brewery Lvl 5 -> destroyed

        var newCapState = new VillageState(villageId: 200, ownerPlayerId: 1, tribeId: 2, isCapital: false)
        {
            Population = 300,
            CulturePointsPerDay = 150
        };
        newCapState.SetSlot(25, 26, 1);  // Palace Lvl 1 in new Capital
        newCapState.SetSlot(26, 29, 10); // Great Barracks Lvl 10 -> destroyed
        newCapState.SetSlot(27, 38, 10); // Great Warehouse Lvl 10 -> destroyed

        var oldCapActor = Sys.ActorOf(VillageActor.Props(oldCapState, playerActorRef: playerActor));
        var newCapActor = Sys.ActorOf(VillageActor.Props(newCapState, playerActorRef: playerActor));

        await playerActor.Ask<CommandResult>(
            new PlayerActor.UpsertPlayerVillageSummaryCommand(100, 200, 400, IsArchon: false, 1_000L, oldCapActor));
        await playerActor.Ask<CommandResult>(
            new PlayerActor.UpsertPlayerVillageSummaryCommand(200, 150, 300, IsArchon: false, 1_000L, newCapActor));
        await playerActor.Ask<CommandResult>(
            new PlayerActor.UpsertPlayerVillageSummaryCommand(300, 999, 500, IsArchon: true, 1_000L));

        // Verify Archon village contributes 0 CP/day (Total = 200 + 150 = 350 CP/day)
        var snapshotBefore = await playerActor.Ask<PlayerCultureSnapshotDto>(
            new PlayerActor.GetPlayerCultureSnapshotQuery(1_000L));
        Assert.Equal(350.0, snapshotBefore.DailyCulturePointsRate, precision: 3);

        // Execute Capital Transfer to Village 200
        var transferResult = await playerActor.Ask<CommandResult>(
            new VillageActorCommandEnvelope<ChangeCapitalVillageCommand>(
                new ChangeCapitalVillageCommand(1, 1, 200),
                NowMs: 2_000L));

        Assert.True(transferResult.Succeeded);

        var updatedOld = await oldCapActor.Ask<VillageState>(new GetVillageStateQuery());
        var updatedNew = await newCapActor.Ask<VillageState>(new GetVillageStateQuery());

        Assert.False(updatedOld.IsCapital);
        Assert.Equal(10, updatedOld.SlotLevels[1]); // Downgraded from 14 to 10
        Assert.Equal(0, updatedOld.SlotBuildingTypes[20]); // Stonemason destroyed
        Assert.Equal(0, updatedOld.SlotBuildingTypes[21]); // Brewery destroyed

        Assert.True(updatedNew.IsCapital);
        Assert.Equal(26, updatedNew.SlotBuildingTypes[25]); // New Palace preserved
        Assert.Equal(0, updatedNew.SlotBuildingTypes[26]);  // Great Barracks destroyed
        Assert.Equal(0, updatedNew.SlotBuildingTypes[27]);  // Great Warehouse destroyed
    }

    [Fact]
    public async Task VillageActor_SimultaneousSettlerRaceCondition_Refunds750ResourcesAndRejectsSettlement()
    {
        var state = new VillageState(villageId: 10, ownerPlayerId: 1, tribeId: 1, isCapital: true)
        {
            Wood = 100,
            Clay = 100,
            Iron = 100,
            Crop = 100,
            WarehouseCapacity = 2000,
            GranaryCapacity = 2000,
            LastResourceUpdateAtMs = 1_000L
        };

        var actor = Sys.ActorOf(VillageActor.Props(state));
        var carried = new ResourceCost(750, 750, 750, 750);

        // First settlement on tile 555 succeeds
        var first = await actor.Ask<CommandResult>(
            new FoundNewVillageCommand(10, 1, 1, TargetTileId: 555, TargetFieldTopologyId: 3, carried));
        Assert.True(first.Succeeded);

        // Second simultaneous arrival on already occupied tile 555 fails and refunds 750 of each resource
        var second = await actor.Ask<CommandResult>(
            new FoundNewVillageCommand(10, 1, 1, TargetTileId: 555, TargetFieldTopologyId: 3, carried));
        Assert.False(second.Succeeded);
        Assert.Equal(TerraviaDomainErrorCodes.Villages.TargetTileAlreadyOccupied, second.ErrorCode);

        var updated = await actor.Ask<VillageState>(new GetVillageStateQuery());
        Assert.Equal(850, updated.Wood);
        Assert.Equal(850, updated.Clay);
        Assert.Equal(850, updated.Iron);
        Assert.Equal(850, updated.Crop);
    }

    [Fact]
    public async Task PlayerActor_ReassignsCapitalToHighestPopulationNonArchonVillage_WhenCapitalIsRazed()
    {
        var ledger = new PlayerCultureLedger(playerId: 1, capitalVillageId: 10, initialTimestampMs: 1_000L);
        var playerActor = Sys.ActorOf(PlayerActor.Props(ledger));

        var v20State = new VillageState(villageId: 20, ownerPlayerId: 1, tribeId: 1, isCapital: false) { Population = 250 };
        var v30State = new VillageState(villageId: 30, ownerPlayerId: 1, tribeId: 1, isCapital: false) { Population = 600 };
        var v20Actor = Sys.ActorOf(VillageActor.Props(v20State, playerActorRef: playerActor));
        var v30Actor = Sys.ActorOf(VillageActor.Props(v30State, playerActorRef: playerActor));

        await playerActor.Ask<CommandResult>(new PlayerActor.UpsertPlayerVillageSummaryCommand(10, 100, 500, false, 1_000L));
        await playerActor.Ask<CommandResult>(new PlayerActor.UpsertPlayerVillageSummaryCommand(20, 80, 250, false, 1_000L, v20Actor));
        await playerActor.Ask<CommandResult>(new PlayerActor.UpsertPlayerVillageSummaryCommand(30, 120, 600, false, 1_000L, v30Actor));
        await playerActor.Ask<CommandResult>(new PlayerActor.UpsertPlayerVillageSummaryCommand(40, 0, 999, true, 1_000L)); // Archon

        await playerActor.Ask<CommandResult>(new PlayerActor.HandleVillageRazedOrConqueredCommand(10, 2_000L));

        var snapshot = await playerActor.Ask<PlayerCultureSnapshotDto>(new PlayerActor.GetPlayerCultureSnapshotQuery(2_000L));
        Assert.Equal(30, snapshot.CapitalVillageId);

        var promotedV30 = await v30Actor.Ask<VillageState>(new GetVillageStateQuery());
        Assert.True(promotedV30.IsCapital);
    }
}
