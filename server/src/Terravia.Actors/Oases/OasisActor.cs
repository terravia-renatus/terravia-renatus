using Akka.Actor;
using Terravia.Domain;
using Terravia.Domain.Oases;
using Terravia.Infrastructure.Oases;

namespace Terravia.Actors.Oases;

/// <summary>
/// Akka.NET Aggregate Root Actor managing a single oasis tile (<c>WildUnoccupied</c> vs <c>PlayerAnnexed</c>),
/// lazy resource and loyalty accrual, scheduled <c>Nature</c> fauna regeneration, Hero Cage capture,
/// annexation/conquest transitions, and manual/conquest abandonment.
/// </summary>
public sealed class OasisActor : ReceiveActor, IWithTimers
{
    private const string NatureRegenTimerKey = "oasis-nature-regen-timer";

    private readonly OasisState _state;
    private readonly OasisEcologyCatalog _ecologyCatalog;
    private readonly OasisAnnexationManager _annexationManager;
    private readonly IActorRef? _persistenceObserver;
    private readonly IActorRef? _worldObserver;

    public ITimerScheduler Timers { get; set; } = null!;

    public OasisActor(
        OasisState initialState,
        OasisEcologyCatalog? ecologyCatalog = null,
        OasisAnnexationManager? annexationManager = null,
        IActorRef? persistenceObserver = null,
        IActorRef? worldObserver = null)
    {
        _state = initialState ?? throw new ArgumentNullException(nameof(initialState));
        _ecologyCatalog = ecologyCatalog ?? OasisEcologyCatalog.Default;
        _annexationManager = annexationManager ?? new OasisAnnexationManager();
        _persistenceObserver = persistenceObserver;
        _worldObserver = worldObserver;

        RegisterHandlers();
    }

    public static Props Props(
        OasisState initialState,
        OasisEcologyCatalog? ecologyCatalog = null,
        OasisAnnexationManager? annexationManager = null,
        IActorRef? persistenceObserver = null,
        IActorRef? worldObserver = null) =>
        Akka.Actor.Props.Create(() => new OasisActor(initialState, ecologyCatalog, annexationManager, persistenceObserver, worldObserver));

    protected override void PreStart()
    {
        base.PreStart();
        ScheduleNatureRegenIfWild(_state.LastUpdatedMs);
    }

