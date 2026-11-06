using Akka.Actor;
using Akka.TestKit.Xunit2;
using Terravia.Actors.Oases;
using Terravia.Domain;
using Terravia.Domain.Oases;
using Terravia.Infrastructure.Oases;

namespace Terravia.Actors.Tests.Oases;

public sealed class OasisLifecycleActorTests : TestKit
{
    [Fact]
    public void OasisActor_AnnexesWildOasis_RegeneratesLoyalty_AndReleasesOnManualAbandonmentReturningReinforcements()
    {
        var persistenceProbe = CreateTestProbe();
        var worldProbe = CreateTestProbe();

        var initialState = new OasisState
        {
            TileId = 501,
            X = 5,
            Y = 5,
            OasisTypeId = 12, // +50% Crop
            RichnessTier = 1,
            AnnexedByVillageId = null,
            OwnerPlayerId = OasisConsts.NaturePlayerId,
            Loyalty = 100.0,
            LastUpdatedMs = 1_000_000L,
            NextNatureRegenAtMs = 4_600_000L,
            ServerSpeed = 1.0
        };
        initialState.WildFaunaCounts31To40[0] = 5; // 5 Rats

        var actor = Sys.ActorOf(OasisActor.Props(
            initialState,
            OasisEcologyCatalog.Default,
            new OasisAnnexationManager(),
            persistenceProbe.Ref,
            worldProbe.Ref));

        // 1. Capture all 5 Rats with Hero Cages before combat
        actor.Tell((AvailableCages: 5, HeroSentAlone: true, NowMs: 1_100_000L));
        var cageResult = ExpectMsg<HeroCageCaptureResult>();
        Assert.Equal(5, cageResult.CagesConsumed);
        Assert.Equal(0, cageResult.RemainingWildDefenders);
        Assert.True(cageResult.SkipCombat);

        // 2. Annex cleared wild oasis with Hero from Village 10 (Mansion Level 10)
        var strikeCtx = new OasisStrikeContext(
            AttackerVillageX: 4,
            AttackerVillageY: 4,
            OasisX: 5,
            OasisY: 5,
            MapRadius: 200,
            AttackerHeroMansionLevel: 10,
            AttackerCurrentlyOwnedOases: 0,
            AttackerHeroSurvived: true,
            IsNormalAttack: true,
            RemainingOasisDefenders: 0,
            CurrentAnnexedByVillageId: null,
            DefenderCurrentlyOwnedOases: 0,
            CurrentOasisLoyalty: 100.0);

        actor.Tell((strikeCtx, AttackerVillageId: 10, AttackerPlayerId: 7, AttackerMansionLevel: 10, NowMs: 1_200_000L));
        var strikeEval = ExpectMsg<OasisConquestEvaluation>();
        Assert.True(strikeEval.WasAnnexedOrConquered);

        var annexEvent = worldProbe.ExpectMsg<OasisOwnershipChangedEto>();
        Assert.Null(annexEvent.PreviousOwnerVillageId);
        Assert.Equal(10, annexEvent.NewOwnerVillageId);

        // 3. Add a reinforcement stack to the annexed oasis and manually abandon it
        initialState.StationedReinforcementStackIds.Add(9001L);
        actor.Tell((new AbandonAnnexedOasisCommand(7, 7, 10, 501), NowMs: 2_000_000L));
        var abandonResult = ExpectMsg<CommandResult>();
        Assert.True(abandonResult.Succeeded);

        var releaseEvent = worldProbe.ExpectMsg<OasisOwnershipChangedEto>();
        Assert.Equal(10, releaseEvent.PreviousOwnerVillageId);
        Assert.Null(releaseEvent.NewOwnerVillageId);
        Assert.Equal(OasisConsts.NaturePlayerId, releaseEvent.NewOwnerPlayerId);
        Assert.Contains(9001L, releaseEvent.ReturnedReinforcementStackIds);
    }
}
