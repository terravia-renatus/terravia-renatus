using Akka.Actor;
using Terravia.Actors.Oases;
using Terravia.Actors.Players;
using Terravia.Domain;
using Terravia.Domain.Catalog;
using Terravia.Domain.Oases;
using Terravia.Domain.Players;
using Terravia.Domain.Villages;

namespace Terravia.Actors.Villages;

/// <summary>
/// Akka.NET Aggregate Root Actor managing a single village's state, lazy resource/loyalty accrual,
/// construction/demolition timers, 5-tier starvation resolution, celebrations, Mead Festivals,
/// Capital transfer mutations, settlement, and conquest state transitions.
/// </summary>
public sealed class VillageActor : ReceiveActor, IWithTimers
{
    private const string ConstructionTimerKey = "village-construction-timer";
    private const string DemolitionTimerKey = "village-demolition-timer";
    private const string StarvationTimerKey = "village-starvation-timer";
    private const string CelebrationTimerKey = "village-celebration-timer";
    private const string MilitaryTrainingTimerKey = "village-military-training-timer";
    private const string ResearchTimerKey = "village-research-timer";

    public sealed record UpsertVillageTroopStackCommand(
        StarvationCandidateStack Stack,
        long NowMs);

    public sealed record SetTargetTileOccupationStatusCommand(
        int TargetTileId,
        bool IsOccupied);

    public sealed record ExecuteVillageConquestCommand(
        ConquestEvaluationContext Context,
        int AttackerOriginVillageId,
        long NowMs,
        Func<int, int, int>? RandomProvider = null,
        IReadOnlyList<IActorRef>? AnnexedOasisActors = null);

    public sealed record GetVillageTroopStacksQuery;

    private readonly VillageState _state;
    private readonly IGameCatalog _catalog;
    private readonly VillageConstructionManager _constructionManager;
    private readonly IResourceProductionCalculator _productionCalculator;
    private readonly IStarvationCalculator _starvationCalculator;
    private readonly CelebrationManager _celebrationManager;
    private readonly VillageExpansionManager _expansionManager;
    private readonly SiegeAndConquestCalculator _conquestCalculator;
    private readonly MilitaryTrainingManager _trainingManager;
    private readonly TechnologyResearchManager _researchManager;
    private readonly HospitalHealingManager _hospitalManager;
    private readonly TrapperManager _trapperManager;
    private readonly IActorRef? _persistenceObserver;
    private readonly IActorRef? _signalRBridgeObserver;
    private readonly IActorRef? _playerActorRef;

    private readonly HashSet<int> _occupiedTargetTiles = new();

    public ITimerScheduler Timers { get; set; } = null!;

    public VillageActor(
        VillageState initialState,
        IGameCatalog? catalog = null,
        IBuildingRuleCatalog? ruleCatalog = null,
        IConstructionCalculator? calculator = null,
        IActorRef? persistenceObserver = null,
        IActorRef? signalRBridgeObserver = null,
        IResourceProductionCalculator? productionCalculator = null,
        IStarvationCalculator? starvationCalculator = null,
        CelebrationManager? celebrationManager = null,
        VillageExpansionManager? expansionManager = null,
        SiegeAndConquestCalculator? conquestCalculator = null,
        IActorRef? playerActorRef = null)
    {
        _state = initialState ?? throw new ArgumentNullException(nameof(initialState));
        _state.SyncExactResourcesFromInteger();
        _catalog = catalog ?? FrozenGameCatalog.Default;
        var calc = calculator ?? new ConstructionCalculator(_catalog);
        var rules = ruleCatalog ?? new BuildingRuleCatalog(_catalog, calc);
        _constructionManager = new VillageConstructionManager(_catalog, rules, calc);
        _productionCalculator = productionCalculator ?? new ResourceProductionCalculator(_catalog);
        _starvationCalculator = starvationCalculator ?? new StarvationCalculator(_catalog);
        _celebrationManager = celebrationManager ?? new CelebrationManager();
        _expansionManager = expansionManager ?? new VillageExpansionManager(gameCatalog: _catalog);
        _conquestCalculator = conquestCalculator ?? new SiegeAndConquestCalculator(_catalog, _expansionManager);
        var timeCalc = new TrainingTimeCalculator(_catalog);
        _trainingManager = new MilitaryTrainingManager(_catalog, timeCalc);
        _researchManager = new TechnologyResearchManager(_catalog, timeCalc);
        _hospitalManager = new HospitalHealingManager(_catalog, timeCalc, _trainingManager);
        _trapperManager = new TrapperManager(_catalog, timeCalc, _trainingManager);
        _persistenceObserver = persistenceObserver;
        _signalRBridgeObserver = signalRBridgeObserver;
        _playerActorRef = playerActorRef;

        RegisterCommandHandlers();
    }

    public static Props Props(
        VillageState initialState,
        IGameCatalog? catalog = null,
        IBuildingRuleCatalog? ruleCatalog = null,
        IConstructionCalculator? calculator = null,
        IActorRef? persistenceObserver = null,
        IActorRef? signalRBridgeObserver = null,
        IResourceProductionCalculator? productionCalculator = null,
        IStarvationCalculator? starvationCalculator = null,
        CelebrationManager? celebrationManager = null,
        VillageExpansionManager? expansionManager = null,
        SiegeAndConquestCalculator? conquestCalculator = null,
        IActorRef? playerActorRef = null) =>
        Akka.Actor.Props.Create(() => new VillageActor(
            initialState,
            catalog,
            ruleCatalog,
            calculator,
            persistenceObserver,
            signalRBridgeObserver,
            productionCalculator,
            starvationCalculator,
            celebrationManager,
            expansionManager,
            conquestCalculator,
            playerActorRef));

