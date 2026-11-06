using Terravia.Domain.Villages;

namespace Terravia.Domain.Catalog;

/// <summary>
/// Stateless, pure C# rule engine for building prerequisite validation, slot upgrade eligibility,
/// Academy unit research prerequisites, and Main Building demolition rules.
/// </summary>
public sealed class BuildingRuleCatalog : IBuildingRuleCatalog
{
    private static readonly int[] WallGids = [31, 32, 33, 42, 43, 47, 50];

    private readonly IGameCatalog _catalog;
    private readonly IConstructionCalculator _calculator;

    public BuildingRuleCatalog(IGameCatalog catalog, IConstructionCalculator calculator)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
    }

    public bool MeetsPrerequisites(int buildingTypeId, in VillagePrerequisiteContext context)
    {
        if (buildingTypeId is < 1 or > 50)
            return false;

        var building = _catalog.GetBuilding(buildingTypeId);
        var slots = context.SlotBuildingTypes;
        var levels = context.SlotLevels;

        // 1. Tribe allowlist / denylist
        if (!building.AllowedTribeIds.IsEmpty && !building.AllowedTribeIds.Contains(context.TribeId))
            return false;

        if (!building.DeniedTribeIds.IsEmpty && building.DeniedTribeIds.Contains(context.TribeId))
            return false;

        // 2. Capital vs. Non-Capital constraint
        if (building.CapitalConstraint == CapitalConstraint.CapitalOnly && !context.IsCapital)
            return false;

        if (building.CapitalConstraint == CapitalConstraint.NonCapitalOnly && context.IsCapital)
            return false;

        // 3. Special-case context rules for mutual exclusions, account uniqueness, and artifacts
        switch (buildingTypeId)
        {
            case 25: // Residence: not Huns (checked above), MB >= 5, no Residence (25), no Palace (26), no Command Center (44)
                return GetHighestBuildingLevel(15, slots, levels) >= 5 &&
                       !HasBuildingInVillage(25, slots) &&
                       !HasBuildingInVillage(26, slots) &&
                       !HasBuildingInVillage(44, slots);

            case 26: // Palace: not Huns, Embassy >= 1, MB >= 5, no Palace in village, no Palace across account, no Residence, no CC
                return GetHighestBuildingLevel(18, slots, levels) >= 1 &&
                       GetHighestBuildingLevel(15, slots, levels) >= 5 &&
                       !HasBuildingInVillage(26, slots) &&
                       !context.HasAccountPalace &&
                       !HasBuildingInVillage(25, slots) &&
                       !HasBuildingInVillage(44, slots);

            case 27: // Treasury: cannot be built in a World Wonder village
                if (context.IsWorldWonderVillage)
                    return false;
                break;

            case 34: // Stonemason's Lodge: CapitalOnly (checked above), MB >= 5, Residence == 0, Palace >= 3 (or Command Center >= 3 for Huns)
                int commandLevel = context.TribeId == 6
                    ? GetHighestBuildingLevel(44, slots, levels)
                    : GetHighestBuildingLevel(26, slots, levels);
                return commandLevel >= 3 &&
                       GetHighestBuildingLevel(15, slots, levels) >= 5 &&
                       GetHighestBuildingLevel(25, slots, levels) == 0 &&
                       !HasBuildingInVillage(34, slots);

            case 38: // Great Warehouse
            case 39: // Great Granary
                bool qualifiesForGreatStorage = context.IsArchonVillage || context.IsWorldWonderVillage || context.HasGreatStorageArtifact;
                if (!qualifiesForGreatStorage)
                    return false;
                if (GetHighestBuildingLevel(15, slots, levels) < 10)
                    return false;
                return !HasBuildingInVillage(buildingTypeId, slots) ||
                       GetHighestBuildingLevel(buildingTypeId, slots, levels) == 20;

            case 40: // Wonder of the World
                if (!context.IsWorldWonderVillage && !context.IsArchonVillage)
                    return false;
                if (context.IsArchonVillage && context.TribeId == 5)
                    return true;
                int effectiveWwLevel = Math.Max(context.WorldWonderLevel, context.MaxQueuedWorldWonderLevel);
                if (effectiveWwLevel < 50)
                    return context.OwnedWwPlanCount >= 1;
                return context.OwnedWwPlanCount >= 1 && context.AllianceWwPlanCount >= 2;

            case 49: // Great Workshop: requires round config GREAT_WKS enabled
                if (!context.GreatWorkshopEnabled)
                    return false;
                break;
        }

        // 4. Declarative MinLevelRequirements (>=)
        foreach (var req in building.MinLevelRequirements)
        {
            if (GetHighestBuildingLevel(req.BuildingTypeId, slots, levels) < req.Level)
                return false;
        }

        // 5. Declarative ExactLevelRequirements (==)
        foreach (var req in building.ExactLevelRequirements)
        {
            if (GetHighestBuildingLevel(req.BuildingTypeId, slots, levels) != req.Level)
                return false;
        }

        // 6. Declarative AnyOfMinLevelRequirements (at least one >=)
        if (!building.AnyOfMinLevelRequirements.IsEmpty)
        {
            bool anyMet = false;
            foreach (var req in building.AnyOfMinLevelRequirements)
            {
                if (GetHighestBuildingLevel(req.BuildingTypeId, slots, levels) >= req.Level)
                {
                    anyMet = true;
                    break;
                }
            }
            if (!anyMet)
                return false;
        }

        // 7. Uniqueness & RebuildAtLevel
        if (building.IsUniquePerVillage)
        {
            bool isBuilt = HasBuildingInVillage(buildingTypeId, slots);
            if (building.RebuildAtLevel is { } rebuildLevel)
            {
                return !isBuilt || GetHighestBuildingLevel(buildingTypeId, slots, levels) == rebuildLevel;
            }
            return !isBuilt;
        }

        return true;
    }

    public BuildSlotEvaluation EvaluateSlotUpgrade(
        int slotId,
        int buildingTypeId,
        in VillageConstructionContext context)
    {
        if (buildingTypeId is < 1 or > 50)
            return new BuildSlotEvaluation(BuildEligibilityStatus.InvalidSlotPlacement, 0, ResourceCost.Zero, 0);

        var building = _catalog.GetBuilding(buildingTypeId);
        var prereqCtx = context.PrerequisiteContext;

        // 1. Validate slot placement topology
        if (!IsValidSlotForBuilding(slotId, buildingTypeId, building.SlotPlacement, prereqCtx))
            return new BuildSlotEvaluation(BuildEligibilityStatus.InvalidSlotPlacement, 0, ResourceCost.Zero, 0);

        // 2. Check if slot is currently being demolished
        if (context.ActiveDemolitionSlotId == slotId)
            return new BuildSlotEvaluation(BuildEligibilityStatus.SlotBeingDemolished, 0, ResourceCost.Zero, 0);

        int currentSlotGid = slotId == 99
            ? (prereqCtx.IsWorldWonderVillage ? 40 : 0)
            : prereqCtx.SlotBuildingTypes[slotId];
        int currentSlotLevel = slotId == 99
            ? prereqCtx.WorldWonderLevel
            : prereqCtx.SlotLevels[slotId];

        bool isNewConstruction = currentSlotGid == 0 || (currentSlotLevel == 0 && context.QueuedJobsForTargetSlot == 0);

        // If slot already holds a different building type, reject
        if (currentSlotGid != 0 && currentSlotGid != buildingTypeId)
            return new BuildSlotEvaluation(BuildEligibilityStatus.InvalidSlotPlacement, 0, ResourceCost.Zero, 0);

        // 3. Check prerequisites (for new construction or World Wonder plan threshold across queued levels)
        if (isNewConstruction || buildingTypeId == 40)
        {
            if (!MeetsPrerequisites(buildingTypeId, in prereqCtx))
                return new BuildSlotEvaluation(BuildEligibilityStatus.PrerequisitesNotMet, 0, ResourceCost.Zero, 0);
        }

        // 4. Max level & queued level checks
        int maxAllowedLevel = (!prereqCtx.IsCapital && buildingTypeId <= 4)
            ? building.NonCapitalMaxLevel
            : building.MaxLevel;

        if (currentSlotLevel >= maxAllowedLevel)
            return new BuildSlotEvaluation(BuildEligibilityStatus.MaxLevelReached, currentSlotLevel, ResourceCost.Zero, 0);

        int targetLevel = currentSlotLevel + 1 + context.QueuedJobsForTargetSlot;
        if (targetLevel > maxAllowedLevel)
            return new BuildSlotEvaluation(BuildEligibilityStatus.MaxLevelUnderConstruction, targetLevel, ResourceCost.Zero, 0);

        var levelStats = building.GetLevelStats(targetLevel);
        int mbLevel = GetHighestBuildingLevel(15, prereqCtx.SlotBuildingTypes, prereqCtx.SlotLevels);
        int buildTimeSeconds = _calculator.CalculateBuildTimeSeconds(buildingTypeId, targetLevel, mbLevel, context.ServerSpeed);

        // 5. Crop starvation guard: if not Cropland (gid 4) and net crop - pop <= 0 with no Cropland queued
        if (buildingTypeId != 4 &&
            !context.HasCroplandUpgradeQueued &&
            (context.NetCropProductionPerHour - levelStats.Population) <= 0 &&
            levelStats.Population > 0)
        {
            return new BuildSlotEvaluation(BuildEligibilityStatus.CropStarvationBlock, targetLevel, levelStats.Cost, buildTimeSeconds);
        }

        // 6. Storage capacity checks (Warehouse evaluated before Granary)
        if (levelStats.Cost.Wood > context.WarehouseCapacity ||
            levelStats.Cost.Clay > context.WarehouseCapacity ||
            levelStats.Cost.Iron > context.WarehouseCapacity)
        {
            return new BuildSlotEvaluation(BuildEligibilityStatus.WarehouseCapacityInsufficient, targetLevel, levelStats.Cost, buildTimeSeconds);
        }

        if (levelStats.Cost.Crop > context.GranaryCapacity)
        {
            return new BuildSlotEvaluation(BuildEligibilityStatus.GranaryCapacityInsufficient, targetLevel, levelStats.Cost, buildTimeSeconds);
        }

        // 7. Determine queue slot availability (Roman dual-queue vs Standard, Plus Waiting Loop, Master Builder)
        bool isRomanDualQueue = prereqCtx.TribeId == 1 || context.AllowAllTribeRomanQueue;
        bool isResourceField = slotId is >= 1 and <= 18;

        bool canStartActiveNow;
        if (isRomanDualQueue)
        {
            canStartActiveNow = isResourceField
                ? context.ActiveResourceFieldJobs == 0
                : context.ActiveInnerBuildingJobs == 0;
        }
        else
        {
            canStartActiveNow = (context.ActiveResourceFieldJobs + context.ActiveInnerBuildingJobs) == 0;
        }

        bool canUseWaitingLoop = !canStartActiveNow &&
                                 context.HasPlusSubscription &&
                                 context.WaitingLoopJobs == 0 &&
                                 context.MasterBuilderJobs == 0;

        bool hasResourcesNow = context.AvailableWood >= levelStats.Cost.Wood &&
                               context.AvailableClay >= levelStats.Cost.Clay &&
                               context.AvailableIron >= levelStats.Cost.Iron &&
                               context.AvailableCrop >= levelStats.Cost.Crop;

        if (!hasResourcesNow)
        {
            if (context.MasterBuilderJobs < context.MaxMasterBuilderSlots)
                return new BuildSlotEvaluation(BuildEligibilityStatus.CanReserveMasterBuilder, targetLevel, levelStats.Cost, buildTimeSeconds);

            return new BuildSlotEvaluation(BuildEligibilityStatus.InsufficientResources, targetLevel, levelStats.Cost, buildTimeSeconds);
        }

        if (canStartActiveNow)
            return new BuildSlotEvaluation(BuildEligibilityStatus.CanBuildActive, targetLevel, levelStats.Cost, buildTimeSeconds);

        if (canUseWaitingLoop)
            return new BuildSlotEvaluation(BuildEligibilityStatus.CanBuildWaitingLoop, targetLevel, levelStats.Cost, buildTimeSeconds);

        if (context.MasterBuilderJobs < context.MaxMasterBuilderSlots)
            return new BuildSlotEvaluation(BuildEligibilityStatus.CanReserveMasterBuilder, targetLevel, levelStats.Cost, buildTimeSeconds);

        return new BuildSlotEvaluation(BuildEligibilityStatus.QueueFull, targetLevel, levelStats.Cost, buildTimeSeconds);
    }

    public bool MeetsUnitResearchPrerequisites(
        int unitTypeId,
        ReadOnlySpan<int> slotBuildingTypes,
        ReadOnlySpan<int> slotLevels)
    {
        if (unitTypeId is < 1 or > 90)
            return false;

        var unit = _catalog.GetUnit(unitTypeId);
        if (unit.ResearchPrerequisites.IsEmpty)
            return false;

        foreach (var req in unit.ResearchPrerequisites)
        {
            if (GetHighestBuildingLevelSpan(req.BuildingTypeId, slotBuildingTypes, slotLevels) < req.Level)
                return false;
        }

        return true;
    }

    public DemolitionEligibilityResult EvaluateDemolition(
        int slotId,
        bool isInstantGold,
        in VillageDemolitionContext context,
        double serverSpeed = 1.0)
    {
        // Only inner slots (19..40) or WW slot (99) can be demolished
        if (slotId is (< 19 or > 40) and not 99)
            return new DemolitionEligibilityResult(DemolitionStatus.InvalidSlot, 0, 0);

        if (context.MainBuildingLevel < 10)
            return new DemolitionEligibilityResult(DemolitionStatus.MainBuildingBelowLevel10, 0, 0);

        if (context.TargetSlotBuildingTypeId <= 0 || context.TargetSlotCurrentLevel <= 0)
            return new DemolitionEligibilityResult(DemolitionStatus.EmptySlot, 0, 0);

        if (context.SlotHasQueuedUpgrade)
            return new DemolitionEligibilityResult(DemolitionStatus.SlotHasActiveUpgrade, 0, 0);

        if (!isInstantGold && context.VillageHasActiveDemolition)
            return new DemolitionEligibilityResult(DemolitionStatus.DemolitionAlreadyActive, 0, 0);

        // Embassy (gid 18) Alliance Founder safeguard
        if (context.TargetSlotBuildingTypeId == 18 &&
            context.IsAllianceOwner &&
            context.AllianceMemberCount > 1)
        {
            int minEmbassyLevel = Math.Max(3, (int)Math.Ceiling(context.AllianceMemberCount / 3.0));
            if (context.TargetSlotCurrentLevel <= minEmbassyLevel &&
                context.QualifyingEmbassyCountAcrossPlayerVillages <= 1)
            {
                return new DemolitionEligibilityResult(DemolitionStatus.CannotDemolishLastAllianceEmbassy, 0, 0);
            }
        }

        if (isInstantGold)
        {
            if (context.PlayerGoldBalance < 10)
                return new DemolitionEligibilityResult(DemolitionStatus.InsufficientGold, 0, 0);

            return new DemolitionEligibilityResult(DemolitionStatus.Allowed, TargetLevel: 0, DurationSeconds: 0);
        }

        int duration = _calculator.CalculateDemolitionTimeSeconds(
            context.TargetSlotBuildingTypeId,
            context.TargetSlotCurrentLevel,
            context.MainBuildingLevel,
            serverSpeed);

        return new DemolitionEligibilityResult(
            DemolitionStatus.Allowed,
            TargetLevel: context.TargetSlotCurrentLevel - 1,
            DurationSeconds: duration);
    }

    private bool IsValidSlotForBuilding(
        int slotId,
        int buildingTypeId,
        SlotPlacementRule placementRule,
        in VillagePrerequisiteContext context)
    {
        return placementRule switch
        {
            SlotPlacementRule.ResourceField1To18 => slotId is >= 1 and <= 18,
            SlotPlacementRule.InnerSlot19To38 => slotId is >= 19 and <= 38,
            SlotPlacementRule.RallyPointSlot39 => slotId == 39 && buildingTypeId == 16,
            SlotPlacementRule.WallSlot40 => slotId == 40 &&
                                            WallGids.Contains(buildingTypeId) &&
                                            _catalog.GetTribe(context.TribeId).WallBuildingTypeId == buildingTypeId,
            SlotPlacementRule.WorldWonderSlot99 => slotId == 99 && buildingTypeId == 40 && (context.IsWorldWonderVillage || context.IsArchonVillage),
            _ => false
        };
    }

    private static bool HasBuildingInVillage(int buildingTypeId, int[] slotBuildingTypes)
    {
        for (int i = 1; i < slotBuildingTypes.Length && i <= 40; i++)
        {
            if (slotBuildingTypes[i] == buildingTypeId)
                return true;
        }
        return false;
    }

    private static int GetHighestBuildingLevel(int buildingTypeId, int[] slotBuildingTypes, int[] slotLevels)
    {
        return GetHighestBuildingLevelSpan(buildingTypeId, slotBuildingTypes, slotLevels);
    }

    private static int GetHighestBuildingLevelSpan(
        int buildingTypeId,
        ReadOnlySpan<int> slotBuildingTypes,
        ReadOnlySpan<int> slotLevels)
    {
        int highest = 0;
        int len = Math.Min(slotBuildingTypes.Length, slotLevels.Length);
        for (int i = 1; i < len && i <= 40; i++)
        {
            if (slotBuildingTypes[i] == buildingTypeId && slotLevels[i] > highest)
            {
                highest = slotLevels[i];
            }
        }
        return highest;
    }
}
