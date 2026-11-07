using Terravia.Domain.Catalog;

namespace Terravia.Domain.Villages;

/// <summary>
/// Orchestrates military unit recruitment, Great Military 3x cost parallel queues, civic expansion slot gating,
/// batch merging, and sequential unit-by-unit graduation (FR-001..FR-007).
/// </summary>
public sealed class MilitaryTrainingManager
{
    private readonly IGameCatalog _catalog;
    private readonly ITrainingTimeCalculator _timeCalculator;
    private long _nextQueueItemId = 1;

    public MilitaryTrainingManager(
        IGameCatalog? catalog = null,
        ITrainingTimeCalculator? timeCalculator = null)
    {
        _catalog = catalog ?? FrozenGameCatalog.Default;
        _timeCalculator = timeCalculator ?? new TrainingTimeCalculator(_catalog);
    }

    public TrainingEnrollmentResult EnqueueTrainingBatch(
        VillageState state,
        EnqueueMilitaryTrainingCommand command,
        long nowMs,
        SitterPermissions? sitterPermissions = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);

        if (sitterPermissions.HasValue && !sitterPermissions.Value.HasFlag(SitterPermissions.BuildAndDemolish))
        {
            return Fail(TerraviaDomainErrorCodes.Sitters.PermissionDenied);
        }

        // Simultaneous Chief (u*9) and Settler (u*0) submission guard
        if (command.SecondExpansionUnitTypeId.HasValue && command.SecondExpansionRequestedAmount > 0)
        {
            int slot1 = GetUnitSlot(command.UnitTypeId);
            int slot2 = GetUnitSlot(command.SecondExpansionUnitTypeId.Value);
            if ((slot1 == 9 && slot2 == 10) || (slot1 == 10 && slot2 == 9))
            {
                return Fail(TerraviaDomainErrorCodes.Villages.SimultaneousChiefAndSettlerForbidden);
            }
        }

        if (command.RequestedAmount <= 0)
        {
            return Fail(TerraviaDomainErrorCodes.Villages.InvalidUnitForFacility);
        }

        if (command.UnitTypeId is (< CatalogConsts.MinUnitId or > CatalogConsts.MaxStandardUnitId) and not CatalogConsts.TrapUnitId)
        {
            return Fail(TerraviaDomainErrorCodes.Villages.InvalidUnitForFacility);
        }

        var unit = _catalog.GetUnit(command.UnitTypeId);

        if (!IsUnitEligibleForFacility(state.TribeId, unit, command.FacilityCategory))
        {
            return Fail(TerraviaDomainErrorCodes.Villages.InvalidUnitForFacility);
        }

        // Great Military non-capital rule (FR-002)
        if (command.FacilityCategory is TrainingFacilityCategory.GreatBarracks
            or TrainingFacilityCategory.GreatStable
            or TrainingFacilityCategory.GreatWorkshop)
        {
            if (state.IsCapital)
            {
                return Fail(TerraviaDomainErrorCodes.Villages.GreatMilitaryForbiddenInCapital);
            }

            if (command.FacilityCategory == TrainingFacilityCategory.GreatWorkshop && !state.GreatWorkshopEnabled)
            {
                return Fail(TerraviaDomainErrorCodes.Villages.TrainingFacilityNotBuilt);
            }
        }

        int facilityLevel = GetFacilityBuildingLevel(state, command.FacilityCategory);
        if (facilityLevel <= 0)
        {
            return Fail(TerraviaDomainErrorCodes.Villages.TrainingFacilityNotBuilt);
        }

        // Unlocked technology check (FR-008)
        if (command.UnitTypeId != CatalogConsts.TrapUnitId)
        {
            int slot = unit.TribeUnitSlot;
            bool isUnlocked = slot is 1 or 10 || (slot >= 1 && slot < state.UnlockedUnitSlots.Length && state.UnlockedUnitSlots[slot]);
            if (!isUnlocked)
            {
                return Fail(TerraviaDomainErrorCodes.Villages.UnitNotResearched);
            }
        }

        int costMultiplier = command.FacilityCategory is TrainingFacilityCategory.GreatBarracks
            or TrainingFacilityCategory.GreatStable
            or TrainingFacilityCategory.GreatWorkshop
            ? VillageConsts.GreatMilitaryCostMultiplier
            : 1;