    private void RegisterCommandHandlers()
    {
        Receive<VillageActorCommandEnvelope<ConstructOrUpgradeBuildingCommand>>(env =>
        {
            AdvanceTimersUpTo(env.NowMs);
            var (result, _, usedGold) = _constructionManager.EnqueueConstruction(_state, env.Command, env.NowMs, env.SitterPermissions);
            if (result.Succeeded)
            {
                _state.SyncExactResourcesFromInteger();
                ScheduleNextTimers(env.NowMs);
                NotifyPersistenceFlush(isImmediateGoldFlush: usedGold, env.NowMs);
            }
            Sender.Tell(result);
        });

        Receive<VillageActorCommandEnvelope<CancelConstructionJobCommand>>(env =>
        {
            AdvanceTimersUpTo(env.NowMs);
            var result = _constructionManager.CancelConstructionJob(_state, env.Command, env.NowMs, env.SitterPermissions);
            if (result.Succeeded)
            {
                _state.SyncExactResourcesFromInteger();
                ScheduleNextTimers(env.NowMs);
                NotifyPersistenceFlush(isImmediateGoldFlush: false, env.NowMs);
            }
            Sender.Tell(result);
        });

        Receive<VillageActorCommandEnvelope<FinishAllConstructionsWithGoldCommand>>(env =>
        {
            AdvanceTimersUpTo(env.NowMs);
            var (result, goldDeducted) = _constructionManager.FinishAllWithGold(_state, env.Command, env.NowMs, env.SitterPermissions);
            if (result.Succeeded)
            {
                RecalculateEconomyRates();
                ScheduleNextTimers(env.NowMs);
                NotifyPersistenceFlush(isImmediateGoldFlush: goldDeducted > 0, env.NowMs);
            }
            Sender.Tell(result);
        });

        Receive<VillageActorCommandEnvelope<StartBuildingDemolitionCommand>>(env =>
        {
            AdvanceTimersUpTo(env.NowMs);
            var (result, goldDeducted) = _constructionManager.StartDemolition(_state, env.Command, env.NowMs, env.SitterPermissions);
            if (result.Succeeded)
            {
                RecalculateEconomyRates();
                ScheduleNextTimers(env.NowMs);
                NotifyPersistenceFlush(isImmediateGoldFlush: goldDeducted > 0, env.NowMs);
            }
            Sender.Tell(result);
        });

        Receive<VillageActorCommandEnvelope<CancelBuildingDemolitionCommand>>(env =>
        {
            AdvanceTimersUpTo(env.NowMs);
            var result = _constructionManager.CancelDemolition(_state, env.Command, env.SitterPermissions);
            if (result.Succeeded)
            {
                ScheduleNextTimers(env.NowMs);
                NotifyPersistenceFlush(isImmediateGoldFlush: false, env.NowMs);
            }
            Sender.Tell(result);
        });

        Receive<AccrueVillageResourcesCommand>(cmd =>
        {
            AdvanceTimersUpTo(cmd.NowMs);
            _state.Wood = Math.Min(_state.WarehouseCapacity, _state.Wood + cmd.AddWood);
            _state.Clay = Math.Min(_state.WarehouseCapacity, _state.Clay + cmd.AddClay);
            _state.Iron = Math.Min(_state.WarehouseCapacity, _state.Iron + cmd.AddIron);
            _state.Crop = Math.Min(_state.GranaryCapacity, _state.Crop + cmd.AddCrop);
            _state.SyncExactResourcesFromInteger();
            _state.LastResourceUpdateAtMs = cmd.NowMs;

            bool promoted = _constructionManager.TryPromoteMasterBuilderJobs(_state, cmd.NowMs);
            if (promoted)
            {
                _state.SyncExactResourcesFromInteger();
                ScheduleNextTimers(cmd.NowMs);
                NotifyPersistenceFlush(isImmediateGoldFlush: true, cmd.NowMs);
            }
            Sender.Tell(CommandResult.Success);
        });

        Receive<UpsertVillageTroopStackCommand>(cmd =>
        {
            AdvanceTimersUpTo(cmd.NowMs);
            int idx = _state.TroopStacks.FindIndex(s => s.StackOrMovementId == cmd.Stack.StackOrMovementId);
            if (idx >= 0)
            {
                _state.TroopStacks[idx] = cmd.Stack;
            }
            else
            {
                _state.TroopStacks.Add(cmd.Stack);
            }

            RecalculateEconomyRates();
            ScheduleNextTimers(cmd.NowMs);
            Sender.Tell(CommandResult.Success);
        });

        Receive<TriggerVillageStarvationCommand>(cmd =>
        {
            AdvanceTimersUpTo(cmd.ExpectedTimestampMs);
            ExecuteDueStarvationSteps(cmd.ExpectedTimestampMs);
            ScheduleNextTimers(cmd.ExpectedTimestampMs);
            Sender.Tell(CommandResult.Success);
        });

        Receive<VillageActorCommandEnvelope<StartTownHallCelebrationCommand>>(env =>
        {
            var result = HandleStartCelebration(env.Command, env.NowMs, env.SitterPermissions);
            Sender.Tell(result);
        });

        Receive<StartTownHallCelebrationCommand>(cmd =>
        {
            var result = HandleStartCelebration(cmd, _state.LastResourceUpdateAtMs, null);
            Sender.Tell(result);
        });

        Receive<VillageActorCommandEnvelope<StartBreweryMeadFestivalCommand>>(env =>
        {
            var result = HandleStartMeadFestival(env.Command, env.NowMs, env.SitterPermissions);
            Sender.Tell(result);
        });

        Receive<StartBreweryMeadFestivalCommand>(cmd =>
        {
            var result = HandleStartMeadFestival(cmd, _state.LastResourceUpdateAtMs, null);
            Sender.Tell(result);
        });

        Receive<PlayerActor.ApplyCapitalTransferPlanToVillageCommand>(cmd =>
        {
            AdvanceTimersUpTo(cmd.NowMs);
            if (cmd.IsOldCapital)
            {
                _expansionManager.ApplyCapitalDemotionToOldCapital(_state);
            }
            else
            {
                _expansionManager.ApplyCapitalPromotionToNewCapital(_state);
            }
            RecalculateEconomyRates();
            ScheduleNextTimers(cmd.NowMs);
            NotifyPersistenceFlush(isImmediateGoldFlush: true, cmd.NowMs);
            NotifyEconomySnapshot(cmd.NowMs);
            Sender.Tell(CommandResult.Success);
        });

        Receive<PlayerActor.PromoteVillageToCapitalOnRazingCommand>(cmd =>
        {
            AdvanceTimersUpTo(cmd.NowMs);
            _expansionManager.ApplyCapitalPromotionToNewCapital(_state);
            RecalculateEconomyRates();
            NotifyPersistenceFlush(isImmediateGoldFlush: true, cmd.NowMs);
            Sender.Tell(CommandResult.Success);
        });

        Receive<SetTargetTileOccupationStatusCommand>(cmd =>
        {
            if (cmd.IsOccupied)
            {
                _occupiedTargetTiles.Add(cmd.TargetTileId);
            }
            else
            {
                _occupiedTargetTiles.Remove(cmd.TargetTileId);
            }
            Sender.Tell(CommandResult.Success);
        });

        Receive<VillageActorCommandEnvelope<FoundNewVillageCommand>>(env =>
        {
            var result = HandleFoundNewVillage(env.Command, env.NowMs);
            Sender.Tell(result);
        });

        Receive<FoundNewVillageCommand>(cmd =>
        {
            var result = HandleFoundNewVillage(cmd, _state.LastResourceUpdateAtMs);
            Sender.Tell(result);
        });

        Receive<ExecuteVillageConquestCommand>(cmd =>
        {
            AdvanceTimersUpTo(cmd.NowMs);
            var resolution = HandleVillageConquest(cmd.Context, cmd.AttackerOriginVillageId, cmd.NowMs, cmd.RandomProvider, cmd.AnnexedOasisActors);
            Sender.Tell(resolution);
        });

        Receive<VillageActorCommandEnvelope<EnqueueMilitaryTrainingCommand>>(env =>
        {
            AdvanceTimersUpTo(env.NowMs);
            var result = _trainingManager.EnqueueTrainingBatch(_state, env.Command, env.NowMs, env.SitterPermissions);
            if (result.Succeeded)
            {
                ScheduleNextTimers(env.NowMs);
                NotifyPersistenceFlush(isImmediateGoldFlush: false, env.NowMs);
                NotifyMilitarySnapshot(env.NowMs);
            }
            Sender.Tell(result);
        });

        Receive<VillageActorCommandEnvelope<EnqueueHospitalHealingCommand>>(env =>
        {
            AdvanceTimersUpTo(env.NowMs);
            var result = _hospitalManager.EnqueueHealingBatch(_state, env.Command, env.NowMs, env.SitterPermissions);
            if (result.Succeeded)
            {
                ScheduleNextTimers(env.NowMs);
                NotifyPersistenceFlush(isImmediateGoldFlush: false, env.NowMs);
                NotifyMilitarySnapshot(env.NowMs);
            }
            Sender.Tell(result);
        });

        Receive<AdmitVillageBattleCasualtiesCommand>(cmd =>
        {
            AdvanceTimersUpTo(cmd.NowMs);
            var result = _hospitalManager.AdmitBattleCasualties(_state, cmd.Context);
            if (result.TotalAdmittedToHospital > 0)
            {
                NotifyPersistenceFlush(isImmediateGoldFlush: false, cmd.NowMs);
                NotifyMilitarySnapshot(cmd.NowMs);
            }
            Sender.Tell(result);
        });

        Receive<VillageActorCommandEnvelope<StartAcademyResearchCommand>>(env =>
        {
            AdvanceTimersUpTo(env.NowMs);
            var result = _researchManager.StartAcademyResearch(_state, env.Command, env.NowMs, env.SitterPermissions);
            if (result.Succeeded)
            {
                ScheduleNextTimers(env.NowMs);
                NotifyPersistenceFlush(isImmediateGoldFlush: false, env.NowMs);
                NotifyMilitarySnapshot(env.NowMs);
            }
            Sender.Tell(result);
        });

        Receive<VillageActorCommandEnvelope<StartSmithyUpgradeCommand>>(env =>
        {
            AdvanceTimersUpTo(env.NowMs);
            var result = _researchManager.StartSmithyUpgrade(_state, env.Command, env.NowMs, env.SitterPermissions);
            if (result.Succeeded)
            {
                ScheduleNextTimers(env.NowMs);
                NotifyPersistenceFlush(isImmediateGoldFlush: false, env.NowMs);
                NotifyMilitarySnapshot(env.NowMs);
            }
            Sender.Tell(result);
        });

        Receive<VillageActorCommandEnvelope<FinishAllResearchWithGoldCommand>>(env =>
        {
            AdvanceTimersUpTo(env.NowMs);
            var (result, goldDeducted, _) = _researchManager.FinishAllResearchWithGold(_state, env.Command, env.NowMs, env.SitterPermissions);
            if (result.Succeeded)
            {
                ScheduleNextTimers(env.NowMs);
                NotifyPersistenceFlush(isImmediateGoldFlush: goldDeducted > 0, env.NowMs);
                NotifyMilitarySnapshot(env.NowMs);
            }
            Sender.Tell(result);
        });

        Receive<VillageActorCommandEnvelope<ReleasePrisonersCommand>>(env =>
        {
            AdvanceTimersUpTo(env.NowMs);
            var (result, outcome, freeRepairs) = _trapperManager.ReleasePrisonersByTrapOwner(_state, env.Command, env.NowMs, env.SitterPermissions);
            if (result.Succeeded && outcome.HasValue)
            {
                ScheduleNextTimers(env.NowMs);
                var eto = new PrisonersReleasedOrLiberatedEto(_state.VillageId, [outcome.Value], freeRepairs, env.NowMs);
                _persistenceObserver?.Tell(eto);
                _signalRBridgeObserver?.Tell(eto);
                NotifyPersistenceFlush(isImmediateGoldFlush: false, env.NowMs);
                NotifyMilitarySnapshot(env.NowMs);
            }
            Sender.Tell(result);
        });

        Receive<VillageActorCommandEnvelope<DissolvePrisonersCommand>>(env =>
        {
            AdvanceTimersUpTo(env.NowMs);
            var (result, freedTraps, freeRepairs) = _trapperManager.DissolvePrisonersByTroopOwner(_state, env.Command, env.NowMs, env.SitterPermissions);
            if (result.Succeeded)
            {
                ScheduleNextTimers(env.NowMs);
                var dissolvedOutcome = new PrisonerLiberationOutcome(
                    PrisonerStackId: env.Command.PrisonerStackId,
                    OwnerVillageId: 0,
                    SurvivingUnitsBySlot: new int[11],
                    HeroSurvived: false,
                    MergedIntoAttackingArmyReturn: false,
                    FreedTrapCount: freedTraps);
                var eto = new PrisonersReleasedOrLiberatedEto(_state.VillageId, [dissolvedOutcome], freeRepairs, env.NowMs);
                _persistenceObserver?.Tell(eto);
                _signalRBridgeObserver?.Tell(eto);
                NotifyPersistenceFlush(isImmediateGoldFlush: false, env.NowMs);
                NotifyMilitarySnapshot(env.NowMs);
            }
            Sender.Tell(result);
        });

        Receive<ProcessDueVillageTimersCommand>(tick =>
        {
            AdvanceTimersUpTo(tick.NowMs);
            ScheduleNextTimers(tick.NowMs);
        });

        Receive<GetVillageStateQuery>(_ =>
        {
            Sender.Tell(_state);
        });

        Receive<GetVillageTroopStacksQuery>(_ =>
        {
            Sender.Tell(_state.TroopStacks.ToList());
        });
    }

