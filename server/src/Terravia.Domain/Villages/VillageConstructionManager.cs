using Terravia.Domain.Catalog;

namespace Terravia.Domain.Villages;

/// <summary>
/// Pure deterministic domain manager orchestrating village construction queues, Roman dual-build partitioning,
/// Plus waiting loops, Master Builder reservations, Gold Finish All, and building demolition.
/// </summary>
public sealed class VillageConstructionManager
{
    private readonly IGameCatalog _catalog;
    private readonly IBuildingRuleCatalog _ruleCatalog;
    private readonly IConstructionCalculator _calculator;
    private long _nextQueueItemId = 1;

    public VillageConstructionManager(
        IGameCatalog catalog,
        IBuildingRuleCatalog ruleCatalog,
        IConstructionCalculator calculator)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _ruleCatalog = ruleCatalog ?? throw new ArgumentNullException(nameof(ruleCatalog));
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
    }

    public VillageConstructionContext CreateEvaluationContext(VillageState state, int targetSlotId)
    {
        int activeField = 0;
        int activeInner = 0;
        int waitingLoop = 0;
        int masterBuilder = 0;
        int queuedForSlot = 0;
        int maxQueuedWwLevel = state.WorldWonderLevel;
        bool hasCroplandQueued = false;

        foreach (var item in state.ConstructionQueue)
        {
            if (item.SlotId == targetSlotId)
                queuedForSlot++;
            if (item.BuildingTypeId == 4)
                hasCroplandQueued = true;
            if (item.BuildingTypeId == 40 && item.TargetLevel > maxQueuedWwLevel)
                maxQueuedWwLevel = item.TargetLevel;

            if (item.IsMasterBuilder)
            {
                masterBuilder++;
            }
            else if (item.IsWaitingLoop)
            {
                waitingLoop++;
            }
            else if (item.IsResourceField)
            {
                activeField++;
            }
            else
            {
                activeInner++;
            }
        }

        var prereqCtx = new VillagePrerequisiteContext(
            TribeId: state.TribeId,
            IsCapital: state.IsCapital,
            IsArchonVillage: state.IsArchonVillage,
            IsWorldWonderVillage: state.IsWorldWonderVillage || state.WorldWonderBuildingType == 40,
            HasAccountPalace: state.HasAccountPalace,
            HasGreatStorageArtifact: state.HasGreatStorageArtifact,
            OwnedWwPlanCount: state.OwnedWwPlanCount,
            AllianceWwPlanCount: state.AllianceWwPlanCount,
            GreatWorkshopEnabled: state.GreatWorkshopEnabled,
            SlotBuildingTypes: state.SlotBuildingTypes,
            SlotLevels: state.SlotLevels,
            WorldWonderLevel: state.WorldWonderLevel,
            MaxQueuedWorldWonderLevel: maxQueuedWwLevel);

        return new VillageConstructionContext(
            PrerequisiteContext: prereqCtx,
            AvailableWood: state.Wood,
            AvailableClay: state.Clay,
            AvailableIron: state.Iron,
            AvailableCrop: state.Crop,
            WarehouseCapacity: state.WarehouseCapacity,
            GranaryCapacity: state.GranaryCapacity,
            NetCropProductionPerHour: state.NetCropProductionPerHour,
            HasPlusSubscription: state.HasPlusSubscription,
            AllowAllTribeRomanQueue: state.AllowAllTribeRomanQueue,
            MaxMasterBuilderSlots: state.MaxMasterBuilderSlots,
            ActiveResourceFieldJobs: activeField,
            ActiveInnerBuildingJobs: activeInner,
            WaitingLoopJobs: waitingLoop,
            MasterBuilderJobs: masterBuilder,
            QueuedJobsForTargetSlot: queuedForSlot,
            HasCroplandUpgradeQueued: hasCroplandQueued,
            ActiveDemolitionSlotId: state.ActiveDemolition?.SlotId,
            ServerSpeed: state.ServerSpeed);
    }

    public (CommandResult Result, ConstructionQueueItem? QueuedItem, bool UsedGold) EnqueueConstruction(
        VillageState state,
        ConstructOrUpgradeBuildingCommand command,
        long nowMs,
        SitterPermissions? sitterPermissions = null)
    {
        if (command.ActorPlayerId != command.OwnerPlayerId &&
            sitterPermissions is { } perms &&
            !perms.HasFlag(TerraviaPermissions.Sitter.BuildMask))
        {
            return (CommandResult.Fail(TerraviaDomainErrorCodes.Sitters.PermissionDenied, "Sitter lacks build permission."), null, false);
        }

        var evalCtx = CreateEvaluationContext(state, command.SlotId);
        var eval = _ruleCatalog.EvaluateSlotUpgrade(command.SlotId, command.BuildingTypeId, in evalCtx);

        if (!eval.IsAllowed)
        {
            return (CommandResult.Fail(eval.Status.ToString()), null, false);
        }

        // If caller explicitly requested Master Builder, or if evaluation says CanReserveMasterBuilder
        bool placeInMasterBuilder = eval.Status == BuildEligibilityStatus.CanReserveMasterBuilder ||
                                    (command.UseMasterBuilder && evalCtx.MasterBuilderJobs < state.MaxMasterBuilderSlots);

        if (command.UseMasterBuilder && evalCtx.MasterBuilderJobs >= state.MaxMasterBuilderSlots)
        {
            return (CommandResult.Fail(BuildEligibilityStatus.QueueFull.ToString()), null, false);
        }

        long queueId = _nextQueueItemId++;
        bool isWaitingLoop = !placeInMasterBuilder && eval.Status == BuildEligibilityStatus.CanBuildWaitingLoop;

        long startedAtMs = 0;
        long finishesAtMs = eval.BuildTimeSeconds * 1000L;

        if (!placeInMasterBuilder)
        {
            // Deduct resources immediately for active and Plus waiting-loop jobs
            state.Wood -= eval.RequiredCost.Wood;
            state.Clay -= eval.RequiredCost.Clay;
            state.Iron -= eval.RequiredCost.Iron;
            state.Crop -= eval.RequiredCost.Crop;

            if (isWaitingLoop)
            {
                long precedingFinishMs = FindPrecedingPartitionFinishMs(state, command.SlotId, nowMs);
                long loopDelayMs = (long)Math.Ceiling(60.0 / state.ServerSpeed) * 1000L;
                startedAtMs = precedingFinishMs + loopDelayMs;
                finishesAtMs = startedAtMs + eval.BuildTimeSeconds * 1000L;
            }
            else
            {
                startedAtMs = nowMs;
                finishesAtMs = nowMs + eval.BuildTimeSeconds * 1000L;
            }
        }

        // Reserve the slot's BuildingTypeId if currently 0
        if (state.GetSlotBuildingType(command.SlotId) == 0)
        {
            state.SetSlot(command.SlotId, command.BuildingTypeId, 0);
        }

        var item = new ConstructionQueueItem(
            QueueItemId: queueId,
            VillageId: state.VillageId,
            SlotId: command.SlotId,
            BuildingTypeId: command.BuildingTypeId,
            TargetLevel: eval.TargetLevel,
            IsWaitingLoop: isWaitingLoop,
            IsMasterBuilder: placeInMasterBuilder,
            StartedAtMs: startedAtMs,
            FinishesAtMs: finishesAtMs,
            DeductedOrRequiredCost: eval.RequiredCost,
            BuildDurationSeconds: eval.BuildTimeSeconds);

        state.ConstructionQueue.Add(item);

        // If placed in Master Builder, check if it can immediately promote into an active slot
        bool usedGold = false;
        if (placeInMasterBuilder)
        {
            usedGold = TryPromoteMasterBuilderJobs(state, nowMs);
        }

        return (CommandResult.Success, state.ConstructionQueue.First(q => q.QueueItemId == queueId), usedGold);
    }

    public bool CompleteDueConstructionsAndPromote(VillageState state, long nowMs)
    {
        bool usedGold = false;
        bool anyCompleted;

        do
        {
            anyCompleted = false;
            var dueItems = state.ConstructionQueue
                .Where(q => !q.IsMasterBuilder && q.FinishesAtMs <= nowMs)
                .OrderBy(q => q.FinishesAtMs)
                .ThenBy(q => q.QueueItemId)
                .ToList();

            foreach (var item in dueItems)
            {
                state.ConstructionQueue.Remove(item);
                ApplyCompletedBuildingLevel(state, item.SlotId, item.BuildingTypeId, item.TargetLevel);
                anyCompleted = true;
            }

            PromoteWaitingLoopJobs(state);
            if (TryPromoteMasterBuilderJobs(state, nowMs))
            {
                usedGold = true;
            }
        }
        while (anyCompleted);

        return usedGold;
    }

    public CommandResult CancelConstructionJob(
        VillageState state,
        CancelConstructionJobCommand command,
        long nowMs,
        SitterPermissions? sitterPermissions = null)
    {
        if (command.ActorPlayerId != command.OwnerPlayerId &&
            sitterPermissions is { } perms &&
            !perms.HasFlag(TerraviaPermissions.Sitter.BuildMask))
        {
            return CommandResult.Fail(TerraviaDomainErrorCodes.Sitters.PermissionDenied, "Sitter lacks build permission.");
        }

        int idx = state.ConstructionQueue.FindIndex(q => q.QueueItemId == command.QueueItemId);
        if (idx < 0)
            return CommandResult.Fail(TerraviaDomainErrorCodes.Villages.QueueItemNotFound);

        var cancelled = state.ConstructionQueue[idx];
        state.ConstructionQueue.RemoveAt(idx);

        // Refund 100% of resources (capped at storage) if not a Master Builder reservation
        if (!cancelled.IsMasterBuilder)
        {
            state.Wood = Math.Min(state.WarehouseCapacity, state.Wood + cancelled.DeductedOrRequiredCost.Wood);
            state.Clay = Math.Min(state.WarehouseCapacity, state.Clay + cancelled.DeductedOrRequiredCost.Clay);
            state.Iron = Math.Min(state.WarehouseCapacity, state.Iron + cancelled.DeductedOrRequiredCost.Iron);
            state.Crop = Math.Min(state.GranaryCapacity, state.Crop + cancelled.DeductedOrRequiredCost.Crop);
        }

        // Adjust any subsequent queued jobs targeting the same slot
        int mbLevel = GetHighestLevelInState(state, 15);
        for (int i = idx; i < state.ConstructionQueue.Count; i++)
        {
            var q = state.ConstructionQueue[i];
            if (q.SlotId == cancelled.SlotId && q.TargetLevel > cancelled.TargetLevel)
            {
                int newTargetLevel = q.TargetLevel - 1;
                var newStats = _catalog.GetBuildingLevelStats(q.BuildingTypeId, newTargetLevel);
                int newDuration = _calculator.CalculateBuildTimeSeconds(q.BuildingTypeId, newTargetLevel, mbLevel, state.ServerSpeed);
                long newFinish = q.IsMasterBuilder ? newDuration * 1000L : q.StartedAtMs + newDuration * 1000L;

                state.ConstructionQueue[i] = q with
                {
                    TargetLevel = newTargetLevel,
                    DeductedOrRequiredCost = newStats.Cost,
                    BuildDurationSeconds = newDuration,
                    FinishesAtMs = newFinish
                };
            }
        }

        // If slot is at Level 0 and no remaining queued jobs target this slot, clear its BuildingTypeId to 0
        if (state.GetSlotLevel(cancelled.SlotId) == 0 &&
            !state.ConstructionQueue.Any(q => q.SlotId == cancelled.SlotId))
        {
            state.SetSlot(cancelled.SlotId, 0, 0);
        }

        // Promote waiting-loop job if active slot freed, and recalculate its finish time from nowMs if needed
        PromoteWaitingLoopJobs(state, nowMs);
        TryPromoteMasterBuilderJobs(state, nowMs);

        return CommandResult.Success;
    }

    public (CommandResult Result, int GoldDeducted) FinishAllWithGold(
        VillageState state,
        FinishAllConstructionsWithGoldCommand command,
        long nowMs,
        SitterPermissions? sitterPermissions = null)
    {
        if (command.ActorPlayerId != command.OwnerPlayerId &&
            sitterPermissions is { } perms &&
            !perms.HasFlag(TerraviaPermissions.Sitter.GoldMask))
        {
            return (CommandResult.Fail(TerraviaDomainErrorCodes.Sitters.GoldPermissionDenied, "Sitter lacks Gold expenditure permission."), 0);
        }

        // FR-011 Rule 1: In a World Wonder village, instant Gold completion is completely disabled for ALL buildings
        if (state.IsWorldWonderVillage || state.WorldWonderBuildingType == 40)
        {
            return (CommandResult.Fail(TerraviaDomainErrorCodes.Villages.WorldWonderVillageFinishAllForbidden), 0);
        }

        // FR-011 Rule 2: Residence (25), Palace (26), and World Wonder (40) are never finished with Gold
        var standardEligible = state.ConstructionQueue
            .Where(q => !q.IsMasterBuilder && _catalog.GetBuilding(q.BuildingTypeId).CanFinishInstantlyWithGold)
            .OrderBy(q => q.FinishesAtMs)
            .ThenBy(q => q.QueueItemId)
            .ToList();

        var masterBuilderCandidates = state.ConstructionQueue
            .Where(q => q.IsMasterBuilder && _catalog.GetBuilding(q.BuildingTypeId).CanFinishInstantlyWithGold)
            .OrderBy(q => q.QueueItemId)
            .ToList();

        bool hasStandardWork = standardEligible.Count > 0 || state.ActiveDemolition is not null || state.HasActiveAcademyOrSmithyResearch;
        bool hasMasterBuilderWork = masterBuilderCandidates.Count > 0;

        if (!hasStandardWork && !hasMasterBuilderWork)
        {
            return (CommandResult.Fail(TerraviaDomainErrorCodes.Villages.NothingEligibleToFinish), 0);
        }

        int requiredGold = (hasStandardWork && hasMasterBuilderWork)
            ? 3
            : 2;

        if (state.PlayerGoldBalance < requiredGold)
        {
            return (CommandResult.Fail(TerraviaDomainErrorCodes.Villages.InsufficientGold), 0);
        }

        // 1. Complete all eligible active/waiting-loop jobs first
        foreach (var job in standardEligible)
        {
            state.ConstructionQueue.Remove(job);
            ApplyCompletedBuildingLevel(state, job.SlotId, job.BuildingTypeId, job.TargetLevel);
        }

        // 2. Complete active timed demolition if present
        if (state.ActiveDemolition is not null)
        {
            CompleteActiveDemolition(state);
        }

        // 3. Complete active Academy/Smithy research if present
        if (state.HasActiveAcademyOrSmithyResearch)
        {
            state.HasActiveAcademyOrSmithyResearch = false;
        }

        // 4. FR-011 Rule 3: For Master Builder jobs, verify prerequisites and deduct resources before finishing
        bool anyMasterFinished = false;
        foreach (var mbJob in masterBuilderCandidates)
        {
            var prereqCtx = CreateEvaluationContext(state, mbJob.SlotId).PrerequisiteContext;
            int currentLevel = state.GetSlotLevel(mbJob.SlotId);
            bool isNewBuild = currentLevel == 0;
            if (isNewBuild && !_ruleCatalog.MeetsPrerequisites(mbJob.BuildingTypeId, in prereqCtx))
                continue;

            var cost = mbJob.DeductedOrRequiredCost;
            if (state.Wood >= cost.Wood &&
                state.Clay >= cost.Clay &&
                state.Iron >= cost.Iron &&
                state.Crop >= cost.Crop)
            {
                state.Wood -= cost.Wood;
                state.Clay -= cost.Clay;
                state.Iron -= cost.Iron;
                state.Crop -= cost.Crop;
                state.ConstructionQueue.Remove(mbJob);
                ApplyCompletedBuildingLevel(state, mbJob.SlotId, mbJob.BuildingTypeId, mbJob.TargetLevel);
                anyMasterFinished = true;
            }
        }

        if (!hasStandardWork && !anyMasterFinished)
        {
            return (CommandResult.Fail(TerraviaDomainErrorCodes.Villages.InsufficientResourcesForMasterBuilderFinish), 0);
        }

        int actualGoldCost = (hasStandardWork && anyMasterFinished) ? 3 : 2;
        state.PlayerGoldBalance -= actualGoldCost;

        return (CommandResult.Success, actualGoldCost);
    }

    public (CommandResult Result, int GoldDeducted) StartDemolition(
        VillageState state,
        StartBuildingDemolitionCommand command,
        long nowMs,
        SitterPermissions? sitterPermissions = null)
    {
        if (command.ActorPlayerId != command.OwnerPlayerId &&
            sitterPermissions is { } perms)
        {
            if (!perms.HasFlag(TerraviaPermissions.Sitter.BuildMask))
                return (CommandResult.Fail(TerraviaDomainErrorCodes.Sitters.PermissionDenied), 0);
            if (command.IsInstantCompleteWithGold && !perms.HasFlag(TerraviaPermissions.Sitter.GoldMask))
                return (CommandResult.Fail(TerraviaDomainErrorCodes.Sitters.GoldPermissionDenied), 0);
        }

        int mbLevel = GetHighestLevelInState(state, 15);
        int targetGid = state.GetSlotBuildingType(command.SlotId);
        int currentLevel = state.GetSlotLevel(command.SlotId);
        bool slotHasQueuedUpgrade = state.ConstructionQueue.Any(q => q.SlotId == command.SlotId && !q.IsMasterBuilder);

        var demoCtx = new VillageDemolitionContext(
            MainBuildingLevel: mbLevel,
            TargetSlotBuildingTypeId: targetGid,
            TargetSlotCurrentLevel: currentLevel,
            SlotHasQueuedUpgrade: slotHasQueuedUpgrade,
            VillageHasActiveDemolition: state.ActiveDemolition is not null,
            IsAllianceOwner: state.IsAllianceOwner,
            AllianceMemberCount: state.AllianceMemberCount,
            QualifyingEmbassyCountAcrossPlayerVillages: state.QualifyingEmbassyCountAcrossPlayerVillages,
            PlayerGoldBalance: state.PlayerGoldBalance);

        var eligibility = _ruleCatalog.EvaluateDemolition(command.SlotId, command.IsInstantCompleteWithGold, in demoCtx, state.ServerSpeed);
        if (!eligibility.IsAllowed)
        {
            return (CommandResult.Fail(eligibility.Status.ToString()), 0);
        }

        if (command.IsInstantCompleteWithGold)
        {
            // 10-Gold Instant Complete Demolition directly to Level 0
            state.PlayerGoldBalance -= VillageConsts.InstantDemolitionGoldCost;
            state.ActiveDemolition = null;

            // Remove any pending Master Builder jobs on this slot
            state.ConstructionQueue.RemoveAll(q => q.SlotId == command.SlotId);

            // Deduct cumulative population across all levels 1..currentLevel
            var building = _catalog.GetBuilding(targetGid);
            int popToSubtract = 0;
            for (int lvl = 1; lvl <= currentLevel; lvl++)
            {
                popToSubtract += building.GetLevelStats(lvl).Population;
            }
            state.Population = Math.Max(0, state.Population - popToSubtract);

            state.SetSlot(command.SlotId, 0, 0);
            RecalculateVillageStorageAndCulturePoints(state);
            return (CommandResult.Success, VillageConsts.InstantDemolitionGoldCost);
        }

        state.ActiveDemolition = new DemolitionQueueItem(
            VillageId: state.VillageId,
            SlotId: command.SlotId,
            BuildingTypeId: targetGid,
            TargetLevel: eligibility.TargetLevel,
            FinishesAtMs: nowMs + eligibility.DurationSeconds * 1000L);

        return (CommandResult.Success, 0);
    }

    public bool CompleteDueDemolition(VillageState state, long nowMs)
    {
        if (state.ActiveDemolition is null || state.ActiveDemolition.FinishesAtMs > nowMs)
            return false;

        CompleteActiveDemolition(state);
        return true;
    }

    public CommandResult CancelDemolition(
        VillageState state,
        CancelBuildingDemolitionCommand command,
        SitterPermissions? sitterPermissions = null)
    {
        if (command.ActorPlayerId != command.OwnerPlayerId &&
            sitterPermissions is { } perms &&
            !perms.HasFlag(TerraviaPermissions.Sitter.BuildMask))
        {
            return CommandResult.Fail(TerraviaDomainErrorCodes.Sitters.PermissionDenied);
        }

        if (state.ActiveDemolition is null)
            return CommandResult.Fail(TerraviaDomainErrorCodes.Villages.NoActiveDemolition);

        state.ActiveDemolition = null;
        return CommandResult.Success;
    }

    private void CompleteActiveDemolition(VillageState state)
    {
        if (state.ActiveDemolition is not { } demo)
            return;

        state.ActiveDemolition = null;
        int currentLevel = state.GetSlotLevel(demo.SlotId);
        int gid = state.GetSlotBuildingType(demo.SlotId);
        if (gid <= 0 || currentLevel <= 0)
            return;

        int popDelta = _catalog.GetBuildingLevelStats(gid, currentLevel).Population;
        state.Population = Math.Max(0, state.Population - popDelta);

        int newLevel = currentLevel - 1;
        if (newLevel <= 0)
        {
            state.SetSlot(demo.SlotId, 0, 0);
            state.ConstructionQueue.RemoveAll(q => q.SlotId == demo.SlotId);
        }
        else
        {
            state.SetSlot(demo.SlotId, gid, newLevel);
        }

        RecalculateVillageStorageAndCulturePoints(state);
    }

    private void ApplyCompletedBuildingLevel(VillageState state, int slotId, int buildingTypeId, int newLevel)
    {
        state.SetSlot(slotId, buildingTypeId, newLevel);
        var stats = _catalog.GetBuildingLevelStats(buildingTypeId, newLevel);
        state.Population += stats.Population;
        RecalculateVillageStorageAndCulturePoints(state);
    }

    private void RecalculateVillageStorageAndCulturePoints(VillageState state)
    {
        state.CulturePointsPerDay = _calculator.CalculateVillageCulturePoints(
            state.SlotBuildingTypes,
            state.SlotLevels,
            state.WorldWonderLevel);

        int totalWarehouse = 0;
        int totalGranary = 0;

        for (int i = VillageConsts.MinInnerBuildingSlotId; i <= VillageConsts.MaxStandardSlotId; i++)
        {
            int gid = state.SlotBuildingTypes[i];
            int lvl = state.SlotLevels[i];
            if (lvl <= 0)
                continue;

            if (gid is 10 or 38)
            {
                totalWarehouse += (int)Math.Round(_catalog.GetBuildingLevelStats(gid, lvl).AttributeValue * state.StorageMultiplier);
            }
            else if (gid is 11 or 39)
            {
                totalGranary += (int)Math.Round(_catalog.GetBuildingLevelStats(gid, lvl).AttributeValue * state.StorageMultiplier);
            }
        }

        int baseCap = (int)Math.Round(VillageConsts.BaseStorageCapacity * state.StorageMultiplier);
        state.WarehouseCapacity = totalWarehouse > 0 ? totalWarehouse : baseCap;
        state.GranaryCapacity = totalGranary > 0 ? totalGranary : baseCap;

        state.Wood = Math.Min(state.Wood, state.WarehouseCapacity);
        state.Clay = Math.Min(state.Clay, state.WarehouseCapacity);
        state.Iron = Math.Min(state.Iron, state.WarehouseCapacity);
        state.Crop = Math.Min(state.Crop, state.GranaryCapacity);
    }

    private void PromoteWaitingLoopJobs(VillageState state, long? cancelTimestampMs = null)
    {
        bool isRomanDual = state.TribeId == 1 || state.AllowAllTribeRomanQueue;
        int activeField = state.ConstructionQueue.Count(q => q.IsActive && q.IsResourceField);
        int activeInner = state.ConstructionQueue.Count(q => q.IsActive && !q.IsResourceField);

        for (int i = 0; i < state.ConstructionQueue.Count; i++)
        {
            var q = state.ConstructionQueue[i];
            if (!q.IsWaitingLoop)
                continue;

            bool canPromote = isRomanDual
                ? (q.IsResourceField ? activeField == 0 : activeInner == 0)
                : (activeField + activeInner) == 0;

            if (canPromote)
            {
                long newStart = cancelTimestampMs.HasValue && cancelTimestampMs.Value < q.StartedAtMs
                    ? cancelTimestampMs.Value
                    : q.StartedAtMs;
                long newFinish = newStart + q.BuildDurationSeconds * 1000L;

                state.ConstructionQueue[i] = q with
                {
                    IsWaitingLoop = false,
                    StartedAtMs = newStart,
                    FinishesAtMs = newFinish
                };

                if (q.IsResourceField) activeField++;
                else activeInner++;
            }
        }
    }

    public bool TryPromoteMasterBuilderJobs(VillageState state, long nowMs)
    {
        bool anyPromoted = false;
        bool isRomanDual = state.TribeId == 1 || state.AllowAllTribeRomanQueue;

        for (int i = 0; i < state.ConstructionQueue.Count; i++)
        {
            var q = state.ConstructionQueue[i];
            if (!q.IsMasterBuilder)
                continue;

            int activeField = state.ConstructionQueue.Count(item => item.IsActive && item.IsResourceField);
            int activeInner = state.ConstructionQueue.Count(item => item.IsActive && !item.IsResourceField);
            int waitingLoop = state.ConstructionQueue.Count(item => item.IsWaitingLoop);

            bool activeSlotFree = isRomanDual
                ? (q.IsResourceField ? activeField == 0 : activeInner == 0)
                : (activeField + activeInner) == 0;

            if (!activeSlotFree || waitingLoop > 0)
                continue;

            var cost = q.DeductedOrRequiredCost;
            if (state.PlayerGoldBalance < 1 ||
                state.Wood < cost.Wood ||
                state.Clay < cost.Clay ||
                state.Iron < cost.Iron ||
                state.Crop < cost.Crop)
            {
                continue;
            }

            state.Wood -= cost.Wood;
            state.Clay -= cost.Clay;
            state.Iron -= cost.Iron;
            state.Crop -= cost.Crop;
            state.PlayerGoldBalance -= 1;

            int mbLevel = GetHighestLevelInState(state, 15);
            int buildSeconds = _calculator.CalculateBuildTimeSeconds(q.BuildingTypeId, q.TargetLevel, mbLevel, state.ServerSpeed);

            state.ConstructionQueue[i] = q with
            {
                IsMasterBuilder = false,
                IsWaitingLoop = false,
                BuildDurationSeconds = buildSeconds,
                StartedAtMs = nowMs,
                FinishesAtMs = nowMs + buildSeconds * 1000L
            };

            anyPromoted = true;
        }

        return anyPromoted;
    }

    private static long FindPrecedingPartitionFinishMs(VillageState state, int targetSlotId, long nowMs)
    {
        bool isRomanDual = state.TribeId == 1 || state.AllowAllTribeRomanQueue;
        bool targetIsField = targetSlotId is >= VillageConsts.MinResourceFieldSlotId and <= VillageConsts.MaxResourceFieldSlotId;

        long maxFinish = nowMs;
        foreach (var q in state.ConstructionQueue)
        {
            if (q.IsMasterBuilder)
                continue;
            if (isRomanDual && q.IsResourceField != targetIsField)
                continue;
            if (q.FinishesAtMs > maxFinish)
                maxFinish = q.FinishesAtMs;
        }
        return maxFinish;
    }

    private static int GetHighestLevelInState(VillageState state, int buildingTypeId)
    {
        int highest = 0;
        for (int i = VillageConsts.MinResourceFieldSlotId; i <= VillageConsts.MaxStandardSlotId; i++)
        {
            if (state.SlotBuildingTypes[i] == buildingTypeId && state.SlotLevels[i] > highest)
                highest = state.SlotLevels[i];
        }
        return highest;
    }
}
