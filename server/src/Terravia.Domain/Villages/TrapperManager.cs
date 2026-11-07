using Terravia.Domain.Catalog;

namespace Terravia.Domain.Villages;

/// <summary>
/// Orchestrates Gaul Trapper (gid 36) capacity across multiple Trappers, pre-combat hostile army interception
/// (including Reconnaissance bypass, Archon Capital 100% auto-trap, Hero escape on partial capture, and remainder clamping),
/// and the 3 prisoner release/liberation/dissolution paths with automatic 0-cost trap repairs (FR-013..FR-015).
/// </summary>
public sealed class TrapperManager
{
    private const int AttackTypeReconnaissance = 1;

    private readonly IGameCatalog _catalog;
    private readonly ITrainingTimeCalculator _timeCalculator;
    private readonly MilitaryTrainingManager _trainingManager;

    public TrapperManager(
        IGameCatalog? catalog = null,
        ITrainingTimeCalculator? timeCalculator = null,
        MilitaryTrainingManager? trainingManager = null)
    {
        _catalog = catalog ?? FrozenGameCatalog.Default;
        _timeCalculator = timeCalculator ?? new TrainingTimeCalculator(_catalog);
        _trainingManager = trainingManager ?? new MilitaryTrainingManager(_catalog, _timeCalculator);
    }

    public int CalculateTotalTrapCapacity(ReadOnlySpan<int> slotBuildingTypes, ReadOnlySpan<int> slotLevels)
    {
        int total = 0;
        int len = Math.Min(slotBuildingTypes.Length, slotLevels.Length);
        for (int s = VillageConsts.MinInnerBuildingSlotId; s < len && s <= VillageConsts.MaxStandardSlotId; s++)
        {
            if (slotBuildingTypes[s] == 36 && slotLevels[s] > 0)
            {
                total += (int)_catalog.GetBuildingLevelStats(36, slotLevels[s]).AttributeValue;
            }
        }
        return total;
    }

    public int GetQueuedTrapsCount(VillageState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        int queued = 0;
        foreach (var q in state.TrainingQueues)
        {
            if (q.UnitTypeId == CatalogConsts.TrapUnitId)
            {
                queued += q.AmountRemaining;
            }
        }
        return queued;
    }