    private void RegisterHandlers()
    {
        Receive<AbandonAnnexedOasisCommand>(cmd =>
        {
            AccrueOasisUpTo(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            if (_state.AnnexedByVillageId != cmd.VillageId)
            {
                Sender.Tell(CommandResult.Fail(TerraviaDomainErrorCodes.Oases.OasisNotOwnedByVillage));
                return;
            }

            ReleaseToWildNatureState(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Sender.Tell(CommandResult.Success);
        });

        Receive<(AbandonAnnexedOasisCommand Command, long NowMs)>(env =>
        {
            AccrueOasisUpTo(env.NowMs);
            if (_state.AnnexedByVillageId != env.Command.VillageId)
            {
                Sender.Tell(CommandResult.Fail(TerraviaDomainErrorCodes.Oases.OasisNotOwnedByVillage));
                return;
            }

            ReleaseToWildNatureState(env.NowMs);
            Sender.Tell(CommandResult.Success);
        });

        Receive<(int AvailableCages, bool HeroSentAlone, long NowMs)>(cageAttack =>
        {
            AccrueOasisUpTo(cageAttack.NowMs);
            if (_state.AnnexedByVillageId is not null)
            {
                Sender.Tell(new HeroCageCaptureResult(0, new int[10], 0, false));
                return;
            }

            var result = _annexationManager.ResolveHeroCageCapture(
                cageAttack.AvailableCages,
                _state.WildFaunaCounts31To40,
                cageAttack.HeroSentAlone);

            if (result.CagesConsumed > 0)
            {
                _persistenceObserver?.Tell(new OasisStateFlushEto(_state.TileId, IsImmediateFlush: true, cageAttack.NowMs));
            }

            Sender.Tell(result);
        });

        Receive<(OasisStrikeContext Context, int AttackerVillageId, int AttackerPlayerId, int AttackerMansionLevel, long NowMs)>(strike =>
        {
            AccrueOasisUpTo(strike.NowMs);
            var ctx = strike.Context with
            {
                CurrentAnnexedByVillageId = _state.AnnexedByVillageId,
                CurrentOasisLoyalty = _state.Loyalty
            };

            var eval = _annexationManager.EvaluateHeroOasisStrike(in ctx);
            if (eval.WasAnnexedOrConquered)
            {
                int? prevOwnerVillage = _state.AnnexedByVillageId;
                var returnedStacks = _state.StationedReinforcementStackIds.ToList();
                _state.StationedReinforcementStackIds.Clear();
                Array.Clear(_state.WildFaunaCounts31To40);

                _state.AnnexedByVillageId = strike.AttackerVillageId;
                _state.OwnerPlayerId = strike.AttackerPlayerId;
                _state.HeroMansionLevelInOwningVillage = strike.AttackerMansionLevel;
                _state.Loyalty = OasisConsts.MaxLoyalty;
                _state.LastUpdatedMs = strike.NowMs;
                Timers.Cancel(NatureRegenTimerKey);

                _worldObserver?.Tell(new OasisOwnershipChangedEto(
                    OasisTileId: _state.TileId,
                    OasisTypeId: _state.OasisTypeId,
                    PreviousOwnerVillageId: prevOwnerVillage,
                    NewOwnerVillageId: strike.AttackerVillageId,
                    NewOwnerPlayerId: strike.AttackerPlayerId,
                    ReturnedReinforcementStackIds: returnedStacks,
                    TimestampMs: strike.NowMs));

                _persistenceObserver?.Tell(new OasisStateFlushEto(_state.TileId, IsImmediateFlush: true, strike.NowMs));
            }
            else if (eval.LoyaltyReductionPercent > 0)
            {
                _state.Loyalty = eval.ResultingLoyalty;
                _state.LastUpdatedMs = strike.NowMs;
                _persistenceObserver?.Tell(new OasisStateFlushEto(_state.TileId, IsImmediateFlush: true, strike.NowMs));
            }

            Sender.Tell(eval);
        });

        Receive<long>(nowMs =>
        {
            AccrueOasisUpTo(nowMs);
            if (_state.AnnexedByVillageId is null && nowMs >= _state.NextNatureRegenAtMs)
            {
                _ecologyCatalog.RegenerateWildOasisFauna(
                    _state.OasisTypeId,
                    _state.RichnessTier,
                    _state.ActivePlayerCount,
                    _state.AvgPlayerProgress,
                    _state.WildFaunaCounts31To40,
                    (min, _) => min); // deterministic base min in default timer tick

                long intervalMs = Math.Max(1000L, _state.NatureRegenIntervalSeconds * 1000L);
                _state.NextNatureRegenAtMs = nowMs + intervalMs;
                ScheduleNatureRegenIfWild(nowMs);
                _persistenceObserver?.Tell(new OasisStateFlushEto(_state.TileId, IsImmediateFlush: false, nowMs));
            }
            Sender.Tell(_state);
        });
    }

    private void AccrueOasisUpTo(long nowMs)
    {
        if (nowMs <= _state.LastUpdatedMs)
            return;

        double elapsedHours = (nowMs - _state.LastUpdatedMs) / 3_600_000.0;
        double speed = _state.ServerSpeed > 0 ? _state.ServerSpeed : 1.0;

        if (_state.AnnexedByVillageId is null)
        {
            double baseRate = OasisConsts.WildOasisBaseHourlyResourceGen * speed;
            double cropRate = (_state.OasisTypeId == OasisConsts.FiftyPercentCropOasisTypeId
                ? OasisConsts.WildOasisFiftyCropHourlyGen
                : OasisConsts.WildOasisBaseHourlyResourceGen) * speed;

            _state.Wood = Math.Min(OasisConsts.WildOasisMaxResourceStock, _state.Wood + baseRate * elapsedHours);
            _state.Clay = Math.Min(OasisConsts.WildOasisMaxResourceStock, _state.Clay + baseRate * elapsedHours);
            _state.Iron = Math.Min(OasisConsts.WildOasisMaxResourceStock, _state.Iron + baseRate * elapsedHours);
            _state.Crop = Math.Min(OasisConsts.WildOasisMaxResourceStock, _state.Crop + cropRate * elapsedHours);
            _state.Loyalty = OasisConsts.MaxLoyalty;
        }
        else
        {
            double loyaltyRegenRate = Math.Max(0, _state.HeroMansionLevelInOwningVillage) * speed;
            _state.Loyalty = Math.Min(OasisConsts.MaxLoyalty, _state.Loyalty + loyaltyRegenRate * elapsedHours);
        }

        _state.LastUpdatedMs = nowMs;
    }

    private void ReleaseToWildNatureState(long nowMs)
    {
        int? previousVillageId = _state.AnnexedByVillageId;
        var returnedStacks = _state.StationedReinforcementStackIds.ToList();
        _state.StationedReinforcementStackIds.Clear();

        _state.AnnexedByVillageId = null;
        _state.OwnerPlayerId = OasisConsts.NaturePlayerId;
        _state.HeroMansionLevelInOwningVillage = 0;
        _state.Loyalty = OasisConsts.MaxLoyalty;
        _state.LastUpdatedMs = nowMs;
        _state.NextNatureRegenAtMs = nowMs + Math.Max(1000L, _state.NatureRegenIntervalSeconds * 1000L);

        ScheduleNatureRegenIfWild(nowMs);

        _worldObserver?.Tell(new OasisOwnershipChangedEto(
            OasisTileId: _state.TileId,
            OasisTypeId: _state.OasisTypeId,
            PreviousOwnerVillageId: previousVillageId,
            NewOwnerVillageId: null,
            NewOwnerPlayerId: OasisConsts.NaturePlayerId,
            ReturnedReinforcementStackIds: returnedStacks,
            TimestampMs: nowMs));

        _persistenceObserver?.Tell(new OasisStateFlushEto(_state.TileId, IsImmediateFlush: true, nowMs));
    }

    private void ScheduleNatureRegenIfWild(long nowMs)
    {
        if (_state.AnnexedByVillageId is not null)
        {
            Timers.Cancel(NatureRegenTimerKey);
            return;
        }

        if (_state.NextNatureRegenAtMs <= nowMs)
        {
            _state.NextNatureRegenAtMs = nowMs + Math.Max(1000L, _state.NatureRegenIntervalSeconds * 1000L);
        }

        long delayMs = Math.Max(1L, _state.NextNatureRegenAtMs - nowMs);
        Timers.StartSingleTimer(
            NatureRegenTimerKey,
            _state.NextNatureRegenAtMs,
            TimeSpan.FromMilliseconds(delayMs));
    }
}