    private CommandResult HandleStartCelebration(
        StartTownHallCelebrationCommand cmd,
        long nowMs,
        SitterPermissions? sitterPermissions)
    {
        if (sitterPermissions.HasValue && !sitterPermissions.Value.HasFlag(SitterPermissions.BuildAndDemolish))
        {
            return CommandResult.Fail(
                TerraviaDomainErrorCodes.Sitters.PermissionDenied,
                "Sitter lacks permission to start Town Hall celebrations.");
        }

        AdvanceTimersUpTo(nowMs);

        int townHallLevel = GetHighestBuildingLevel(24);
        var available = new ResourceCost(_state.Wood, _state.Clay, _state.Iron, _state.Crop);
        bool hasActive = _state.ActiveCelebrationType != CelebrationType.None && _state.CelebrationEndsAtMs > nowMs;

        var startResult = _celebrationManager.EvaluateStartCelebration(
            celebrationType: cmd.CelebrationType,
            townHallLevel: townHallLevel,
            hasActiveCelebration: hasActive,
            availableResources: in available,
            serverSpeed: _state.ServerSpeed);

        if (!startResult.Succeeded)
        {
            return CommandResult.Fail(startResult.ErrorCode ?? TerraviaDomainErrorCodes.Villages.InsufficientTownHallLevel);
        }

        _state.Wood -= startResult.DeductedCost.Wood;
        _state.Clay -= startResult.DeductedCost.Clay;
        _state.Iron -= startResult.DeductedCost.Iron;
        _state.Crop -= startResult.DeductedCost.Crop;
        _state.SyncExactResourcesFromInteger();
        _state.ActiveCelebrationType = startResult.CelebrationType;
        _state.CelebrationEndsAtMs = nowMs + startResult.DurationSeconds * 1000L;

        ScheduleNextTimers(nowMs);
        NotifyPersistenceFlush(isImmediateGoldFlush: false, nowMs);
        NotifyEconomySnapshot(nowMs);
        return CommandResult.Success;
    }