    public TrapInterceptionResult InterceptIncomingArmy(
        VillageState defenderState,
        int attackType,
        int attackerVillageId,
        int attackerPlayerId,
        int attackerAllianceId,
        int attackerTribeId,
        ReadOnlySpan<int> incomingUnitsBySlot,
        bool incomingHasHero,
        long nextPrisonerStackId)
    {
        ArgumentNullException.ThrowIfNull(defenderState);

        int[] remainingBySlot = new int[11];
        int[] capturedBySlot = new int[11];
        int totalRegularTroops = 0;

        for (int i = 1; i <= 10; i++)
        {
            int c = i < incomingUnitsBySlot.Length ? Math.Max(0, incomingUnitsBySlot[i]) : 0;
            remainingBySlot[i] = c;
            totalRegularTroops += c;
        }

        int totalTroopsWithHero = totalRegularTroops + (incomingHasHero ? 1 : 0);
        if (totalTroopsWithHero <= 0)
        {
            return new TrapInterceptionResult(remainingBySlot, false, capturedBySlot, false, 0, null);
        }

        // Archon Capital Auto-Trap (FR-014): 100% of all incoming units (including scouts) are trapped
        bool isArchonCapital = defenderState.IsArchonVillage && defenderState.IsCapital;
        if (!isArchonCapital)
        {
            // Reconnaissance/Scouting missions (AttackType = 1) bypass Gaul Trappers completely
            if (attackType == AttackTypeReconnaissance)
            {
                return new TrapInterceptionResult(remainingBySlot, incomingHasHero, capturedBySlot, false, 0, null);
            }

            int freeTraps = Math.Max(0, defenderState.TotalTrapsCount - defenderState.OccupiedTrapsCount);
            if (freeTraps <= 0)
            {
                return new TrapInterceptionResult(remainingBySlot, incomingHasHero, capturedBySlot, false, 0, null);
            }

            if (freeTraps >= totalTroopsWithHero)
            {
                // Full Army Capture (freeTraps >= TotalTroops): every unit including Hero is captured
                for (int i = 1; i <= 10; i++)
                {
                    capturedBySlot[i] = remainingBySlot[i];
                    remainingBySlot[i] = 0;
                }

                var fullStack = UpsertPrisonerStack(
                    defenderState,
                    attackerVillageId,
                    attackerPlayerId,
                    attackerAllianceId,
                    attackerTribeId,
                    capturedBySlot,
                    incomingHasHero,
                    nextPrisonerStackId);

                defenderState.OccupiedTrapsCount += totalTroopsWithHero;
                return new TrapInterceptionResult(remainingBySlot, false, capturedBySlot, incomingHasHero, totalTroopsWithHero, fullStack);
            }

            // Partial Army Capture (freeTraps < TotalTroops): Hero (t11) escapes capture (trapped[11] = 0)
            int sumCaptured = 0;
            for (int i = 1; i <= 10; i++)
            {
                if (remainingBySlot[i] > 0)
                {
                    int share = (int)Math.Floor(remainingBySlot[i] * ((double)freeTraps / totalTroopsWithHero));
                    capturedBySlot[i] = Math.Min(remainingBySlot[i], share);
                    sumCaptured += capturedBySlot[i];
                }
            }

            // Remainder distribution +1 sequentially 1..10 clamped by captured[i] < count[i]
            int rem = Math.Min(freeTraps - sumCaptured, totalRegularTroops - sumCaptured);
            while (rem > 0)
            {
                bool anyAdded = false;
                for (int i = 1; i <= 10 && rem > 0; i++)
                {
                    if (capturedBySlot[i] < remainingBySlot[i])
                    {
                        capturedBySlot[i]++;
                        sumCaptured++;
                        rem--;
                        anyAdded = true;
                    }
                }

                if (!anyAdded)
                {
                    break;
                }
            }

            for (int i = 1; i <= 10; i++)
            {
                remainingBySlot[i] -= capturedBySlot[i];
            }

            if (sumCaptured <= 0)
            {
                return new TrapInterceptionResult(remainingBySlot, incomingHasHero, capturedBySlot, false, 0, null);
            }

            var partialStack = UpsertPrisonerStack(
                defenderState,
                attackerVillageId,
                attackerPlayerId,
                attackerAllianceId,
                attackerTribeId,
                capturedBySlot,
                heroCaptured: false,
                nextPrisonerStackId);

            defenderState.OccupiedTrapsCount += sumCaptured;
            return new TrapInterceptionResult(remainingBySlot, incomingHasHero, capturedBySlot, false, sumCaptured, partialStack);
        }
        else
        {
            // Archon Capital 100% auto-trap
            for (int i = 1; i <= 10; i++)
            {
                capturedBySlot[i] = remainingBySlot[i];
                remainingBySlot[i] = 0;
            }

            var archonStack = UpsertPrisonerStack(
                defenderState,
                attackerVillageId,
                attackerPlayerId,
                attackerAllianceId,
                attackerTribeId,
                capturedBySlot,
                incomingHasHero,
                nextPrisonerStackId);

            defenderState.TotalTrapsCount = Math.Max(defenderState.TotalTrapsCount, defenderState.OccupiedTrapsCount + totalTroopsWithHero);
            defenderState.OccupiedTrapsCount += totalTroopsWithHero;
            return new TrapInterceptionResult(remainingBySlot, false, capturedBySlot, incomingHasHero, totalTroopsWithHero, archonStack);
        }
    }

