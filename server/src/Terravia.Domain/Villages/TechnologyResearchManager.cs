using Terravia.Domain.Catalog;

namespace Terravia.Domain.Villages;

/// <summary>
/// Orchestrates Academy (gid 22) unit discovery (t2..t9), Blacksmith (gid 12) offense upgrades (b1..b8),
/// Armoury (gid 13) defense upgrades (a1..a8), Plus 2nd Smithy waiting-loop slot, and 2 Gold instant research completion (FR-008..FR-009).
/// </summary>
public sealed class TechnologyResearchManager
{
    private readonly IGameCatalog _catalog;
    private readonly ITrainingTimeCalculator _timeCalculator;
    private long _nextQueueItemId = 1;

    public TechnologyResearchManager(
        IGameCatalog? catalog = null,
        ITrainingTimeCalculator? timeCalculator = null)
    {
        _catalog = catalog ?? FrozenGameCatalog.Default;
        _timeCalculator = timeCalculator ?? new TrainingTimeCalculator(_catalog);
    }

    public ResearchEnrollmentResult StartAcademyResearch(
        VillageState state,
        StartAcademyResearchCommand command,
        long nowMs,
        SitterPermissions? sitterPermissions = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);

        if (sitterPermissions.HasValue && !sitterPermissions.Value.HasFlag(SitterPermissions.BuildAndDemolish))
        {
            return Fail(ResearchCategory.Academy, command.UnitTypeId, TerraviaDomainErrorCodes.Sitters.PermissionDenied);
        }

        if (command.UnitTypeId is < CatalogConsts.MinUnitId or > CatalogConsts.MaxStandardUnitId)
        {
            return Fail(ResearchCategory.Academy, command.UnitTypeId, TerraviaDomainErrorCodes.Villages.InvalidUnitForFacility);
        }

        var unit = _catalog.GetUnit(command.UnitTypeId);
        if (unit.TribeId != state.TribeId || unit.TribeUnitSlot is < 2 or > 9 || unit.ResearchCost is not { } researchCost)
        {
            return Fail(ResearchCategory.Academy, command.UnitTypeId, TerraviaDomainErrorCodes.Villages.InvalidUnitForFacility);
        }

        int slot = unit.TribeUnitSlot;
        if (state.UnlockedUnitSlots[slot] || state.ResearchQueue.Any(q => q.Category == ResearchCategory.Academy && q.TribeUnitSlot == slot))
        {
            return Fail(ResearchCategory.Academy, command.UnitTypeId, TerraviaDomainErrorCodes.Villages.UnitAlreadyResearched);
        }

        // Strict limit of 1 active Academy research per village (Plus waiting loop does not apply to Academy)
        if (state.ResearchQueue.Any(q => q.Category == ResearchCategory.Academy))
        {
            return Fail(ResearchCategory.Academy, command.UnitTypeId, TerraviaDomainErrorCodes.Villages.AcademyResearchAlreadyActive);
        }

        int academyLevel = GetHighestBuildingLevel(state, 22);
        if (academyLevel <= 0 || !MeetsResearchPrerequisites(state, unit))
        {
            return Fail(ResearchCategory.Academy, command.UnitTypeId, TerraviaDomainErrorCodes.Villages.ResearchPrerequisitesNotMet);
        }

        if (!HasSufficientResources(state, in researchCost))
        {
            return Fail(ResearchCategory.Academy, command.UnitTypeId, TerraviaDomainErrorCodes.Villages.InsufficientResources);
        }

        DeductResources(state, in researchCost);

        int durationSeconds = _timeCalculator.CalculateAcademyResearchDurationSeconds(command.UnitTypeId, academyLevel, state.ServerSpeed);
        long finishesAtMs = nowMs + durationSeconds * 1000L;

