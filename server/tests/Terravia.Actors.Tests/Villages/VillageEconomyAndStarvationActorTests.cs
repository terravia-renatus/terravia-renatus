using Akka.Actor;
using Akka.TestKit.Xunit2;
using Terravia.Actors.Villages;
using Terravia.Domain;
using Terravia.Domain.Villages;
using Xunit;

namespace Terravia.Actors.Tests.Villages;

public sealed class VillageEconomyAndStarvationActorTests : TestKit
{
    [Fact]
    public async Task VillageActor_LazilyAccruesResourcesAndLoyalty_AndClampsToStorageCap()
    {
        var state = new VillageState(villageId: 10, ownerPlayerId: 1, tribeId: 1, isCapital: true)
        {
            Wood = 100,
            Clay = 100,
            Iron = 100,
            Crop = 100,
            WarehouseCapacity = 800,
            GranaryCapacity = 800,
            GrossWoodPerHour = 300,
            GrossClayPerHour = 300,
            GrossIronPerHour = 300,
            GrossCropPerHour = 200,
            NetCropProductionPerHour = 150,
            Loyalty = 50.0,
            LastResourceUpdateAtMs = 1_000_000L
        };
        // Palace (gid 26) at Level 10 => +10% loyalty per hour at 1x speed
        state.SetSlot(26, 26, 10);

        var actor = Sys.ActorOf(VillageActor.Props(state));

        // Advance 2 hours (7,200,000 ms)
        actor.Tell(new ProcessDueVillageTimersCommand(8_200_000L));
        var updated = await actor.Ask<VillageState>(new GetVillageStateQuery());

        Assert.Equal(700, updated.Wood);
        Assert.Equal(700, updated.Clay);
        Assert.Equal(700, updated.Iron);
        Assert.Equal(400, updated.Crop);
        Assert.Equal(70.0, updated.Loyalty, precision: 3);
    }

    [Fact]
    public async Task VillageActor_Starvation_DeletesEmptiedReinforcementStack_AndDecrementsGallicTrapOnTrappedDeath()
    {
        var persistenceProbe = CreateTestProbe();
        var state = new VillageState(villageId: 20, ownerPlayerId: 1, tribeId: 3, isCapital: true)
        {
            Population = 20,
            Crop = 0,
            GranaryCapacity = 800,
            TotalTrapsCount = 10,
            OccupiedTrapsCount = 1,
            LastResourceUpdateAtMs = 1_000L
        };

        var actor = Sys.ActorOf(VillageActor.Props(state, persistenceObserver: persistenceProbe.Ref));

        // 1. Add a foreign non-allied reinforcement stack with 1 Legionnaire (u1, CropCost = 40)
        var foreignCounts = new int[91];
        foreignCounts[1] = 1;
        var foreignStack = new StarvationCandidateStack
        {
            StackOrMovementId = 101L,
            Tier = StarvationPriorityTier.VillageReinforcement,
            OwnerVillageId = 99,
            LocationTileId = 20,
            UnitCounts = foreignCounts,
            HasHero = false
        };

        // 2. Add a trapped enemy stack with 1 Clubswinger (u11, CropCost = 60)
        var trappedCounts = new int[91];
        trappedCounts[11] = 1;
        var trappedStack = new StarvationCandidateStack
        {
            StackOrMovementId = 102L,
            Tier = StarvationPriorityTier.TrappedPrisoner,
            OwnerVillageId = 88,
            LocationTileId = 20,
            UnitCounts = trappedCounts,
            HasHero = false
        };

        // 3. Add a large own stationary stack so NetCrop remains negative
        var ownCounts = new int[91];
        ownCounts[21] = 50; // 50 Phalanxes => 50 upkeep/hr
        var ownStack = new StarvationCandidateStack
        {
            StackOrMovementId = 103L,
            Tier = StarvationPriorityTier.OwnStationed,
            OwnerVillageId = 20,
            LocationTileId = 20,
            UnitCounts = ownCounts,
            HasHero = false
        };

        await actor.Ask<CommandResult>(new VillageActor.UpsertVillageTroopStackCommand(foreignStack, 1_000L));
        await actor.Ask<CommandResult>(new VillageActor.UpsertVillageTroopStackCommand(trappedStack, 1_000L));
        await actor.Ask<CommandResult>(new VillageActor.UpsertVillageTroopStackCommand(ownStack, 1_000L));

        // Trigger first starvation at Crop = 0 => Tier 1 foreign reinforcement (Stack 101, u1) starves, credits 40 Crop, deletes stack 101
        await actor.Ask<CommandResult>(new TriggerVillageStarvationCommand(20, 1_000L));

        var stacksAfterFirst = await actor.Ask<List<StarvationCandidateStack>>(new VillageActor.GetVillageTroopStacksQuery());
        Assert.DoesNotContain(stacksAfterFirst, s => s.StackOrMovementId == 101L);

        var stateAfterFirst = await actor.Ask<VillageState>(new GetVillageStateQuery());
        Assert.Equal(30, stateAfterFirst.Crop);

        // Advance time until the 40 Crop is consumed so Crop reaches 0 again
        await actor.Ask<CommandResult>(new TriggerVillageStarvationCommand(20, 10_000_000L));

        var stateAfterSecond = await actor.Ask<VillageState>(new GetVillageStateQuery());
        var stacksAfterSecond = await actor.Ask<List<StarvationCandidateStack>>(new VillageActor.GetVillageTroopStacksQuery());

        // Trapped stack 102 was emptied and deleted, and TotalTrapsCount / OccupiedTrapsCount decremented by 1
        Assert.DoesNotContain(stacksAfterSecond, s => s.StackOrMovementId == 102L);
        Assert.Equal(9, stateAfterSecond.TotalTrapsCount);
        Assert.Equal(0, stateAfterSecond.OccupiedTrapsCount);
    }

