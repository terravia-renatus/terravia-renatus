using Akka.Actor;
using Terravia.Domain;
using Terravia.Domain.Catalog;
using Terravia.Domain.Villages;

namespace Terravia.Actors.Villages;

/// <summary>
/// Akka.NET Aggregate Root Actor managing a single village's state, resources, and timers,
/// delegating domain rule execution to pure domain managers.
/// </summary>
public sealed class VillageActor : ReceiveActor, IWithTimers
{
    private const string ConstructionTimerKey = "village-construction-timer";
    private const string DemolitionTimerKey = "village-demolition-timer";

    private readonly VillageState _state;
    private readonly VillageConstructionManager _constructionManager;
    private readonly IActorRef? _persistenceObserver;
    private readonly IActorRef? _signalRBridgeObserver;

    public ITimerScheduler Timers { get; set; } = null!;

    public VillageActor(
        VillageState initialState,
        IGameCatalog? catalog = null,
        IBuildingRuleCatalog? ruleCatalog = null,
        IConstructionCalculator? calculator = null,
        IActorRef? persistenceObserver = null,
        IActorRef? signalRBridgeObserver = null)
    {
        _state = initialState ?? throw new ArgumentNullException(nameof(initialState));
        var cat = catalog ?? FrozenGameCatalog.Default;
        var calc = calculator ?? new ConstructionCalculator(cat);
        var rules = ruleCatalog ?? new BuildingRuleCatalog(cat, calc);
        _constructionManager = new VillageConstructionManager(cat, rules, calc);
        _persistenceObserver = persistenceObserver;
        _signalRBridgeObserver = signalRBridgeObserver;

        RegisterCommandHandlers();
    }

    public static Props Props(
        VillageState initialState,
        IGameCatalog? catalog = null,
        IBuildingRuleCatalog? ruleCatalog = null,
        IConstructionCalculator? calculator = null,
        IActorRef? persistenceObserver = null,
        IActorRef? signalRBridgeObserver = null) =>
        Akka.Actor.Props.Create(() => new VillageActor(initialState, catalog, ruleCatalog, calculator, persistenceObserver, signalRBridgeObserver));

    private void RegisterCommandHandlers()
    {
        Receive<VillageActorCommandEnvelope<ConstructOrUpgradeBuildingCommand>>(env =>
        {
            AdvanceTimersUpTo(env.NowMs);
            var (result, _, usedGold) = _constructionManager.EnqueueConstruction(_state, env.Command, env.NowMs, env.SitterPermissions);
            if (result.Succeeded)
            {
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
            _state.LastResourceUpdateAtMs = cmd.NowMs;

            bool promoted = _constructionManager.TryPromoteMasterBuilderJobs(_state, cmd.NowMs);
            if (promoted)
            {
                ScheduleNextTimers(cmd.NowMs);
                NotifyPersistenceFlush(isImmediateGoldFlush: true, cmd.NowMs);
            }
            Sender.Tell(CommandResult.Success);
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
    }

    private void AdvanceTimersUpTo(long nowMs)
    {
        int priorQueueCount = _state.ConstructionQueue.Count;
        bool usedGold = _constructionManager.CompleteDueConstructionsAndPromote(_state, nowMs);
        bool demoCompleted = _constructionManager.CompleteDueDemolition(_state, nowMs);
        if (usedGold || demoCompleted || _state.ConstructionQueue.Count != priorQueueCount)
        {
            NotifyPersistenceFlush(isImmediateGoldFlush: usedGold, nowMs);
        }
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
                CulturePointsPerDay: _state.CulturePointsPerDay,
                UpdatedSlots: slots,
                ConstructionQueue: _state.ConstructionQueue.ToList(),
                ActiveDemolition: _state.ActiveDemolition));
        }
    }
}