        var perUnitCost = new ResourceCost(
            unit.Cost.Wood * costMultiplier,
            unit.Cost.Clay * costMultiplier,
            unit.Cost.Iron * costMultiplier,
            unit.Cost.Crop * costMultiplier);

        // Granary stockpile gate (Clarification Q1)
        if (state.Crop <= 0 ||
            state.Wood < perUnitCost.Wood ||
            state.Clay < perUnitCost.Clay ||
            state.Iron < perUnitCost.Iron ||
            state.Crop < perUnitCost.Crop)
        {
            return Fail(TerraviaDomainErrorCodes.Villages.InsufficientResources);
        }

        int maxAffordable = CalculateMaxAffordableUnits(state, in perUnitCost);
        int clampedAmount = Math.Min(command.RequestedAmount, maxAffordable);

        int hdtLevel = GetHighestBuildingLevel(state, 41);
        int effectivePopUpkeep = CalculateEffectiveUnitPopUpkeep(state.TribeId, unit, hdtLevel);

        // Optional world setting BlockTrainingOnNegativeCrop (Clarification Q1)
        if (state.BlockTrainingOnNegativeCrop && effectivePopUpkeep > 0)
        {
            if (state.NetCropProductionPerHour < effectivePopUpkeep)
            {
                return Fail(TerraviaDomainErrorCodes.Villages.NegativeCropTrainingRestricted);
            }

            clampedAmount = Math.Min(clampedAmount, state.NetCropProductionPerHour / effectivePopUpkeep);
            if (clampedAmount <= 0)
            {
                return Fail(TerraviaDomainErrorCodes.Villages.NegativeCropTrainingRestricted);
            }
        }

        // Civic Expansion Slot Gating (FR-004)
        if (command.FacilityCategory == TrainingFacilityCategory.ResidencePalace)
        {
            var (chiefSlots, settlerSlots) = CalculateAvailableExpansionUnitSlots(state);
            int maxBySlot = unit.TribeUnitSlot == 9 ? chiefSlots : settlerSlots;
            clampedAmount = Math.Min(clampedAmount, maxBySlot);
            if (clampedAmount <= 0)
            {
                return Fail(TerraviaDomainErrorCodes.Villages.ExpansionUnitQuotaExceeded);
            }
        }

        // Gaul Trapper Capacity Gating (FR-013)
        if (command.FacilityCategory == TrainingFacilityCategory.Trapper)
        {
            int totalTrapCap = CalculateTotalTrapCapacity(state);
            int queuedTraps = GetQueuedTrapsCount(state);
            int freeTrapCap = Math.Max(0, totalTrapCap - state.TotalTrapsCount - queuedTraps);
            clampedAmount = Math.Min(clampedAmount, freeTrapCap);
            if (clampedAmount <= 0)
            {
                return Fail(TerraviaDomainErrorCodes.Villages.TrapperCapacityExceeded);
            }
        }

        var speedCtx = new TrainingSpeedContext(
            UnitTypeId: command.UnitTypeId,
            FacilityCategory: command.FacilityCategory,
            FacilityLevel: facilityLevel,
            HorseDrinkingTroughLevel: hdtLevel,
            TrainerArtifactMultiplier: state.TrainerArtifactMultiplier,
            HasStationedLivingHero: state.HasStationedLivingHero,
            StationedHeroHelmet: state.StationedHeroHelmet,
            AllianceRecruitmentBonusLevel: state.AllianceRecruitmentBonusLevel,
            AllianceJoinedAtMs: state.AllianceJoinedAtMs,
            CurrentTimestampMs: nowMs,
            ServerSpeed: state.ServerSpeed);

        int eachTimeSeconds = _timeCalculator.CalculateEachTimeSeconds(in speedCtx);
        var deductedCost = new ResourceCost(
            perUnitCost.Wood * clampedAmount,
            perUnitCost.Clay * clampedAmount,
            perUnitCost.Iron * clampedAmount,
            perUnitCost.Crop * clampedAmount);

