using Akka.Actor;
using Akka.TestKit.Xunit2;
using Terravia.Actors.Villages;
using Terravia.Domain;
using Terravia.Domain.Villages;
using Xunit;

namespace Terravia.Actors.Tests.Villages;

public sealed class VillageMilitaryActorTests : TestKit
{
    [Fact]
    public async Task VillageActor_EnqueuesTraining_AndGraduatesUnitsSequentiallyOnTimerTicks()
    {
        var signalRProbe = CreateTestProbe();
        var state = new VillageState(villageId: 100, ownerPlayerId: 1, tribeId: 1, isCapital: true)
        {
            Wood = 20_000,
            Clay = 20_000,
            Iron = 20_000,
            Crop = 20_000,
            WarehouseCapacity = 50_000,
            GranaryCapacity = 50_000,
            LastResourceUpdateAtMs = 1_000L
        };
        state.SetSlot(20, 19, 1); // Barracks Level 1

        var actor = Sys.ActorOf(VillageActor.Props(state, signalRBridgeObserver: signalRProbe.Ref));

        var enrollResult = await actor.Ask<TrainingEnrollmentResult>(
            new VillageActorCommandEnvelope<EnqueueMilitaryTrainingCommand>(
                new EnqueueMilitaryTrainingCommand(100, TrainingFacilityCategory.Barracks, UnitTypeId: 1, RequestedAmount: 4),
                NowMs: 1_000L));

        Assert.True(enrollResult.Succeeded);
        Assert.Equal(4, enrollResult.EnrolledAmount);
        var snapshotEto = signalRProbe.FishForMessage<VillageMilitaryStateChangedEto>(_ => true);
        Assert.Single(snapshotEto.Snapshot.TrainingQueues);

        // Advance time by 2.5 units -> 2 Legionnaires graduate
        long tickMs = 1_000L + 2L * enrollResult.EachTimeSeconds * 1000L + 500L;
        actor.Tell(new ProcessDueVillageTimersCommand(tickMs));

        var updatedState = await actor.Ask<VillageState>(new GetVillageStateQuery());
        Assert.Equal(2, updatedState.TrainingQueues[0].AmountRemaining);

        var homeStack = Assert.Single(updatedState.TroopStacks);
        Assert.Equal(2, homeStack.UnitCounts[1]);
    }

    [Fact]
    public async Task VillageActor_AcademyAndSmithyResearch_CompletesViaTimerOrTwoGoldInstantFinish()
    {
        var state = new VillageState(villageId: 101, ownerPlayerId: 1, tribeId: 1, isCapital: true)
        {
            Wood = 50_000,
            Clay = 50_000,
            Iron = 50_000,
            Crop = 50_000,
            WarehouseCapacity = 100_000,
            GranaryCapacity = 100_000,
            PlayerGoldBalance = 10,
            LastResourceUpdateAtMs = 1_000L
        };
        state.SetSlot(19, 15, 10); // Main Building 10
        state.SetSlot(20, 19, 5);  // Barracks 5
        state.SetSlot(21, 22, 5);  // Academy 5
        state.SetSlot(22, 12, 5);  // Blacksmith 5
        state.SetSlot(23, 13, 5);  // Armoury 5

        var actor = Sys.ActorOf(VillageActor.Props(state));

        // 1. Start Academy research for Praetorian (u2)
        var academyRes = await actor.Ask<ResearchEnrollmentResult>(
            new VillageActorCommandEnvelope<StartAcademyResearchCommand>(
                new StartAcademyResearchCommand(101, 2),
                NowMs: 1_000L));
        Assert.True(academyRes.Succeeded);

        // 2. Start Blacksmith upgrade for Legionnaire (u1)
        var smithyRes = await actor.Ask<ResearchEnrollmentResult>(
            new VillageActorCommandEnvelope<StartSmithyUpgradeCommand>(
                new StartSmithyUpgradeCommand(101, ResearchCategory.Blacksmith, 1),
                NowMs: 1_000L));
        Assert.True(smithyRes.Succeeded);

        // Sitter without BuyAndSpendGold cannot finish with Gold
        var deniedGold = await actor.Ask<CommandResult>(
            new VillageActorCommandEnvelope<FinishAllResearchWithGoldCommand>(
                new FinishAllResearchWithGoldCommand(101),
                NowMs: 2_000L,
                SitterPermissions: SitterPermissions.BuildAndDemolish));
        Assert.False(deniedGold.Succeeded);
        Assert.Equal(TerraviaDomainErrorCodes.Sitters.GoldPermissionDenied, deniedGold.ErrorCode);

        // Owner finishes both with 2 Gold
        var finishGold = await actor.Ask<CommandResult>(
            new VillageActorCommandEnvelope<FinishAllResearchWithGoldCommand>(
                new FinishAllResearchWithGoldCommand(101),
                NowMs: 2_000L));
        Assert.True(finishGold.Succeeded);

        var updated = await actor.Ask<VillageState>(new GetVillageStateQuery());
        Assert.Equal(8, updated.PlayerGoldBalance);
        Assert.True(updated.UnlockedUnitSlots[2]);
        Assert.Equal(1, updated.OffenseUpgradeLevels[1]);
        Assert.Empty(updated.ResearchQueue);
    }

