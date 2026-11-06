using Terravia.Domain.Catalog;

namespace Terravia.Domain.Villages;

/// <summary>
/// Pure domain manager for Culture Point expansion thresholds, Residence/Palace/Command Center slot quotas,
/// loyalty regeneration rates, atomic Capital transfers, and newly founded village defaults (<c>FR-014</c>..<c>FR-016</c>).
/// </summary>
public sealed class VillageExpansionManager
{
    private readonly VillageExpansionCatalog _expansionCatalog;
    private readonly IGameCatalog _gameCatalog;
    private readonly IConstructionCalculator _constructionCalculator;
    private readonly IResourceProductionCalculator _productionCalculator;

    public VillageExpansionManager(
        VillageExpansionCatalog? expansionCatalog = null,
        IGameCatalog? gameCatalog = null,
        IConstructionCalculator? constructionCalculator = null,
        IResourceProductionCalculator? productionCalculator = null)
    {
        _expansionCatalog = expansionCatalog ?? VillageExpansionCatalog.Default;
        _gameCatalog = gameCatalog ?? FrozenGameCatalog.Default;
        _constructionCalculator = constructionCalculator ?? new ConstructionCalculator(_gameCatalog);
        _productionCalculator = productionCalculator ?? new ResourceProductionCalculator(_gameCatalog);
    }

    public int GetRequiredCulturePointsForVillageNumber(int targetVillageNumber, CulturePointCurveMode curveMode) =>
        _expansionCatalog.GetRequiredCulturePoints(targetVillageNumber, curveMode);

    /// <summary>
    /// Computes unlocked expansion slots (<c>Residence gid 25</c> -&gt; <c>10/20</c>; <c>Palace gid 26</c> &amp; <c>Command Center gid 44</c> -&gt; <c>10/15/20</c>)
    /// and remaining trainable Settlers (<c>1</c> eq) and Administrators (<c>3</c> eq).
    /// </summary>
    public ExpansionTrainingQuota EvaluateExpansionTrainingQuota(in ExpansionQuotaContext context)
    {
        int unlockedSlots = 0;

        if (context.PalaceLevel >= 20 || context.CommandCenterLevel >= 20)
            unlockedSlots = 3;
        else if (context.PalaceLevel >= 15 || context.CommandCenterLevel >= 15 || context.ResidenceLevel >= 20)
            unlockedSlots = 2;
        else if (context.PalaceLevel >= 10 || context.CommandCenterLevel >= 10 || context.ResidenceLevel >= 10)
            unlockedSlots = 1;

        int totalEquivalents = unlockedSlots * 3;
        int consumedEquivalents = Math.Max(0, context.OccupiedExpansionSlotsCount) * 3
                                + Math.Max(0, context.ExistingOrQueuedAdministratorsCount) * 3
                                + Math.Max(0, context.ExistingOrQueuedSettlersCount);

        int remainingEquivalents = Math.Max(0, totalEquivalents - consumedEquivalents);

        return new ExpansionTrainingQuota(
            UnlockedExpansionSlots: unlockedSlots,
            TotalCapacityInSettlerEquivalents: totalEquivalents,
            ConsumedInSettlerEquivalents: consumedEquivalents,
            RemainingTrainableSettlers: remainingEquivalents,
            RemainingTrainableAdministrators: remainingEquivalents / 3);
    }

    /// <summary>
    /// Computes hourly village loyalty regeneration rate: <c>L_ResidenceOrPalaceOrCommandCenter * ServerSpeed</c> %/hr.
    /// </summary>
    public double CalculateVillageLoyaltyRegenPerHour(
        ReadOnlySpan<int> slotBuildingTypes,
        ReadOnlySpan<int> slotLevels,
        double serverSpeed = 1.0)
    {
        int expansionBuildingLevel = 0;
        int len = Math.Min(slotBuildingTypes.Length, slotLevels.Length);
        for (int s = VillageConsts.MinInnerBuildingSlotId; s < len && s <= VillageConsts.MaxStandardSlotId; s++)
        {
            int gid = slotBuildingTypes[s];
            if (gid is 25 or 26 or 44 && slotLevels[s] > expansionBuildingLevel)
            {
                expansionBuildingLevel = slotLevels[s];
            }
        }

        double speed = serverSpeed > 0 ? serverSpeed : 1.0;
        return expansionBuildingLevel * speed;
    }