        state.Wood -= deductedCost.Wood;
        state.Clay -= deductedCost.Clay;
        state.Iron -= deductedCost.Iron;
        state.Crop -= deductedCost.Crop;
        state.SyncExactResourcesFromInteger();

        var (finishesAtMs, wasMerged) = AppendOrMergeQueueEntry(
            state,
            command.FacilityCategory,
            command.UnitTypeId,
            clampedAmount,
            effectivePopUpkeep,
            eachTimeSeconds,
            nowMs);

        return new TrainingEnrollmentResult(
            Succeeded: true,
            ErrorCode: null,
            EnrolledAmount: clampedAmount,
            EachTimeSeconds: eachTimeSeconds,
            DeductedCost: deductedCost,
            FinishesAtMs: finishesAtMs,
            WasMergedIntoTrailingBatch: wasMerged);
    }

    /// <summary>
    /// Evaluates sequential unit-by-unit graduation (<c>floor(elapsed / eachtime)</c>) across all active facility lanes (FR-003).
    /// </summary>
    public int CompleteDueTrainingGraduations(VillageState state, long nowMs)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.TrainingQueues.Count == 0)
        {
            return 0;
        }

        int totalGraduated = 0;

        for (int i = 0; i < state.TrainingQueues.Count; i++)
        {
            var item = state.TrainingQueues[i];
            if (item.AmountRemaining <= 0 || nowMs < item.CommenceAtMs)
            {
                continue;
            }

            long elapsedSeconds = (nowMs - item.CommenceAtMs) / 1000L;
            int eachTime = Math.Max(1, item.EachTimeSeconds);
            long rawCompleted = elapsedSeconds / eachTime;
            if (rawCompleted <= 0)
            {
                continue;
            }

            int graduated = (int)Math.Min(item.AmountRemaining, rawCompleted);
            CreditGraduatedUnitsToVillage(state, item.UnitTypeId, graduated);

            item.CommenceAtMs += (long)graduated * eachTime * 1000L;
            item.AmountRemaining -= graduated;
            totalGraduated += graduated;
        }

        state.TrainingQueues.RemoveAll(q => q.AmountRemaining <= 0);
        return totalGraduated;
    }

    public (int ChiefSlots, int SettlerSlots) CalculateAvailableExpansionUnitSlots(VillageState state)
    {
        int unlockedSlots = 0;
        int resLevel = GetHighestBuildingLevel(state, 25);
        int palLevel = GetHighestBuildingLevel(state, 26);
        int ccLevel = GetHighestBuildingLevel(state, 44);

        if (palLevel >= 20 || ccLevel >= 20)
            unlockedSlots = 3;
        else if (palLevel >= 15 || ccLevel >= 15 || resLevel >= 20)
            unlockedSlots = 2;
        else if (palLevel >= 10 || ccLevel >= 10 || resLevel >= 10)
            unlockedSlots = 1;

        int childVillages = 0;
        if (state.ExpansionSlot1VillageId.HasValue) childVillages++;
        if (state.ExpansionSlot2VillageId.HasValue) childVillages++;
        if (state.ExpansionSlot3VillageId.HasValue) childVillages++;

        int chiefUnitId = (state.TribeId - 1) * 10 + 9;
        int settlerUnitId = (state.TribeId - 1) * 10 + 10;

        int totalChiefs = 0;
        int totalSettlers = 0;

        // 1. Garrison, reinforcements, movements
        foreach (var stack in state.TroopStacks)
        {
            if (stack.OwnerVillageId == state.VillageId)
            {
                if (chiefUnitId is >= 1 and <= 90 && chiefUnitId < stack.UnitCounts.Length)
                    totalChiefs += stack.UnitCounts[chiefUnitId];
                if (settlerUnitId is >= 1 and <= 90 && settlerUnitId < stack.UnitCounts.Length)
                    totalSettlers += stack.UnitCounts[settlerUnitId];
            }
        }

        // 2. Active training queues
        foreach (var q in state.TrainingQueues)
        {
            if (q.UnitTypeId == chiefUnitId)
                totalChiefs += q.AmountRemaining;
            else if (q.UnitTypeId == settlerUnitId)
                totalSettlers += q.AmountRemaining;
        }

        // 3. Trapped prisoners belonging to this village
        foreach (var pris in state.HeldPrisoners)
        {
            if (pris.OwnerVillageId == state.VillageId)
            {
                totalChiefs += pris.UnitCountsBySlot[9];
                totalSettlers += pris.UnitCountsBySlot[10];
            }
        }

        int settlerSlots = Math.Max(0, 3 * (unlockedSlots - childVillages - totalChiefs) - totalSettlers);
        int chiefSlots = !state.UnlockedUnitSlots[9]
            ? 0
            : Math.Max(0, unlockedSlots - childVillages - totalChiefs - ((totalSettlers + 2) / 3));

        return (chiefSlots, settlerSlots);
    }

    public int CalculateEffectiveUnitPopUpkeep(int tribeId, UnitDefinition unit, int horseDrinkingTroughLevel)
    {
        if (unit.UnitTypeId == CatalogConsts.TrapUnitId)
        {
            return 0;
        }

        int upkeep = unit.UpkeepCropPerHour;
        if (tribeId == 1 && unit.HorseDrinkingTroughThresholdLevel.HasValue
            && horseDrinkingTroughLevel >= unit.HorseDrinkingTroughThresholdLevel.Value)
        {
            upkeep = Math.Max(0, upkeep - 1);
        }

        return upkeep;
    }

    internal (long FinishesAtMs, bool WasMerged) AppendOrMergeQueueEntry(
        VillageState state,
        TrainingFacilityCategory category,
        int unitTypeId,
        int amount,
        int popUpkeepPerUnit,
        int eachTimeSeconds,
        long nowMs)
    {
        TrainingQueueItem? trailingInLane = null;
        for (int i = state.TrainingQueues.Count - 1; i >= 0; i--)
        {
            if (state.TrainingQueues[i].FacilityCategory == category)
            {
                trailingInLane = state.TrainingQueues[i];
                break;
            }
        }

        if (trailingInLane is not null
            && trailingInLane.UnitTypeId == unitTypeId
            && trailingInLane.EachTimeSeconds == eachTimeSeconds)
        {
            trailingInLane.AmountRemaining += amount;
            trailingInLane.FinishesAtMs += (long)amount * eachTimeSeconds * 1000L;
            return (trailingInLane.FinishesAtMs, true);
        }

        long commenceAtMs = trailingInLane is not null && trailingInLane.FinishesAtMs > nowMs
            ? trailingInLane.FinishesAtMs
            : nowMs;
        long finishesAtMs = commenceAtMs + (long)amount * eachTimeSeconds * 1000L;

        long nextId = Math.Max(_nextQueueItemId++, state.TrainingQueues.Count > 0 ? state.TrainingQueues.Max(q => q.QueueItemId) + 1 : 1);
        state.TrainingQueues.Add(new TrainingQueueItem
        {
            QueueItemId = nextId,
            VillageId = state.VillageId,
            FacilityCategory = category,
            UnitTypeId = unitTypeId,
            AmountRemaining = amount,
            PopUpkeepPerUnit = popUpkeepPerUnit,
            EachTimeSeconds = eachTimeSeconds,
            CommenceAtMs = commenceAtMs,
            FinishesAtMs = finishesAtMs
        });

        return (finishesAtMs, false);
    }

    private void CreditGraduatedUnitsToVillage(VillageState state, int unitTypeId, int graduated)
    {
        if (unitTypeId == CatalogConsts.TrapUnitId)
        {
            state.TotalTrapsCount += graduated;
            return;
        }

        var homeStack = state.TroopStacks.FirstOrDefault(
            s => s.Tier == StarvationPriorityTier.OwnStationed && s.OwnerVillageId == state.VillageId);

        if (homeStack is null)
        {
            homeStack = new StarvationCandidateStack
            {
                StackOrMovementId = state.VillageId,
                Tier = StarvationPriorityTier.OwnStationed,
                OwnerVillageId = state.VillageId,
                LocationTileId = state.TileId
            };
            Array.Copy(state.OffenseUpgradeLevels, homeStack.OffenseUpgradeLevels, state.OffenseUpgradeLevels.Length);
            Array.Copy(state.DefenseUpgradeLevels, homeStack.DefenseUpgradeLevels, state.DefenseUpgradeLevels.Length);
            state.TroopStacks.Add(homeStack);
        }

        if (unitTypeId is >= 1 and <= 90)
        {
            homeStack.UnitCounts[unitTypeId] += graduated;
        }
    }

    private int CalculateTotalTrapCapacity(VillageState state)
    {
        int total = 0;
        for (int s = VillageConsts.MinInnerBuildingSlotId; s <= VillageConsts.MaxStandardSlotId; s++)
        {
            if (state.SlotBuildingTypes[s] == 36 && state.SlotLevels[s] > 0)
            {
                total += (int)_catalog.GetBuildingLevelStats(36, state.SlotLevels[s]).AttributeValue;
            }
        }
        return total;
    }

    private static int GetQueuedTrapsCount(VillageState state)
    {
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

    private static bool IsUnitEligibleForFacility(int villageTribeId, UnitDefinition unit, TrainingFacilityCategory category)
    {
        if (unit.UnitTypeId == CatalogConsts.TrapUnitId)
        {
            return villageTribeId == 3 && category == TrainingFacilityCategory.Trapper;
        }

        if (unit.TribeId != villageTribeId)
        {
            return false;
        }

        return category switch
        {
            TrainingFacilityCategory.Barracks or TrainingFacilityCategory.GreatBarracks => unit.TrainingBuildingTypeId == 19,
            TrainingFacilityCategory.Stable or TrainingFacilityCategory.GreatStable => unit.TrainingBuildingTypeId == 20,
            TrainingFacilityCategory.Workshop or TrainingFacilityCategory.GreatWorkshop => unit.TrainingBuildingTypeId == 21,
            TrainingFacilityCategory.ResidencePalace => unit.TrainingBuildingTypeId is 25 or 26 or 44 || unit.TribeUnitSlot is 9 or 10,
            _ => false
        };
    }

    private static int GetFacilityBuildingLevel(VillageState state, TrainingFacilityCategory category) =>
        category switch
        {
            TrainingFacilityCategory.Barracks => GetHighestBuildingLevel(state, 19),
            TrainingFacilityCategory.GreatBarracks => GetHighestBuildingLevel(state, 29),
            TrainingFacilityCategory.Stable => GetHighestBuildingLevel(state, 20),
            TrainingFacilityCategory.GreatStable => GetHighestBuildingLevel(state, 30),
            TrainingFacilityCategory.Workshop => GetHighestBuildingLevel(state, 21),
            TrainingFacilityCategory.GreatWorkshop => GetHighestBuildingLevel(state, 49),
            TrainingFacilityCategory.ResidencePalace => Math.Max(
                GetHighestBuildingLevel(state, 25),
                Math.Max(GetHighestBuildingLevel(state, 26), GetHighestBuildingLevel(state, 44))),
            TrainingFacilityCategory.Trapper => GetHighestBuildingLevel(state, 36),
            TrainingFacilityCategory.Healing => Math.Max(GetHighestBuildingLevel(state, 46), GetHighestBuildingLevel(state, 48)),
            _ => 0
        };

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

    private static int CalculateMaxAffordableUnits(VillageState state, in ResourceCost perUnitCost)
    {
        int maxW = perUnitCost.Wood > 0 ? state.Wood / perUnitCost.Wood : int.MaxValue;
        int maxC = perUnitCost.Clay > 0 ? state.Clay / perUnitCost.Clay : int.MaxValue;
        int maxI = perUnitCost.Iron > 0 ? state.Iron / perUnitCost.Iron : int.MaxValue;
        int maxK = perUnitCost.Crop > 0 ? state.Crop / perUnitCost.Crop : int.MaxValue;
        return Math.Min(Math.Min(maxW, maxC), Math.Min(maxI, maxK));
    }

    private static int GetUnitSlot(int unitTypeId)
    {
        if (unitTypeId == CatalogConsts.TrapUnitId)
            return 99;
        int mod = unitTypeId % 10;
        return mod == 0 ? 10 : mod;
    }

    private static TrainingEnrollmentResult Fail(string errorCode) =>
        new(false, errorCode, 0, 0, ResourceCost.Zero, 0, false);
}