        long nextId = Math.Max(_nextQueueItemId++, state.ResearchQueue.Count > 0 ? state.ResearchQueue.Max(q => q.QueueItemId) + 1 : 1);
        state.ResearchQueue.Add(new ResearchQueueItem
        {
            QueueItemId = nextId,
            VillageId = state.VillageId,
            Category = ResearchCategory.Academy,
            UnitTypeId = command.UnitTypeId,
            TribeUnitSlot = slot,
            TargetLevel = 1,
            IsPlusWaitingLoop = false,
            StartedAtMs = nowMs,
            FinishesAtMs = finishesAtMs
        });

        state.HasActiveAcademyOrSmithyResearch = true;

        return new ResearchEnrollmentResult(
            Succeeded: true,
            ErrorCode: null,
            Category: ResearchCategory.Academy,
            UnitTypeId: command.UnitTypeId,
            TargetLevel: 1,
            IsPlusWaitingLoop: false,
            DeductedCost: researchCost,
            FinishesAtMs: finishesAtMs);
    }

    public ResearchEnrollmentResult StartSmithyUpgrade(
        VillageState state,
        StartSmithyUpgradeCommand command,
        long nowMs,
        SitterPermissions? sitterPermissions = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);

        if (command.Category is not (ResearchCategory.Blacksmith or ResearchCategory.Armoury))
        {
            return Fail(command.Category, command.UnitTypeId, TerraviaDomainErrorCodes.Villages.InvalidUnitForFacility);
        }

        if (sitterPermissions.HasValue && !sitterPermissions.Value.HasFlag(SitterPermissions.BuildAndDemolish))
        {
            return Fail(command.Category, command.UnitTypeId, TerraviaDomainErrorCodes.Sitters.PermissionDenied);
        }

        if (command.UnitTypeId is < CatalogConsts.MinUnitId or > CatalogConsts.MaxStandardUnitId)
        {
            return Fail(command.Category, command.UnitTypeId, TerraviaDomainErrorCodes.Villages.InvalidUnitForFacility);
        }

        var unit = _catalog.GetUnit(command.UnitTypeId);
        int slot = unit.TribeUnitSlot;

        // Only combat slots 1..8 can be upgraded in Blacksmith/Armoury
        if (unit.TribeId != state.TribeId || slot is < 1 or > 8 || unit.SmithyUpgrades.IsDefaultOrEmpty)
        {
            return Fail(command.Category, command.UnitTypeId, TerraviaDomainErrorCodes.Villages.InvalidUnitForFacility);
        }

        // Unit must be unlocked in the village (slot 1 is unlocked by default)
        bool isUnlocked = slot == 1 || state.UnlockedUnitSlots[slot];
        if (!isUnlocked)
        {
            return Fail(command.Category, command.UnitTypeId, TerraviaDomainErrorCodes.Villages.UnitNotResearched);
        }

        int smithyGid = command.Category == ResearchCategory.Blacksmith ? 12 : 13;
        int smithyLevel = GetHighestBuildingLevel(state, smithyGid);
        if (smithyLevel <= 0)
        {
            return Fail(command.Category, command.UnitTypeId, TerraviaDomainErrorCodes.Villages.TrainingFacilityNotBuilt);
        }

        var activeInLane = state.ResearchQueue.Where(q => q.Category == command.Category).OrderBy(q => q.FinishesAtMs).ToList();
        int maxAllowedInLane = state.HasPlusSubscription ? 2 : 1;
        if (activeInLane.Count >= maxAllowedInLane)
        {
            return Fail(command.Category, command.UnitTypeId, TerraviaDomainErrorCodes.Villages.SmithyQueueFull);
        }

        int currentLevel = command.Category == ResearchCategory.Blacksmith
            ? state.OffenseUpgradeLevels[slot]
            : state.DefenseUpgradeLevels[slot];
        int queuedForSlot = activeInLane.Count(q => q.TribeUnitSlot == slot);
        int targetLevel = currentLevel + queuedForSlot + 1;

        if (targetLevel > 20 || targetLevel > smithyLevel)
        {
            return Fail(command.Category, command.UnitTypeId, TerraviaDomainErrorCodes.Villages.SmithyLevelCapReached);
        }

        var upgradeCost = unit.SmithyUpgrades[targetLevel - 1].Cost;
        if (!HasSufficientResources(state, in upgradeCost))
        {
            return Fail(command.Category, command.UnitTypeId, TerraviaDomainErrorCodes.Villages.InsufficientResources);
        }

        // Immediate resource deduction for both 1st slot and 2nd Plus waiting-loop slot (FR-009.4)
        DeductResources(state, in upgradeCost);

        int durationSeconds = _timeCalculator.CalculateSmithyUpgradeDurationSeconds(
            command.UnitTypeId,
            targetLevel,
            smithyLevel,
            state.ServerSpeed);

        bool isPlusLoop = activeInLane.Count == 1;
        long startedAtMs;
        long finishesAtMs;

        if (!isPlusLoop)
        {
            startedAtMs = nowMs;
            finishesAtMs = nowMs + durationSeconds * 1000L;
        }
        else
        {
            double speed = state.ServerSpeed > 0.0 ? state.ServerSpeed : 1.0;
            int loopDelaySeconds = (int)Math.Ceiling(VillageConsts.PlusSmithyLoopBaseDelaySeconds / speed);
            startedAtMs = activeInLane[0].FinishesAtMs + loopDelaySeconds * 1000L;
            finishesAtMs = startedAtMs + durationSeconds * 1000L;
        }

        long nextId = Math.Max(_nextQueueItemId++, state.ResearchQueue.Count > 0 ? state.ResearchQueue.Max(q => q.QueueItemId) + 1 : 1);
        state.ResearchQueue.Add(new ResearchQueueItem
        {
            QueueItemId = nextId,
            VillageId = state.VillageId,
            Category = command.Category,
            UnitTypeId = command.UnitTypeId,
            TribeUnitSlot = slot,
            TargetLevel = targetLevel,
            IsPlusWaitingLoop = isPlusLoop,
            StartedAtMs = startedAtMs,
            FinishesAtMs = finishesAtMs
        });

        state.HasActiveAcademyOrSmithyResearch = true;

        return new ResearchEnrollmentResult(
            Succeeded: true,
            ErrorCode: null,
            Category: command.Category,
            UnitTypeId: command.UnitTypeId,
            TargetLevel: targetLevel,
            IsPlusWaitingLoop: isPlusLoop,
            DeductedCost: upgradeCost,
            FinishesAtMs: finishesAtMs);
    }

    public (CommandResult Result, int GoldDeducted, int CompletedCount) FinishAllResearchWithGold(
        VillageState state,
        FinishAllResearchWithGoldCommand command,
        long nowMs,
        SitterPermissions? sitterPermissions = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);

        if (sitterPermissions.HasValue && !sitterPermissions.Value.HasFlag(SitterPermissions.BuyAndSpendGold))
        {
            return (CommandResult.Fail(TerraviaDomainErrorCodes.Sitters.GoldPermissionDenied, "Sitter lacks Gold spending permission."), 0, 0);
        }

        if (state.ResearchQueue.Count == 0)
        {
            return (CommandResult.Fail(TerraviaDomainErrorCodes.Villages.NothingEligibleToFinish, "No active Academy or Smithy projects to finish."), 0, 0);
        }

        if (state.PlayerGoldBalance < VillageConsts.ResearchFinishAllGoldCost)
        {
            return (CommandResult.Fail(TerraviaDomainErrorCodes.Villages.InsufficientGold, "Insufficient Gold to finish research."), 0, 0);
        }

        state.PlayerGoldBalance -= VillageConsts.ResearchFinishAllGoldCost;

        foreach (var item in state.ResearchQueue)
        {
            item.FinishesAtMs = nowMs;
        }

        int completedCount = CompleteDueResearchItems(state, nowMs);
        return (CommandResult.Success, VillageConsts.ResearchFinishAllGoldCost, completedCount);
    }

    public int CompleteDueResearchItems(VillageState state, long nowMs)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.ResearchQueue.Count == 0)
        {
            state.HasActiveAcademyOrSmithyResearch = false;
            return 0;
        }

        var dueItems = state.ResearchQueue
            .Where(q => nowMs >= q.FinishesAtMs)
            .OrderBy(q => q.FinishesAtMs)
            .ThenBy(q => q.QueueItemId)
            .ToList();

        if (dueItems.Count == 0)
        {
            return 0;
        }

        foreach (var item in dueItems)
        {
            ApplyCompletedResearchItem(state, item);
            state.ResearchQueue.Remove(item);
        }

        // Promote any remaining Plus waiting-loop item whose 1st item finished
        foreach (var remaining in state.ResearchQueue)
        {
            if (remaining.IsPlusWaitingLoop && state.ResearchQueue.Count(q => q.Category == remaining.Category) == 1)
            {
                remaining.IsPlusWaitingLoop = false;
            }
        }

        state.HasActiveAcademyOrSmithyResearch = state.ResearchQueue.Count > 0;
        return dueItems.Count;
    }

    private static void ApplyCompletedResearchItem(VillageState state, ResearchQueueItem item)
    {
        int slot = item.TribeUnitSlot;
        switch (item.Category)
        {
            case ResearchCategory.Academy:
                if (slot is >= 1 and <= 10)
                {
                    state.UnlockedUnitSlots[slot] = true;
                }
                break;

            case ResearchCategory.Blacksmith:
                if (slot is >= 1 and <= 8)
                {
                    state.OffenseUpgradeLevels[slot] = Math.Max(state.OffenseUpgradeLevels[slot], item.TargetLevel);
                    SyncHomeStackUpgrades(state);
                }
                break;

            case ResearchCategory.Armoury:
                if (slot is >= 1 and <= 8)
                {
                    state.DefenseUpgradeLevels[slot] = Math.Max(state.DefenseUpgradeLevels[slot], item.TargetLevel);
                    SyncHomeStackUpgrades(state);
                }
                break;
        }
    }

    private static void SyncHomeStackUpgrades(VillageState state)
    {
        // Snapshot at Dispatch Rule (Clarification Q3): only update the Home garrison stack;
        // outbound movements and foreign reinforcements keep their dispatch snapshot.
        foreach (var stack in state.TroopStacks)
        {
            if (stack.Tier == StarvationPriorityTier.OwnStationed && stack.OwnerVillageId == state.VillageId)
            {
                Array.Copy(state.OffenseUpgradeLevels, stack.OffenseUpgradeLevels, state.OffenseUpgradeLevels.Length);
                Array.Copy(state.DefenseUpgradeLevels, stack.DefenseUpgradeLevels, state.DefenseUpgradeLevels.Length);
            }
        }
    }

    private static bool MeetsResearchPrerequisites(VillageState state, UnitDefinition unit)
    {
        foreach (var req in unit.ResearchPrerequisites)
        {
            if (GetHighestBuildingLevel(state, req.BuildingTypeId) < req.Level)
            {
                return false;
            }
        }
        return true;
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

    private static bool HasSufficientResources(VillageState state, in ResourceCost cost) =>
        state.Wood >= cost.Wood &&
        state.Clay >= cost.Clay &&
        state.Iron >= cost.Iron &&
        state.Crop >= cost.Crop;

    private static void DeductResources(VillageState state, in ResourceCost cost)
    {
        state.Wood -= cost.Wood;
        state.Clay -= cost.Clay;
        state.Iron -= cost.Iron;
        state.Crop -= cost.Crop;
        state.SyncExactResourcesFromInteger();
    }

    private static ResearchEnrollmentResult Fail(ResearchCategory category, int unitTypeId, string errorCode) =>
        new(false, errorCode, category, unitTypeId, 0, false, ResourceCost.Zero, 0);
}
