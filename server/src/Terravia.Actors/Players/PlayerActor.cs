using Akka.Actor;
using Terravia.Actors.Villages;
using Terravia.Domain;
using Terravia.Domain.Players;
using Terravia.Domain.Villages;

namespace Terravia.Actors.Players;

/// <summary>
/// Akka.NET Aggregate Root Actor managing a player's empire-wide <see cref="PlayerCultureLedger"/>,
/// celebration CP awards, Teuton Mead Festival windows, Capital transfer orchestration, and
/// highest-population Capital reassignment on Capital razing.
/// </summary>
public sealed class PlayerActor : ReceiveActor
{
    public sealed record UpsertPlayerVillageSummaryCommand(
        int VillageId,
        int CulturePointsPerDay,
        int Population,
        bool IsArchon,
        long NowMs,
        IActorRef? VillageActorRef = null);

    public sealed record AwardCelebrationCulturePointsCommand(
        int VillageId,
        int AwardedCulturePoints,
        long NowMs);

    public sealed record ActivateMeadFestivalWindowCommand(
        int CapitalVillageId,
        int BreweryLevel,
        long EndsAtMs,
        long NowMs);

    public sealed record HandleVillageRazedOrConqueredCommand(
        int VillageId,
        long NowMs);

    public sealed record GetPlayerCultureSnapshotQuery(
        long NowMs,
        CulturePointCurveMode CurveMode = CulturePointCurveMode.Standard);

    public sealed record GetPlayerCultureLedgerQuery(long NowMs);

    public sealed record ApplyCapitalTransferPlanToVillageCommand(
        CapitalTransferPlan Plan,
        bool IsOldCapital,
        long NowMs);

    public sealed record PromoteVillageToCapitalOnRazingCommand(
        int VillageId,
        long NowMs);

    private sealed class TrackedVillageSummary
    {
        public int VillageId { get; init; }
        public int CulturePointsPerDay { get; set; }
        public int Population { get; set; }
        public bool IsArchon { get; set; }
        public IActorRef? ActorRef { get; set; }
    }

    private readonly PlayerCultureLedger _ledger;
    private readonly VillageExpansionManager _expansionManager;
    private readonly IActorRef? _persistenceObserver;
    private readonly IActorRef? _signalRBridgeObserver;
    private readonly Dictionary<int, TrackedVillageSummary> _villages = new();

    public long MeadFestivalEndsAtMs { get; private set; }
    public int MeadFestivalBreweryLevel { get; private set; }

    public PlayerActor(
        PlayerCultureLedger initialLedger,
        VillageExpansionManager? expansionManager = null,
        IActorRef? persistenceObserver = null,
        IActorRef? signalRBridgeObserver = null)
    {
        _ledger = initialLedger ?? throw new ArgumentNullException(nameof(initialLedger));
        _expansionManager = expansionManager ?? new VillageExpansionManager();
        _persistenceObserver = persistenceObserver;
        _signalRBridgeObserver = signalRBridgeObserver;

        RegisterCommandHandlers();
    }

    public static Props Props(
        PlayerCultureLedger initialLedger,
        VillageExpansionManager? expansionManager = null,
        IActorRef? persistenceObserver = null,
        IActorRef? signalRBridgeObserver = null) =>
        Akka.Actor.Props.Create(() => new PlayerActor(
            initialLedger,
            expansionManager,
            persistenceObserver,
            signalRBridgeObserver));