    private CommandResult HandleStartMeadFestival(
        StartBreweryMeadFestivalCommand cmd,
        long nowMs,
        SitterPermissions? sitterPermissions)
    {
        if (sitterPermissions.HasValue && !sitterPermissions.Value.HasFlag(SitterPermissions.BuildAndDemolish))
        {
            return CommandResult.Fail(
                TerraviaDomainErrorCodes.Sitters.PermissionDenied,
                "Sitter lacks permission to start Brewery Mead Festivals.");
        }

        AdvanceTimersUpTo(nowMs);

        int breweryLevel = GetHighestBuildingLevel(35);
        var available = new ResourceCost(_state.Wood, _state.Clay, _state.Iron, _state.Crop);
        bool hasActive = _state.FestivalEndsAtMs > nowMs;

        var startResult = _celebrationManager.EvaluateStartMeadFestival(
            ownerTribeId: _state.TribeId,
            isCapital: _state.IsCapital,
            breweryLevel: breweryLevel,
            hasActiveFestival: hasActive,
            availableResources: in available,
            serverSpeed: _state.ServerSpeed);

        if (!startResult.Succeeded)
        {
            return CommandResult.Fail(startResult.ErrorCode ?? TerraviaDomainErrorCodes.Villages.MeadFestivalRequiresTeutonCapitalBrewery);
        }

        _state.Wood -= startResult.DeductedCost.Wood;
        _state.Clay -= startResult.DeductedCost.Clay;
        _state.Iron -= startResult.DeductedCost.Iron;
        _state.Crop -= startResult.DeductedCost.Crop;
        _state.SyncExactResourcesFromInteger();
        _state.FestivalEndsAtMs = nowMs + startResult.DurationSeconds * 1000L;

        _playerActorRef?.Tell(new PlayerActor.ActivateMeadFestivalWindowCommand(
            CapitalVillageId: _state.VillageId,
            BreweryLevel: breweryLevel,
            EndsAtMs: _state.FestivalEndsAtMs,
            NowMs: nowMs));

        NotifyPersistenceFlush(isImmediateGoldFlush: false, nowMs);
        NotifyEconomySnapshot(nowMs);
        return CommandResult.Success;
    }