    /// <summary>
    /// Computes atomic Capital transfer mutations between <paramref name="oldCapital"/> and <paramref name="newCapital"/> (<c>FR-015</c>).
    /// </summary>
    public CapitalTransferPlan BuildCapitalTransferPlan(
        VillageCapitalState oldCapital,
        VillageCapitalState newCapital)
    {
        ArgumentNullException.ThrowIfNull(oldCapital);
        ArgumentNullException.ThrowIfNull(newCapital);

        if (newCapital.IsArchonVillage)
        {
            return new CapitalTransferPlan(
                false,
                TerraviaDomainErrorCodes.Villages.CannotMakeWorldWonderVillageCapital,
                oldCapital.VillageId,
                newCapital.VillageId,
                [],
                [],
                []);
        }

        bool hasPalaceOrCommandCenter = false;
        for (int s = VillageConsts.MinInnerBuildingSlotId; s <= VillageConsts.MaxStandardSlotId && s < newCapital.SlotBuildingTypes.Length; s++)
        {
            int gid = newCapital.SlotBuildingTypes[s];
            int lvl = s < newCapital.SlotLevels.Length ? newCapital.SlotLevels[s] : 0;
            if (gid is 26 or 44 && lvl >= 1)
            {
                hasPalaceOrCommandCenter = true;
                break;
            }
        }

        if (!hasPalaceOrCommandCenter)
        {
            return new CapitalTransferPlan(
                false,
                TerraviaDomainErrorCodes.Villages.CapitalRequiresPalaceOrCommandCenter,
                oldCapital.VillageId,
                newCapital.VillageId,
                [],
                [],
                []);
        }

        var oldDowngradedFields = new List<int>();
        for (int s = VillageConsts.MinResourceFieldSlotId; s <= VillageConsts.MaxResourceFieldSlotId && s < oldCapital.SlotLevels.Length; s++)
        {
            if (oldCapital.SlotLevels[s] > VillageConsts.MaxNonCapitalResourceFieldLevel)
            {
                oldDowngradedFields.Add(s);
            }
        }

        // Old Capital: destroy Stonemason's Lodge (gid 34) and Teuton Brewery (gid 35)
        var oldDestroyedSlots = new List<int>();
        for (int s = VillageConsts.MinInnerBuildingSlotId; s <= VillageConsts.MaxStandardSlotId && s < oldCapital.SlotBuildingTypes.Length; s++)
        {
            int gid = oldCapital.SlotBuildingTypes[s];
            if (gid is 34 or 35 && oldCapital.SlotLevels[s] > 0)
            {
                oldDestroyedSlots.Add(s);
            }
        }

        // New Capital: destroy non-capital-only buildings (Great Barracks 29, Great Stable 30, Great Warehouse 38, Great Granary 39, Great Workshop 49)
        var newDestroyedSlots = new List<int>();
        for (int s = VillageConsts.MinInnerBuildingSlotId; s <= VillageConsts.MaxStandardSlotId && s < newCapital.SlotBuildingTypes.Length; s++)
        {
            int gid = newCapital.SlotBuildingTypes[s];
            if (gid is 29 or 30 or 38 or 39 or 49 && newCapital.SlotLevels[s] > 0)
            {
                newDestroyedSlots.Add(s);
            }
        }

        return new CapitalTransferPlan(
            true,
            null,
            oldCapital.VillageId,
            newCapital.VillageId,
            oldDowngradedFields,
            oldDestroyedSlots,
            newDestroyedSlots);
    }

    public void ApplyCapitalDemotionToOldCapital(VillageState oldCapital)
    {
        oldCapital.IsCapital = false;

        for (int s = VillageConsts.MinResourceFieldSlotId; s <= VillageConsts.MaxResourceFieldSlotId; s++)
        {
            if (oldCapital.SlotLevels[s] > VillageConsts.MaxNonCapitalResourceFieldLevel)
            {
                oldCapital.SlotLevels[s] = VillageConsts.MaxNonCapitalResourceFieldLevel;
            }
        }

        for (int s = VillageConsts.MinInnerBuildingSlotId; s <= VillageConsts.MaxStandardSlotId; s++)
        {
            if (oldCapital.SlotBuildingTypes[s] is 34 or 35)
            {
                oldCapital.SetSlot(s, 0, 0);
            }
        }

        oldCapital.ConstructionQueue.RemoveAll(q =>
            (q.SlotId is >= 1 and <= 18 && q.TargetLevel > VillageConsts.MaxNonCapitalResourceFieldLevel) ||
            q.BuildingTypeId is 34 or 35);

        RecountVillagePopulationCpAndStorage(oldCapital);
    }

    public void ApplyCapitalPromotionToNewCapital(VillageState newCapital)
    {
        newCapital.IsCapital = true;

        for (int s = VillageConsts.MinInnerBuildingSlotId; s <= VillageConsts.MaxStandardSlotId; s++)
        {
            if (newCapital.SlotBuildingTypes[s] is 29 or 30 or 38 or 39 or 49)
            {
                newCapital.SetSlot(s, 0, 0);
            }
        }

        newCapital.ConstructionQueue.RemoveAll(q => q.BuildingTypeId is 29 or 30 or 38 or 39 or 49);

        RecountVillagePopulationCpAndStorage(newCapital);
    }