    public CombatPrisonerLiberationResult LiberatePrisonersOnSuccessfulNormalAttack(
        VillageState defenderState,
        int attackerVillageId,
        int attackerPlayerId,
        int attackerAllianceId,
        IReadOnlySet<int>? alliedOrNapAllianceIds,
        long nowMs)
    {
        ArgumentNullException.ThrowIfNull(defenderState);

        var outcomes = new List<PrisonerLiberationOutcome>();
        int[] mergedIntoAttackerReturn = new int[11];
        bool mergedHero = false;
        int totalFreedTraps = 0;

        var matchingStacks = defenderState.HeldPrisoners
            .Where(p =>
                p.OwnerPlayerId == attackerPlayerId ||
                (attackerAllianceId > 0 && p.OwnerAllianceId == attackerAllianceId) ||
                (p.OwnerAllianceId > 0 && alliedOrNapAllianceIds is not null && alliedOrNapAllianceIds.Contains(p.OwnerAllianceId)))
            .ToList();

        if (matchingStacks.Count == 0)
        {
            return new CombatPrisonerLiberationResult(outcomes, mergedIntoAttackerReturn, false, 0, 0);
        }

        foreach (var stack in matchingStacks)
        {
            int freedInStack = stack.TotalOccupiedTraps;
            totalFreedTraps += freedInStack;

            int[] survivingBySlot = new int[11];
            for (int i = 1; i <= 10; i++)
            {
                int count = stack.UnitCountsBySlot[i];
                if (count > 0)
                {
                    int dead = (int)Math.Round(count / 4.0, MidpointRounding.AwayFromZero);
                    survivingBySlot[i] = Math.Max(0, count - dead);
                }
            }

            // Single Hero (count = 1): round(1 / 4) = 0 dead -> Hero always survives combat liberation
            bool heroSurvived = stack.HasCapturedHero;
            bool isSameAttackingVillage = stack.OwnerVillageId == attackerVillageId;

            if (isSameAttackingVillage)
            {
                for (int i = 1; i <= 10; i++)
                {
                    mergedIntoAttackerReturn[i] += survivingBySlot[i];
                }
                if (heroSurvived)
                {
                    mergedHero = true;
                }
            }

            outcomes.Add(new PrisonerLiberationOutcome(
                PrisonerStackId: stack.PrisonerStackId,
                OwnerVillageId: stack.OwnerVillageId,
                SurvivingUnitsBySlot: survivingBySlot,
                HeroSurvived: heroSurvived,
                MergedIntoAttackingArmyReturn: isSameAttackingVillage,
                FreedTrapCount: freedInStack));

            defenderState.HeldPrisoners.Remove(stack);
            defenderState.TroopStacks.RemoveAll(s => s.Tier == StarvationPriorityTier.TrappedPrisoner && s.StackOrMovementId == stack.PrisonerStackId);
        }

        defenderState.TotalTrapsCount = Math.Max(0, defenderState.TotalTrapsCount - totalFreedTraps);
        defenderState.OccupiedTrapsCount = Math.Max(0, defenderState.OccupiedTrapsCount - totalFreedTraps);

        // Canonical 1/3 free trap repair: round(totalFreed / 3) (Clarification Q5)
        int rawFreeRepairs = (int)Math.Round(totalFreedTraps / 3.0, MidpointRounding.AwayFromZero);
        int queuedFreeRepairs = EnqueueFreeTrapRepairs(defenderState, rawFreeRepairs, nowMs);

        return new CombatPrisonerLiberationResult(
            LiberatedStacks: outcomes,
            MergedIntoAttackerReturnBySlot: mergedIntoAttackerReturn,
            MergedHeroIntoAttackerReturn: mergedHero,
            TotalDestroyedTraps: totalFreedTraps,
            FreeAutoRepairedTrapsQueued: queuedFreeRepairs);
    }