    private CommandResult HandleFoundNewVillage(FoundNewVillageCommand cmd, long nowMs)
    {
        AdvanceTimersUpTo(nowMs);

        if (_occupiedTargetTiles.Contains(cmd.TargetTileId))
        {
            // Simultaneous Settler arrival race condition: turn around surviving 3 Settlers and refund 750 each resource
            _state.Wood = Math.Min(_state.WarehouseCapacity, _state.Wood + cmd.CarriedResources.Wood);
            _state.Clay = Math.Min(_state.WarehouseCapacity, _state.Clay + cmd.CarriedResources.Clay);
            _state.Iron = Math.Min(_state.WarehouseCapacity, _state.Iron + cmd.CarriedResources.Iron);
            _state.Crop = Math.Min(_state.GranaryCapacity, _state.Crop + cmd.CarriedResources.Crop);
            _state.SyncExactResourcesFromInteger();

            NotifyPersistenceFlush(isImmediateGoldFlush: true, nowMs);
            NotifyEconomySnapshot(nowMs);

            return CommandResult.Fail(
                TerraviaDomainErrorCodes.Villages.TargetTileAlreadyOccupied,
                "Target settlement tile was already occupied; Settlers turned around and 750 of each resource were refunded.");
        }

        if (!_expansionManager.TryBindExpansionSlot(_state, cmd.TargetTileId))
        {
            return CommandResult.Fail(
                TerraviaDomainErrorCodes.Villages.NoFreeExpansionSlot,
                "All 3 expansion slots for this village are already occupied.");
        }

        _occupiedTargetTiles.Add(cmd.TargetTileId);
        NotifyPersistenceFlush(isImmediateGoldFlush: true, nowMs);
        NotifyEconomySnapshot(nowMs);
        return CommandResult.Success;
    }

    private ConquestResolutionResult HandleVillageConquest(
        in ConquestEvaluationContext context,
        int attackerOriginVillageId,
        long nowMs,
        Func<int, int, int>? randomProvider,
        IReadOnlyList<IActorRef>? annexedOasisActors)
    {
        var resolution = _conquestCalculator.EvaluateAdministratorConquest(in context, randomProvider);
        if (!resolution.CanAttemptPersuasion)
        {
            return resolution;
        }

        _state.Loyalty = resolution.ResultingLoyalty;

        if (resolution.WasVillageConquered)
        {
            int previousOwnerId = _state.OwnerPlayerId;
            var releasedOases = _conquestCalculator.ApplyVillageConquestStateReset(
                _state,
                context.AttackerPlayerId,
                context.AttackerTribeId,
                resolution.ApplyBiggerPlayerMinusOneBuildingLevelPenalty);

            if (annexedOasisActors is not null)
            {
                foreach (var oasisActor in annexedOasisActors)
                {
                    oasisActor.Tell((new AbandonAnnexedOasisCommand(previousOwnerId, previousOwnerId, _state.VillageId, 0), nowMs));
                }
            }

            RecalculateEconomyRates();
            ScheduleNextTimers(nowMs);

            bool wasRazed = _state.Population <= 0;
            if (wasRazed)
            {
                _playerActorRef?.Tell(new PlayerActor.HandleVillageRazedOrConqueredCommand(_state.VillageId, nowMs));
            }

            var eto = new VillageConqueredEto(
                VillageId: _state.VillageId,
                PreviousOwnerPlayerId: previousOwnerId,
                NewOwnerPlayerId: _state.OwnerPlayerId,
                NewOwnerTribeId: _state.TribeId,
                ExpansionOriginVillageId: attackerOriginVillageId,
                WasRazedToZeroPopulation: wasRazed,
                ReleasedOasisTileIds: releasedOases,
                TimestampMs: nowMs);

            _persistenceObserver?.Tell(eto);
            _signalRBridgeObserver?.Tell(eto);
            NotifyPersistenceFlush(isImmediateGoldFlush: true, nowMs);
        }
        else
        {
            NotifyPersistenceFlush(isImmediateGoldFlush: false, nowMs);
        }

        return resolution;
    }