    [Fact]
    public async Task VillageActor_LowFreeCropConstructionLockout_EnforcesAndExemptsCorrectBuildings()
    {
        var state = new VillageState(villageId: 30, ownerPlayerId: 1, tribeId: 1, isCapital: true)
        {
            Wood = 5000,
            Clay = 5000,
            Iron = 5000,
            Crop = 5000,
            WarehouseCapacity = 8000,
            GranaryCapacity = 8000,
            Population = 20,
            GrossCropPerHour = 21,
            NetCropProductionPerHour = 1, // FreeCrop = 1 (<= 1 lockout active!)
            LastResourceUpdateAtMs = 1_000L
        };
        state.SetSlot(1, 1, 1); // Woodcutter Lvl 1 on Slot 1
        state.SetSlot(2, 4, 1); // Cropland Lvl 1 on Slot 2
        state.SetSlot(19, 15, 1); // Main Building Lvl 1 on Slot 19

        var actor = Sys.ActorOf(VillageActor.Props(state));

        // 1. Upgrading Woodcutter (gid 1) on Slot 1 should be rejected with NegativeCropConstructionRestricted
        var rejectWood = await actor.Ask<CommandResult>(
            new VillageActorCommandEnvelope<ConstructOrUpgradeBuildingCommand>(
                new ConstructOrUpgradeBuildingCommand(1, 1, 30, 1, 1),
                NowMs: 1_000L));

        Assert.False(rejectWood.Succeeded);
        Assert.Equal(TerraviaDomainErrorCodes.Villages.NegativeCropConstructionRestricted, rejectWood.ErrorCode);

        // 2. Upgrading Cropland (gid 4) on Slot 2 is EXEMPT and must succeed
        var allowCrop = await actor.Ask<CommandResult>(
            new VillageActorCommandEnvelope<ConstructOrUpgradeBuildingCommand>(
                new ConstructOrUpgradeBuildingCommand(1, 1, 30, 2, 4),
                NowMs: 1_000L));

        Assert.True(allowCrop.Succeeded);
    }
}