    public (CommandResult Result, PrisonerLiberationOutcome? Outcome, int FreeRepairsQueued) ReleasePrisonersByTrapOwner(
        VillageState defenderState,
        ReleasePrisonersCommand command,
        long nowMs,
        SitterPermissions? sitterPermissions = null)
    {
        ArgumentNullException.ThrowIfNull(defenderState);
        ArgumentNullException.ThrowIfNull(command);

        if (sitterPermissions.HasValue && !sitterPermissions.Value.HasFlag(SitterPermissions.Reinforce))
        {
            return (CommandResult.Fail(TerraviaDomainErrorCodes.Sitters.PermissionDenied, "Sitter lacks Reinforce permission to release prisoners."), null, 0);
        }

        var stack = defenderState.HeldPrisoners.FirstOrDefault(p => p.PrisonerStackId == command.PrisonerStackId);
        if (stack is null)
        {
            return (CommandResult.Fail(TerraviaDomainErrorCodes.Villages.PrisonerStackNotFound, "Prisoner stack not found."), null, 0);
        }

        int totalFreed = stack.TotalOccupiedTraps;
        int[] surviving = new int[11];
        Array.Copy(stack.UnitCountsBySlot, surviving, 11);

        var outcome = new PrisonerLiberationOutcome(
            PrisonerStackId: stack.PrisonerStackId,
            OwnerVillageId: stack.OwnerVillageId,
            SurvivingUnitsBySlot: surviving,
            HeroSurvived: stack.HasCapturedHero,
            MergedIntoAttackingArmyReturn: false,
            FreedTrapCount: totalFreed);

        defenderState.HeldPrisoners.Remove(stack);
        defenderState.TroopStacks.RemoveAll(s => s.Tier == StarvationPriorityTier.TrappedPrisoner && s.StackOrMovementId == stack.PrisonerStackId);

        defenderState.TotalTrapsCount = Math.Max(0, defenderState.TotalTrapsCount - totalFreed);
        defenderState.OccupiedTrapsCount = Math.Max(0, defenderState.OccupiedTrapsCount - totalFreed);

        // Path B: 100% of released traps are queued for free repair
        int queuedRepairs = EnqueueFreeTrapRepairs(defenderState, totalFreed, nowMs);

        return (CommandResult.Success, outcome, queuedRepairs);
    }

    public (CommandResult Result, int DissolvedCount, int FreeRepairsQueued) DissolvePrisonersByTroopOwner(
        VillageState defenderState,
        DissolvePrisonersCommand command,
        long nowMs,
        SitterPermissions? sitterPermissions = null)
    {
        ArgumentNullException.ThrowIfNull(defenderState);
        ArgumentNullException.ThrowIfNull(command);

        if (sitterPermissions.HasValue && !sitterPermissions.Value.HasFlag(SitterPermissions.Reinforce))
        {
            return (CommandResult.Fail(TerraviaDomainErrorCodes.Sitters.PermissionDenied, "Sitter lacks Reinforce permission to dissolve prisoners."), 0, 0);
        }

        var stack = defenderState.HeldPrisoners.FirstOrDefault(
            p => p.PrisonerStackId == command.PrisonerStackId && p.OwnerPlayerId == command.OwnerPlayerId);

        if (stack is null)
        {
            return (CommandResult.Fail(TerraviaDomainErrorCodes.Villages.PrisonerStackNotFound, "Prisoner stack not found."), 0, 0);
        }

        int totalFreed = stack.TotalOccupiedTraps;
        defenderState.HeldPrisoners.Remove(stack);
        defenderState.TroopStacks.RemoveAll(s => s.Tier == StarvationPriorityTier.TrappedPrisoner && s.StackOrMovementId == stack.PrisonerStackId);

        defenderState.TotalTrapsCount = Math.Max(0, defenderState.TotalTrapsCount - totalFreed);
        defenderState.OccupiedTrapsCount = Math.Max(0, defenderState.OccupiedTrapsCount - totalFreed);

        // Path C: round(totalFreed / 3) free trap repairs queued in Gaul village
        int rawFreeRepairs = (int)Math.Round(totalFreed / 3.0, MidpointRounding.AwayFromZero);
        int queuedRepairs = EnqueueFreeTrapRepairs(defenderState, rawFreeRepairs, nowMs);

        return (CommandResult.Success, totalFreed, queuedRepairs);
    }