    private void RegisterCommandHandlers()
    {
        Receive<UpsertPlayerVillageSummaryCommand>(cmd =>
        {
            _ledger.AccrueCulturePointsUpTo(cmd.NowMs);

            if (!_villages.TryGetValue(cmd.VillageId, out var summary))
            {
                summary = new TrackedVillageSummary { VillageId = cmd.VillageId };
                _villages[cmd.VillageId] = summary;
            }

            summary.CulturePointsPerDay = cmd.IsArchon ? 0 : Math.Max(0, cmd.CulturePointsPerDay);
            summary.Population = Math.Max(0, cmd.Population);
            summary.IsArchon = cmd.IsArchon;
            if (cmd.VillageActorRef is not null)
            {
                summary.ActorRef = cmd.VillageActorRef;
            }

            RecalculateVillageTotals();
            Sender.Tell(CommandResult.Success);
        });

        Receive<AwardCelebrationCulturePointsCommand>(cmd =>
        {
            _ledger.AccrueCulturePointsUpTo(cmd.NowMs);
            _ledger.AwardCelebrationCulturePoints(cmd.AwardedCulturePoints);
            NotifyCultureSnapshot(cmd.NowMs);
            Sender.Tell(CommandResult.Success);
        });

        Receive<ActivateMeadFestivalWindowCommand>(cmd =>
        {
            _ledger.AccrueCulturePointsUpTo(cmd.NowMs);
            MeadFestivalBreweryLevel = cmd.BreweryLevel;
            MeadFestivalEndsAtMs = cmd.EndsAtMs;
            Sender.Tell(CommandResult.Success);
        });

        ReceiveAsync<VillageActorCommandEnvelope<ChangeCapitalVillageCommand>>(async env =>
        {
            var result = await HandleChangeCapitalAsync(env.Command, env.NowMs, env.SitterPermissions);
            Sender.Tell(result);
        });

        ReceiveAsync<ChangeCapitalVillageCommand>(async cmd =>
        {
            var result = await HandleChangeCapitalAsync(cmd, _ledger.LastCpUpdatedMs, null);
            Sender.Tell(result);
        });

        Receive<HandleVillageRazedOrConqueredCommand>(cmd =>
        {
            _ledger.AccrueCulturePointsUpTo(cmd.NowMs);
            _villages.Remove(cmd.VillageId);
            RecalculateVillageTotals();

            if (_ledger.CapitalVillageId == cmd.VillageId)
            {
                int oldCapitalId = _ledger.CapitalVillageId;
                var replacement = _villages.Values
                    .Where(v => !v.IsArchon)
                    .OrderByDescending(v => v.Population)
                    .ThenBy(v => v.VillageId)
                    .FirstOrDefault();

                int newCapitalId = replacement?.VillageId ?? 0;
                _ledger.CapitalVillageId = newCapitalId;

                if (replacement?.ActorRef is not null)
                {
                    replacement.ActorRef.Tell(new PromoteVillageToCapitalOnRazingCommand(newCapitalId, cmd.NowMs));
                }

                var eto = new CapitalVillageTransferredEto(
                    PlayerId: _ledger.PlayerId,
                    OldCapitalVillageId: oldCapitalId,
                    NewCapitalVillageId: newCapitalId,
                    IsAutomaticReassignmentOnRazing: true,
                    TimestampMs: cmd.NowMs);

                _persistenceObserver?.Tell(eto);
                _signalRBridgeObserver?.Tell(eto);
            }

            NotifyCultureSnapshot(cmd.NowMs);
            Sender.Tell(CommandResult.Success);
        });

        Receive<GetPlayerCultureSnapshotQuery>(q =>
        {
            _ledger.AccrueCulturePointsUpTo(q.NowMs);
            Sender.Tell(BuildSnapshot(q.NowMs, q.CurveMode));
        });

        Receive<GetPlayerCultureLedgerQuery>(q =>
        {
            _ledger.AccrueCulturePointsUpTo(q.NowMs);
            Sender.Tell(_ledger);
        });
    }