    private void AdvanceTimersUpTo(long nowMs)
    {
        if (nowMs > _state.LastResourceUpdateAtMs && _state.LastResourceUpdateAtMs > 0)
        {
            double loyaltyRegenPerHour = _expansionManager.CalculateVillageLoyaltyRegenPerHour(
                _state.SlotBuildingTypes,
                _state.SlotLevels,
                _state.ServerSpeed);

            var accrual = _productionCalculator.AccrueVillageResourcesAndLoyalty(
                new VillageAccrualInput(
                    CurrentWood: _state.ExactWood,
                    CurrentClay: _state.ExactClay,
                    CurrentIron: _state.ExactIron,
                    CurrentCrop: _state.ExactCrop,
                    WarehouseCap: _state.WarehouseCapacity,
                    GranaryCap: _state.GranaryCapacity,
                    GrossWoodPerHour: _state.GrossWoodPerHour,
                    GrossClayPerHour: _state.GrossClayPerHour,
                    GrossIronPerHour: _state.GrossIronPerHour,
                    NetCropPerHour: _state.NetCropProductionPerHour,
                    CurrentLoyalty: _state.Loyalty,
                    LoyaltyRegenPerHour: loyaltyRegenPerHour,
                    LastUpdatedMs: _state.LastResourceUpdateAtMs),
                nowMs);

            _state.ExactWood = accrual.Wood;
            _state.ExactClay = accrual.Clay;
            _state.ExactIron = accrual.Iron;
            _state.ExactCrop = accrual.Crop;
            _state.SyncIntegerResourcesFromExact();
            _state.Loyalty = accrual.Loyalty;
            _state.LastResourceUpdateAtMs = nowMs;
        }
        else if (_state.LastResourceUpdateAtMs == 0 && nowMs > 0)
        {
            _state.LastResourceUpdateAtMs = nowMs;
        }

        if (_state.ActiveCelebrationType != CelebrationType.None && nowMs >= _state.CelebrationEndsAtMs)
        {
            int awardedCp = _state.ActiveCelebrationType == CelebrationType.Great
                ? PlayerConsts.GreatCelebrationCpReward
                : PlayerConsts.SmallCelebrationCpReward;
            _state.ActiveCelebrationType = CelebrationType.None;
            _state.CelebrationEndsAtMs = 0;
            _playerActorRef?.Tell(new PlayerActor.AwardCelebrationCulturePointsCommand(
                VillageId: _state.VillageId,
                AwardedCulturePoints: awardedCp,
                NowMs: nowMs));
        }

        int priorQueueCount = _state.ConstructionQueue.Count;
        bool usedGold = _constructionManager.CompleteDueConstructionsAndPromote(_state, nowMs);
        bool demoCompleted = _constructionManager.CompleteDueDemolition(_state, nowMs);
        int graduatedUnits = _trainingManager.CompleteDueTrainingGraduations(_state, nowMs);
        int completedResearch = _researchManager.CompleteDueResearchItems(_state, nowMs);

        if (usedGold || demoCompleted || _state.ConstructionQueue.Count != priorQueueCount || graduatedUnits > 0 || completedResearch > 0)
        {
            _state.SyncExactResourcesFromInteger();
            RecalculateEconomyRates();
            NotifyPersistenceFlush(isImmediateGoldFlush: usedGold, nowMs);
            if (graduatedUnits > 0 || completedResearch > 0)
            {
                NotifyMilitarySnapshot(nowMs);
            }
        }

        if (_state.NetCropProductionPerHour < 0 && !_state.IsPeaceMode && _state.Crop <= 0)
        {
            ExecuteDueStarvationSteps(nowMs);
        }
    }

    private void ExecuteDueStarvationSteps(long nowMs)
    {
        var steps = new List<StarvationStepResult>();
        int safetyGuard = 0;

        while (_state.NetCropProductionPerHour < 0 &&
               !_state.IsPeaceMode &&
               _state.Crop <= 0 &&
               _state.TroopStacks.Count > 0 &&
               safetyGuard++ < 100)
        {
            var evalCtx = new StarvationEvaluationContext(
                CurrentStoredCrop: _state.ExactCrop,
                NetCropPerHour: _state.NetCropProductionPerHour,
                IsPeaceMode: _state.IsPeaceMode,
                CandidateStacks: _state.TroopStacks);

            if (!_starvationCalculator.TryResolveNextStarvationStep(in evalCtx, out var step))
            {
                break;
            }

            if (step.ShouldDeleteEmptyStackOrMovement)
            {
                _state.TroopStacks.RemoveAll(s => s.StackOrMovementId == step.TargetStackOrMovementId);
            }

            if (step.FreedTrapCount > 0)
            {
                _state.TotalTrapsCount = Math.Max(0, _state.TotalTrapsCount - step.FreedTrapCount);
                _state.OccupiedTrapsCount = Math.Max(0, _state.OccupiedTrapsCount - step.FreedTrapCount);
            }

            _state.ExactCrop = Math.Clamp(_state.ExactCrop + step.CreditedCrop, 0.0, _state.GranaryCapacity);
            _state.SyncIntegerResourcesFromExact();
            RecalculateEconomyRates();
            steps.Add(step);
        }

        if (steps.Count > 0)
        {
            var eto = new VillageStarvationExecutedEto(
                VillageId: _state.VillageId,
                TimestampMs: nowMs,
                Steps: steps,
                ResultingStoredCrop: _state.ExactCrop,
                ResultingNetCropPerHour: _state.NetCropProductionPerHour);

            _persistenceObserver?.Tell(eto);
            _signalRBridgeObserver?.Tell(eto);
        }
    }