    /// <summary>
    /// Creates a newly founded village state matching FR-016 defaults:
    /// Main Building (<c>gid 15</c>) at Level 1 on <c>Slot 26</c>, <c>Slot 1..18</c> at Level 0 matching <paramref name="fieldTopologyId"/>,
    /// <c>Population = 2</c>, <c>CulturePointsPerDay = 1</c>, <c>Loyalty = 100%</c>, <c>IsCapital = false</c>,
    /// <c>750</c> of each resource, and <c>WarehouseCap = GranaryCap = BaseStorageCapacity</c>.
    /// </summary>
    public VillageState CreateNewlyFoundedVillageState(
        int newVillageId,
        int tileId,
        int ownerPlayerId,
        int tribeId,
        int fieldTopologyId,
        long nowMs,
        double serverSpeed = 1.0,
        double storageMultiplier = 1.0)
    {
        int baseStorage = (int)Math.Round(VillageConsts.BaseStorageCapacity * (storageMultiplier > 0 ? storageMultiplier : 1.0), MidpointRounding.AwayFromZero);

        var state = new VillageState(newVillageId, ownerPlayerId, tribeId, isCapital: false)
        {
            TileId = tileId,
            FieldTopologyId = fieldTopologyId,
            IsCapital = false,
            IsArchonVillage = false,
            Population = VillageConsts.FoundingPopulation,
            CulturePointsPerDay = VillageConsts.FoundingCulturePointsPerDay,
            Loyalty = VillageConsts.MaxLoyalty,
            Wood = VillageConsts.FoundingResourceAmount,
            Clay = VillageConsts.FoundingResourceAmount,
            Iron = VillageConsts.FoundingResourceAmount,
            Crop = VillageConsts.FoundingResourceAmount,
            ExactWood = VillageConsts.FoundingResourceAmount,
            ExactClay = VillageConsts.FoundingResourceAmount,
            ExactIron = VillageConsts.FoundingResourceAmount,
            ExactCrop = VillageConsts.FoundingResourceAmount,
            WarehouseCapacity = baseStorage,
            GranaryCapacity = baseStorage,
            LastResourceUpdateAtMs = nowMs,
            ServerSpeed = serverSpeed,
            StorageMultiplier = storageMultiplier
        };

        VillageFieldTopologyCatalog.Default.InitializeOuterResourceSlots(fieldTopologyId, state.SlotBuildingTypes, state.SlotLevels);
        state.SetSlot(VillageConsts.DefaultMainBuildingSlotId, 15, 1);

        return state;
    }

    public bool TryBindExpansionSlot(VillageState parentVillage, int daughterVillageId)
    {
        if (parentVillage.ExpansionSlot1VillageId is null)
        {
            parentVillage.ExpansionSlot1VillageId = daughterVillageId;
            return true;
        }
        if (parentVillage.ExpansionSlot2VillageId is null)
        {
            parentVillage.ExpansionSlot2VillageId = daughterVillageId;
            return true;
        }
        if (parentVillage.ExpansionSlot3VillageId is null)
        {
            parentVillage.ExpansionSlot3VillageId = daughterVillageId;
            return true;
        }
        return false;
    }

    public bool TryFreeExpansionSlot(VillageState parentVillage, int daughterVillageId)
    {
        bool freed = false;
        if (parentVillage.ExpansionSlot1VillageId == daughterVillageId)
        {
            parentVillage.ExpansionSlot1VillageId = null;
            freed = true;
        }
        if (parentVillage.ExpansionSlot2VillageId == daughterVillageId)
        {
            parentVillage.ExpansionSlot2VillageId = null;
            freed = true;
        }
        if (parentVillage.ExpansionSlot3VillageId == daughterVillageId)
        {
            parentVillage.ExpansionSlot3VillageId = null;
            freed = true;
        }
        return freed;
    }

    public void RecountVillagePopulationCpAndStorage(VillageState state)
    {
        int totalPop = 0;
        for (int s = VillageConsts.MinResourceFieldSlotId; s <= VillageConsts.MaxStandardSlotId; s++)
        {
            int gid = state.SlotBuildingTypes[s];
            int lvl = state.SlotLevels[s];
            if (gid <= 0 || lvl <= 0)
                continue;

            var b = _gameCatalog.GetBuilding(gid);
            for (int l = 1; l <= lvl; l++)
            {
                totalPop += b.GetLevelStats(l).Population;
            }
        }

        if (state.WorldWonderBuildingType == 40 && state.WorldWonderLevel > 0)
        {
            var ww = _gameCatalog.GetBuilding(40);
            for (int l = 1; l <= state.WorldWonderLevel; l++)
            {
                totalPop += ww.GetLevelStats(l).Population;
            }
        }

        state.Population = totalPop;
        state.CulturePointsPerDay = state.IsArchonVillage
            ? 0
            : _constructionCalculator.CalculateVillageCulturePoints(
                state.SlotBuildingTypes,
                state.SlotLevels,
                state.WorldWonderLevel);

        var (whCap, grCap) = _productionCalculator.CalculateStorageCapacities(
            state.SlotBuildingTypes,
            state.SlotLevels,
            state.StorageMultiplier);

        state.WarehouseCapacity = whCap;
        state.GranaryCapacity = grCap;
        state.Wood = Math.Min(state.Wood, whCap);
        state.Clay = Math.Min(state.Clay, whCap);
        state.Iron = Math.Min(state.Iron, whCap);
        state.Crop = Math.Min(state.Crop, grCap);
        state.SyncExactResourcesFromInteger();
    }
}