    private int EnqueueFreeTrapRepairs(VillageState defenderState, int requestedRepairs, long nowMs)
    {
        if (requestedRepairs <= 0)
        {
            return 0;
        }

        int highestTrapperLevel = GetHighestBuildingLevel(defenderState, 36);
        if (highestTrapperLevel <= 0)
        {
            return 0;
        }

        int totalTrapCap = CalculateTotalTrapCapacity(defenderState.SlotBuildingTypes, defenderState.SlotLevels);
        int currentlyQueued = GetQueuedTrapsCount(defenderState);
        int availableCap = Math.Max(0, totalTrapCap - defenderState.TotalTrapsCount - currentlyQueued);
        int clampedRepairs = Math.Min(requestedRepairs, availableCap);
        if (clampedRepairs <= 0)
        {
            return 0;
        }

        var speedCtx = new TrainingSpeedContext(
            UnitTypeId: CatalogConsts.TrapUnitId,
            FacilityCategory: TrainingFacilityCategory.Trapper,
            FacilityLevel: highestTrapperLevel,
            TrainerArtifactMultiplier: defenderState.TrainerArtifactMultiplier,
            HasStationedLivingHero: defenderState.HasStationedLivingHero,
            StationedHeroHelmet: defenderState.StationedHeroHelmet,
            AllianceRecruitmentBonusLevel: defenderState.AllianceRecruitmentBonusLevel,
            AllianceJoinedAtMs: defenderState.AllianceJoinedAtMs,
            CurrentTimestampMs: nowMs,
            ServerSpeed: defenderState.ServerSpeed);

        int eachTimeSeconds = _timeCalculator.CalculateEachTimeSeconds(in speedCtx);
        _trainingManager.AppendOrMergeQueueEntry(
            defenderState,
            TrainingFacilityCategory.Trapper,
            CatalogConsts.TrapUnitId,
            clampedRepairs,
            popUpkeepPerUnit: 0,
            eachTimeSeconds: eachTimeSeconds,
            nowMs: nowMs);

        return clampedRepairs;
    }

    private static PrisonerStack UpsertPrisonerStack(
        VillageState defenderState,
        int attackerVillageId,
        int attackerPlayerId,
        int attackerAllianceId,
        int attackerTribeId,
        int[] capturedBySlot,
        bool heroCaptured,
        long nextPrisonerStackId)
    {
        var existing = defenderState.HeldPrisoners.FirstOrDefault(p => p.OwnerVillageId == attackerVillageId);
        if (existing is not null)
        {
            for (int i = 1; i <= 10; i++)
            {
                existing.UnitCountsBySlot[i] += capturedBySlot[i];
            }
            if (heroCaptured)
            {
                existing.HasCapturedHero = true;
            }
            return existing;
        }

        var stack = new PrisonerStack
        {
            PrisonerStackId = nextPrisonerStackId,
            CaptorVillageId = defenderState.VillageId,
            OwnerVillageId = attackerVillageId,
            OwnerPlayerId = attackerPlayerId,
            OwnerAllianceId = attackerAllianceId,
            OwnerTribeId = attackerTribeId,
            HasCapturedHero = heroCaptured
        };
        Array.Copy(capturedBySlot, stack.UnitCountsBySlot, 11);
        defenderState.HeldPrisoners.Add(stack);
        return stack;
    }

    private static int GetHighestBuildingLevel(VillageState state, int buildingTypeId)
    {
        int max = 0;
        for (int s = VillageConsts.MinInnerBuildingSlotId; s <= VillageConsts.MaxStandardSlotId; s++)
        {
            if (state.SlotBuildingTypes[s] == buildingTypeId && state.SlotLevels[s] > max)
            {
                max = state.SlotLevels[s];
            }
        }
        return max;
    }
}