    private async Task<CommandResult> HandleChangeCapitalAsync(
        ChangeCapitalVillageCommand cmd,
        long nowMs,
        SitterPermissions? sitterPermissions)
    {
        if (sitterPermissions.HasValue || cmd.ActorPlayerId != cmd.OwnerPlayerId)
        {
            return CommandResult.Fail(
                TerraviaDomainErrorCodes.Sitters.PermissionDenied,
                "Sitters are not permitted to transfer a player's Capital village.");
        }

        _ledger.AccrueCulturePointsUpTo(nowMs);

        int oldCapitalId = _ledger.CapitalVillageId;
        int newCapitalId = cmd.NewCapitalVillageId;

        if (!_villages.TryGetValue(newCapitalId, out var newCapSummary) || newCapSummary.ActorRef is null)
        {
            return CommandResult.Fail(
                TerraviaDomainErrorCodes.Villages.CapitalRequiresPalaceOrCommandCenter,
                "Target village is not registered with PlayerActor.");
        }

        if (!_villages.TryGetValue(oldCapitalId, out var oldCapSummary) || oldCapSummary.ActorRef is null)
        {
            return CommandResult.Fail(
                TerraviaDomainErrorCodes.Villages.CapitalRequiresPalaceOrCommandCenter,
                "Current Capital village is not registered with PlayerActor.");
        }

        var oldState = await oldCapSummary.ActorRef.Ask<VillageState>(new GetVillageStateQuery(), TimeSpan.FromSeconds(5));
        var newState = await newCapSummary.ActorRef.Ask<VillageState>(new GetVillageStateQuery(), TimeSpan.FromSeconds(5));

        var oldCapState = new VillageCapitalState(
            oldState.VillageId,
            oldState.IsCapital,
            oldState.IsArchonVillage,
            (int[])oldState.SlotBuildingTypes.Clone(),
            (int[])oldState.SlotLevels.Clone());

        var newCapState = new VillageCapitalState(
            newState.VillageId,
            newState.IsCapital,
            newState.IsArchonVillage,
            (int[])newState.SlotBuildingTypes.Clone(),
            (int[])newState.SlotLevels.Clone());

        var plan = _expansionManager.BuildCapitalTransferPlan(oldCapState, newCapState);
        if (!plan.Succeeded)
        {
            return CommandResult.Fail(plan.ErrorCode ?? TerraviaDomainErrorCodes.Villages.CapitalRequiresPalaceOrCommandCenter);
        }

        await oldCapSummary.ActorRef.Ask<CommandResult>(
            new ApplyCapitalTransferPlanToVillageCommand(plan, IsOldCapital: true, nowMs),
            TimeSpan.FromSeconds(5));

        await newCapSummary.ActorRef.Ask<CommandResult>(
            new ApplyCapitalTransferPlanToVillageCommand(plan, IsOldCapital: false, nowMs),
            TimeSpan.FromSeconds(5));

        _ledger.CapitalVillageId = newCapitalId;

        var eto = new CapitalVillageTransferredEto(
            PlayerId: _ledger.PlayerId,
            OldCapitalVillageId: oldCapitalId,
            NewCapitalVillageId: newCapitalId,
            IsAutomaticReassignmentOnRazing: false,
            TimestampMs: nowMs);

        _persistenceObserver?.Tell(eto);
        _signalRBridgeObserver?.Tell(eto);
        NotifyCultureSnapshot(nowMs);

        return CommandResult.Success;
    }

    private void RecalculateVillageTotals()
    {
        int totalCp = 0;
        int count = 0;
        foreach (var v in _villages.Values)
        {
            count++;
            if (!v.IsArchon)
            {
                totalCp += Math.Max(0, v.CulturePointsPerDay);
            }
        }

        _ledger.TotalVillageBaseCpPerDay = totalCp;
        _ledger.OwnedVillageCount = Math.Max(1, count);
    }

    private PlayerCultureSnapshotDto BuildSnapshot(long nowMs, CulturePointCurveMode curveMode)
    {
        int nextVillageNumber = Math.Min(
            PlayerConsts.MaxCulturePointTableVillageNumber,
            Math.Max(2, _ledger.OwnedVillageCount + 1));
        int nextRequiredCp = _expansionManager.GetRequiredCulturePointsForVillageNumber(nextVillageNumber, curveMode);

        return new PlayerCultureSnapshotDto(
            PlayerId: _ledger.PlayerId,
            AccumulatedCulturePoints: _ledger.AccumulatedCulturePoints,
            DailyCulturePointsRate: _ledger.EffectiveDailyCulturePointsRate,
            LastCpUpdatedServerMs: nowMs,
            OwnedVillageCount: _ledger.OwnedVillageCount,
            NextVillageRequiredCp: nextRequiredCp,
            CapitalVillageId: _ledger.CapitalVillageId);
    }

    private void NotifyCultureSnapshot(long nowMs)
    {
        _signalRBridgeObserver?.Tell(BuildSnapshot(nowMs, CulturePointCurveMode.Standard));
    }
}