    [Fact]
    public async Task VillageActor_HospitalCasualtyAdmission_AndHealingQueueGraduation()
    {
        var state = new VillageState(villageId: 102, ownerPlayerId: 1, tribeId: 1, isCapital: false)
        {
            Wood = 20_000,
            Clay = 20_000,
            Iron = 20_000,
            Crop = 20_000,
            WarehouseCapacity = 50_000,
            GranaryCapacity = 50_000,
            LastResourceUpdateAtMs = 1_000L
        };
        state.SetSlot(25, 46, 5); // Hospital Level 5 (capacity 150, 40% save rate)

        var actor = Sys.ActorOf(VillageActor.Props(state));

        var dead = new int[11];
        dead[1] = 100; // 100 dead Legionnaires -> 40 admitted to Hospital

        var admission = await actor.Ask<BattleCasualtyAdmissionResult>(
            new AdmitVillageBattleCasualtiesCommand(
                new BattleCasualtyAdmissionContext(dead),
                NowMs: 1_000L));
        Assert.Equal(40, admission.TotalAdmittedToHospital);

        // Enqueue healing for 10 Legionnaires at 50% cost
        var healEnroll = await actor.Ask<TrainingEnrollmentResult>(
            new VillageActorCommandEnvelope<EnqueueHospitalHealingCommand>(
                new EnqueueHospitalHealingCommand(102, 1, 10),
                NowMs: 2_000L));
        Assert.True(healEnroll.Succeeded);
        Assert.Equal(10, healEnroll.EnrolledAmount);

        // Advance past completion of all 10 healed units
        actor.Tell(new ProcessDueVillageTimersCommand(healEnroll.FinishesAtMs + 100L));

        var updated = await actor.Ask<VillageState>(new GetVillageStateQuery());
        Assert.Equal(30, updated.WoundedUnitsBySlot[1]);
        Assert.Empty(updated.TrainingQueues);
        var homeStack = Assert.Single(updated.TroopStacks);
        Assert.Equal(10, homeStack.UnitCounts[1]);
    }

    [Fact]
    public async Task VillageActor_TrapperPrisonerReleaseAndDissolution_EmitsEtoAndQueuesFreeRepairs()
    {
        var signalRProbe = CreateTestProbe();
        var state = new VillageState(villageId: 103, ownerPlayerId: 1, tribeId: 3, isCapital: false)
        {
            TotalTrapsCount = 100,
            OccupiedTrapsCount = 60,
            LastResourceUpdateAtMs = 1_000L
        };
        state.SetSlot(20, 36, 10); // Trapper Level 10

        var stack1 = new PrisonerStack
        {
            PrisonerStackId = 1,
            CaptorVillageId = 103,
            OwnerVillageId = 200,
            OwnerPlayerId = 2,
            OwnerTribeId = 1
        };
        stack1.UnitCountsBySlot[1] = 30;

        var stack2 = new PrisonerStack
        {
            PrisonerStackId = 2,
            CaptorVillageId = 103,
            OwnerVillageId = 201,
            OwnerPlayerId = 2,
            OwnerTribeId = 1
        };
        stack2.UnitCountsBySlot[1] = 30;

        state.HeldPrisoners.Add(stack1);
        state.HeldPrisoners.Add(stack2);

        var actor = Sys.ActorOf(VillageActor.Props(state, signalRBridgeObserver: signalRProbe.Ref));

        // 1. Gaul trap owner releases stack 1 -> 100% survival (30 units), 100% free repair (30 traps queued)
        var releaseRes = await actor.Ask<CommandResult>(
            new VillageActorCommandEnvelope<ReleasePrisonersCommand>(
                new ReleasePrisonersCommand(103, 1),
                NowMs: 2_000L));
        Assert.True(releaseRes.Succeeded);

        var releaseEto = signalRProbe.FishForMessage<PrisonersReleasedOrLiberatedEto>(_ => true);
        Assert.Equal(30, releaseEto.FreeRepairsQueued);
        signalRProbe.FishForMessage<VillageMilitaryStateChangedEto>(_ => true);

        // 2. Troop owner dissolves stack 2 -> 100% death, round(30 / 3) = 10 free repairs queued
        var dissolveRes = await actor.Ask<CommandResult>(
            new VillageActorCommandEnvelope<DissolvePrisonersCommand>(
                new DissolvePrisonersCommand(103, 2, 2),
                NowMs: 3_000L));
        Assert.True(dissolveRes.Succeeded);

        var dissolveEto = signalRProbe.FishForMessage<PrisonersReleasedOrLiberatedEto>(_ => true);
        Assert.Equal(10, dissolveEto.FreeRepairsQueued);
    }
}