    private void RecalculateEconomyRates()
    {
        var snapshot = _productionCalculator.CalculateProduction(
            new VillageProductionContext(
                SlotBuildingTypes: _state.SlotBuildingTypes,
                SlotLevels: _state.SlotLevels,
                AnnexedOasisWoodUnits: _state.AnnexedOasisWoodUnits,
                AnnexedOasisClayUnits: _state.AnnexedOasisClayUnits,
                AnnexedOasisIronUnits: _state.AnnexedOasisIronUnits,
                AnnexedOasisCropUnits: _state.AnnexedOasisCropUnits,
                HasStationedLivingHero: _state.HasStationedLivingHero,
                HeroResourceBonusPoints: _state.HeroResourceBonusPoints,
                HeroResourceFocusMode: _state.HeroResourceFocusMode,
                PlusWoodBonusActive: _state.PlusWoodBonusActive,
                PlusClayBonusActive: _state.PlusClayBonusActive,
                PlusIronBonusActive: _state.PlusIronBonusActive,
                PlusCropBonusActive: _state.PlusCropBonusActive,
                IsArchonVillage: _state.IsArchonVillage,
                VillageTribeId: _state.TribeId,
                Population: _state.Population,
                FedTroopStacks: _state.TroopStacks,
                DietControlArtifactMultiplier: _state.DietControlArtifactMultiplier,
                ServerSpeed: _state.ServerSpeed));

        _state.GrossWoodPerHour = snapshot.GrossWoodPerHour;
        _state.GrossClayPerHour = snapshot.GrossClayPerHour;
        _state.GrossIronPerHour = snapshot.GrossIronPerHour;
        _state.GrossCropPerHour = snapshot.GrossCropPerHour;
        _state.NetCropProductionPerHour = snapshot.NetCropPerHour;
    }

    private int GetHighestBuildingLevel(int buildingTypeId)
    {
        int max = 0;
        for (int slot = VillageConsts.MinInnerBuildingSlotId; slot <= VillageConsts.MaxStandardSlotId; slot++)
        {
            if (_state.SlotBuildingTypes[slot] == buildingTypeId && _state.SlotLevels[slot] > max)
            {
                max = _state.SlotLevels[slot];
            }
        }
        return max;
    }

    private void ScheduleNextTimers(long nowMs)
    {
        var nextBuild = _state.ConstructionQueue
            .Where(q => !q.IsMasterBuilder)
            .OrderBy(q => q.FinishesAtMs)
            .FirstOrDefault();

        if (nextBuild is not null)
        {
            long delayMs = Math.Max(1L, nextBuild.FinishesAtMs - nowMs);
            Timers.StartSingleTimer(
                ConstructionTimerKey,
                new ProcessDueVillageTimersCommand(nextBuild.FinishesAtMs),
                TimeSpan.FromMilliseconds(delayMs));
        }
        else
        {
            Timers.Cancel(ConstructionTimerKey);
        }

        if (_state.ActiveDemolition is { } demo)
        {
            long delayMs = Math.Max(1L, demo.FinishesAtMs - nowMs);
            Timers.StartSingleTimer(
                DemolitionTimerKey,
                new ProcessDueVillageTimersCommand(demo.FinishesAtMs),
                TimeSpan.FromMilliseconds(delayMs));
        }
        else
        {
            Timers.Cancel(DemolitionTimerKey);
        }

        if (_state.ActiveCelebrationType != CelebrationType.None && _state.CelebrationEndsAtMs > nowMs)
        {
            long delayMs = Math.Max(1L, _state.CelebrationEndsAtMs - nowMs);
            Timers.StartSingleTimer(
                CelebrationTimerKey,
                new ProcessDueVillageTimersCommand(_state.CelebrationEndsAtMs),
                TimeSpan.FromMilliseconds(delayMs));
        }
        else
        {
            Timers.Cancel(CelebrationTimerKey);
        }

        if (_state.NetCropProductionPerHour < 0 && !_state.IsPeaceMode && _state.TroopStacks.Count > 0)
        {
            int secondsToZero = _state.ExactCrop <= 0
                ? 0
                : (int)Math.Ceiling((_state.ExactCrop / -_state.NetCropProductionPerHour) * 3600.0);
            long triggerAtMs = nowMs + secondsToZero * 1000L;
            long delayMs = Math.Max(1L, triggerAtMs - nowMs);
            Timers.StartSingleTimer(
                StarvationTimerKey,
                new TriggerVillageStarvationCommand(_state.VillageId, triggerAtMs),
                TimeSpan.FromMilliseconds(delayMs));
        }
        else
        {
            Timers.Cancel(StarvationTimerKey);
        }

        if (_state.TrainingQueues.Count > 0)
        {
            long nextGraduationMs = _state.TrainingQueues.Min(q => q.NextUnitGraduatesAtMs);
            long delayMs = Math.Max(1L, nextGraduationMs - nowMs);
            Timers.StartSingleTimer(
                MilitaryTrainingTimerKey,
                new ProcessDueVillageTimersCommand(nextGraduationMs),
                TimeSpan.FromMilliseconds(delayMs));
        }
        else
        {
            Timers.Cancel(MilitaryTrainingTimerKey);
        }

        if (_state.ResearchQueue.Count > 0)
        {
            long nextResearchFinishMs = _state.ResearchQueue.Min(q => q.FinishesAtMs);
            long delayMs = Math.Max(1L, nextResearchFinishMs - nowMs);
            Timers.StartSingleTimer(
                ResearchTimerKey,
                new ProcessDueVillageTimersCommand(nextResearchFinishMs),
                TimeSpan.FromMilliseconds(delayMs));
        }
        else
        {
            Timers.Cancel(ResearchTimerKey);
        }
    }

    private void NotifyMilitarySnapshot(long timestampMs)
    {
        if (_signalRBridgeObserver is null)
            return;

        var trainingDtos = _state.TrainingQueues
            .Select(q => new TrainingQueueItemDto(
                q.QueueItemId,
                q.FacilityCategory,
                q.UnitTypeId,
                q.AmountRemaining,
                q.EachTimeSeconds,
                q.CommenceAtMs,
                q.FinishesAtMs,
                q.NextUnitGraduatesAtMs))
            .ToList();

        var researchDtos = _state.ResearchQueue
            .Select(q => new ResearchQueueItemDto(
                q.QueueItemId,
                q.Category,
                q.UnitTypeId,
                q.TribeUnitSlot,
                q.TargetLevel,
                q.IsPlusWaitingLoop,
                q.StartedAtMs,
                q.FinishesAtMs))
            .ToList();

        var snapshot = new VillageMilitarySnapshotDto(
            VillageId: _state.VillageId,
            UnlockedUnitSlots: _state.UnlockedUnitSlots.ToArray(),
            OffenseUpgradeLevels: _state.OffenseUpgradeLevels.ToArray(),
            DefenseUpgradeLevels: _state.DefenseUpgradeLevels.ToArray(),
            WoundedUnitsBySlot: _state.WoundedUnitsBySlot.ToArray(),
            MaxHospitalCapacity: _hospitalManager.GetMaxHospitalCapacity(_state),
            TotalTrapsCount: _state.TotalTrapsCount,
            OccupiedTrapsCount: _state.OccupiedTrapsCount,
            MaxTrapCapacity: _trapperManager.CalculateTotalTrapCapacity(_state.SlotBuildingTypes, _state.SlotLevels),
            TrainingQueues: trainingDtos,
            ResearchQueue: researchDtos);

        _signalRBridgeObserver.Tell(new VillageMilitaryStateChangedEto(_state.VillageId, timestampMs, snapshot));
    }

    private void NotifyEconomySnapshot(long timestampMs)
    {
        if (_signalRBridgeObserver is null)
            return;

        double loyaltyRegenPerHour = _expansionManager.CalculateVillageLoyaltyRegenPerHour(
            _state.SlotBuildingTypes,
            _state.SlotLevels,
            _state.ServerSpeed);

        var prodSnapshot = new ResourceProductionSnapshot(
            GrossWoodPerHour: _state.GrossWoodPerHour,
            GrossClayPerHour: _state.GrossClayPerHour,
            GrossIronPerHour: _state.GrossIronPerHour,
            GrossCropPerHour: _state.GrossCropPerHour,
            EffectivePopulationUpkeep: _state.Population,
            TotalTroopUpkeepPerHour: Math.Max(0, _state.GrossCropPerHour - _state.Population - _state.NetCropProductionPerHour),
            NetCropPerHour: _state.NetCropProductionPerHour);

        var dto = new VillageEconomySnapshotDto(
            VillageId: _state.VillageId,
            TileId: _state.TileId,
            Name: _state.Name,
            FieldTopologyId: _state.FieldTopologyId,
            IsCapital: _state.IsCapital,
            IsArchon: _state.IsArchonVillage,
            Population: _state.Population,
            CulturePointsPerDay: _state.IsArchonVillage ? 0 : _state.CulturePointsPerDay,
            Loyalty: _state.Loyalty,
            LoyaltyRegenPerHour: loyaltyRegenPerHour,
            LastUpdatedServerMs: timestampMs,
            Wood: _state.ExactWood,
            Clay: _state.ExactClay,
            Iron: _state.ExactIron,
            Crop: _state.ExactCrop,
            WarehouseCap: _state.WarehouseCapacity,
            GranaryCap: _state.GranaryCapacity,
            Production: prodSnapshot,
            ActiveCelebrationType: _state.ActiveCelebrationType,
            CelebrationEndsAtMs: _state.CelebrationEndsAtMs,
            FestivalEndsAtMs: _state.FestivalEndsAtMs,
            UnlockedExpansionSlots: 0,
            ExpansionSlot1VillageId: _state.ExpansionSlot1VillageId,
            ExpansionSlot2VillageId: _state.ExpansionSlot2VillageId,
            ExpansionSlot3VillageId: _state.ExpansionSlot3VillageId,
            RemainingTrainableSettlerEquivalents: 0);

        _signalRBridgeObserver.Tell(new VillageEconomyChangedEto(_state.VillageId, timestampMs, dto));
    }

    private void NotifyPersistenceFlush(bool isImmediateGoldFlush, long timestampMs)
    {
        _persistenceObserver?.Tell(new VillageStateFlushEto(_state.VillageId, isImmediateGoldFlush, timestampMs));

        if (_signalRBridgeObserver is not null)
        {
            var slots = new List<VillageSlotDto>(41);
            for (int slot = VillageConsts.MinResourceFieldSlotId; slot <= VillageConsts.MaxStandardSlotId; slot++)
            {
                if (_state.SlotBuildingTypes[slot] != 0 || _state.SlotLevels[slot] != 0)
                {
                    slots.Add(new VillageSlotDto(slot, _state.SlotBuildingTypes[slot], _state.SlotLevels[slot]));
                }
            }
            if (_state.WorldWonderBuildingType == 40)
            {
                slots.Add(new VillageSlotDto(VillageConsts.WorldWonderSlotId, 40, _state.WorldWonderLevel));
            }

            _signalRBridgeObserver.Tell(new VillageConstructionChangedEto(
                VillageId: _state.VillageId,
                ServerTimeMs: timestampMs,
                Wood: _state.Wood,
                Clay: _state.Clay,
                Iron: _state.Iron,
                Crop: _state.Crop,
                WarehouseCap: _state.WarehouseCapacity,
                GranaryCap: _state.GranaryCapacity,
                NetCropPerHour: _state.NetCropProductionPerHour,
                Population: _state.Population,
                CulturePointsPerDay: _state.IsArchonVillage ? 0 : _state.CulturePointsPerDay,
                UpdatedSlots: slots,
                ConstructionQueue: _state.ConstructionQueue.ToList(),
                ActiveDemolition: _state.ActiveDemolition));
        }
    }
}
