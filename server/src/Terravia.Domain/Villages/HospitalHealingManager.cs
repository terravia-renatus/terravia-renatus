using Terravia.Domain.Catalog;

namespace Terravia.Domain.Villages;

/// <summary>
/// Orchestrates Hospital (gid 46, 40% save rate, 30 * Level capacity) and Spartan/Viking Big Hospital
/// (gid 48, 50% save rate, 60 * Level capacity) casualty interception, T4 Hero Bandage precedence,
/// soft-cap retention on building damage, conquest/razing cleanup, and 50%-cost sequential healing queues (FR-011..FR-012).
/// </summary>
public sealed class HospitalHealingManager
{
    private readonly IGameCatalog _catalog;
    private readonly ITrainingTimeCalculator _timeCalculator;
    private readonly MilitaryTrainingManager _trainingManager;

    public HospitalHealingManager(
        IGameCatalog? catalog = null,
        ITrainingTimeCalculator? timeCalculator = null,
        MilitaryTrainingManager? trainingManager = null)
    {
        _catalog = catalog ?? FrozenGameCatalog.Default;
        _timeCalculator = timeCalculator ?? new TrainingTimeCalculator(_catalog);
        _trainingManager = trainingManager ?? new MilitaryTrainingManager(_catalog, _timeCalculator);
    }

    public int GetMaxHospitalCapacity(VillageState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        int bigHospLevel = GetHighestBuildingLevel(state, 48);
        if (bigHospLevel > 0)
        {
            return bigHospLevel * VillageConsts.BigHospitalCapacityPerLevel;
        }

        int hospLevel = GetHighestBuildingLevel(state, 46);
        return hospLevel > 0 ? hospLevel * VillageConsts.HospitalCapacityPerLevel : 0;
    }

    public double GetCasualtyInterceptionRate(VillageState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (GetHighestBuildingLevel(state, 48) > 0)
        {
            return VillageConsts.BigHospitalSaveRate;
        }

        if (GetHighestBuildingLevel(state, 46) > 0)
        {
            return VillageConsts.HospitalSaveRate;
        }

        return 0.0;
    }

    public BattleCasualtyAdmissionResult AdmitBattleCasualties(
        VillageState state,
        in BattleCasualtyAdmissionContext context)
    {
        ArgumentNullException.ThrowIfNull(state);

        var deadSpan = context.DeadUnitsBySlot.Span;
        int[] bandagedBySlot = new int[9];
        int[] admittedBySlot = new int[9];
        int totalBandagesConsumed = 0;

        double bandageRate = context.HasSurvivingHeroWithBandages
            ? context.BandageTier switch
            {
                HeroBandageTier.SmallBandage25 => VillageConsts.SmallBandageSaveRate,
                HeroBandageTier.LargeBandage33 => VillageConsts.LargeBandageSaveRate,
                _ => 0.0
            }
            : 0.0;

        int remainingBandages = bandageRate > 0.0 ? Math.Max(0, context.AvailableBandageItemCount) : 0;

        int maxHospitalCapacity = GetMaxHospitalCapacity(state);
        double hospitalRate = GetCasualtyInterceptionRate(state);

        int currentWounded = 0;
        for (int s = 1; s <= 8; s++)
        {
            currentWounded += state.WoundedUnitsBySlot[s];
        }

        int freeHospitalCapacity = Math.Max(0, maxHospitalCapacity - currentWounded);
        int totalAdmitted = 0;

        // Iterate strictly over combat unit slots 1..8 (X9 Cultural Leaders and X10 Settlers are never admitted)
        for (int s = 1; s <= 8; s++)
        {
            int deadInSlot = s < deadSpan.Length ? Math.Max(0, deadSpan[s]) : 0;
            if (deadInSlot == 0)
            {
                continue;
            }

            // Step 1: T4 Hero Bandage battlefield revival first (FR-011)
            int bandaged = 0;
            if (bandageRate > 0.0 && remainingBandages > 0)
            {
                int maxSaveableByBandage = (int)Math.Floor(deadInSlot * bandageRate);
                bandaged = Math.Min(remainingBandages, maxSaveableByBandage);
                bandagedBySlot[s] = bandaged;
                remainingBandages -= bandaged;
                totalBandagesConsumed += bandaged;
            }

            // Step 2: Home Hospital evaluates only remaining un-bandaged casualties
            int deadAfterBandages = deadInSlot - bandaged;
            if (hospitalRate > 0.0 && freeHospitalCapacity > 0 && deadAfterBandages > 0)
            {
                int rawWounded = (int)Math.Floor(deadAfterBandages * hospitalRate);
                int admitted = Math.Min(rawWounded, freeHospitalCapacity);
                if (admitted > 0)
                {
                    admittedBySlot[s] = admitted;
                    state.WoundedUnitsBySlot[s] += admitted;
                    freeHospitalCapacity -= admitted;
                    totalAdmitted += admitted;
                }
            }
        }

        return new BattleCasualtyAdmissionResult(
            BandagedUnitsBySlot: bandagedBySlot,
            TotalBandagesConsumed: totalBandagesConsumed,
            AdmittedWoundedBySlot: admittedBySlot,
            TotalAdmittedToHospital: totalAdmitted,
            RemainingFreeHospitalCapacity: freeHospitalCapacity);
    }

    public TrainingEnrollmentResult EnqueueHealingBatch(
        VillageState state,
        EnqueueHospitalHealingCommand command,
        long nowMs,
        SitterPermissions? sitterPermissions = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);

        if (sitterPermissions.HasValue && !sitterPermissions.Value.HasFlag(SitterPermissions.BuildAndDemolish))
        {
            return Fail(TerraviaDomainErrorCodes.Sitters.PermissionDenied);
        }

        if (command.RequestedAmount <= 0 || command.UnitTypeId is < CatalogConsts.MinUnitId or > CatalogConsts.MaxStandardUnitId)
        {
            return Fail(TerraviaDomainErrorCodes.Villages.InvalidUnitForFacility);
        }

        var unit = _catalog.GetUnit(command.UnitTypeId);
        int slot = unit.TribeUnitSlot;

        if (unit.TribeId != state.TribeId || slot is < 1 or > 8)
        {
            return Fail(TerraviaDomainErrorCodes.Villages.InvalidUnitForFacility);
        }

        int bigHospLevel = GetHighestBuildingLevel(state, 48);
        int hospLevel = GetHighestBuildingLevel(state, 46);
        int activeHospitalGid = bigHospLevel > 0 ? 48 : 46;
        int activeHospitalLevel = bigHospLevel > 0 ? bigHospLevel : hospLevel;

        // Soft-cap retention rule (Clarification Q2): if Hospital was destroyed to Level 0, it must be rebuilt to Level >= 1 to start new healing batches
        if (activeHospitalLevel <= 0)
        {
            return Fail(TerraviaDomainErrorCodes.Villages.TrainingFacilityNotBuilt);
        }

        int availableWounded = state.WoundedUnitsBySlot[slot];
        if (availableWounded <= 0)
        {
            return Fail(TerraviaDomainErrorCodes.Villages.InsufficientWoundedUnits);
        }

        int clampedAmount = Math.Min(command.RequestedAmount, availableWounded);

        // Clamp by affordable 50% resource cost: Cost_heal(u, amt) = ceil(BaseCost(u) * amt / 2)
        while (clampedAmount > 0)
        {
            var checkCost = CalculateBatchHealingCost(unit.Cost, clampedAmount);
            if (state.Crop > 0 &&
                state.Wood >= checkCost.Wood &&
                state.Clay >= checkCost.Clay &&
                state.Iron >= checkCost.Iron &&
                state.Crop >= checkCost.Crop)
            {
                break;
            }
            clampedAmount--;
        }

        if (clampedAmount <= 0)
        {
            return Fail(TerraviaDomainErrorCodes.Villages.InsufficientResources);
        }

        int hdtLevel = GetHighestBuildingLevel(state, 41);
        int effectivePopUpkeep = _trainingManager.CalculateEffectiveUnitPopUpkeep(state.TribeId, unit, hdtLevel);

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

        var deductedCost = CalculateBatchHealingCost(unit.Cost, clampedAmount);
        state.Wood -= deductedCost.Wood;
        state.Clay -= deductedCost.Clay;
        state.Iron -= deductedCost.Iron;
        state.Crop -= deductedCost.Crop;
        state.SyncExactResourcesFromInteger();

        state.WoundedUnitsBySlot[slot] -= clampedAmount;

        int eachTimeSeconds = _timeCalculator.CalculateHospitalHealingEachTimeSeconds(
            command.UnitTypeId,
            activeHospitalGid,
            activeHospitalLevel,
            state.ServerSpeed);

        var (finishesAtMs, wasMerged) = _trainingManager.AppendOrMergeQueueEntry(
            state,
            TrainingFacilityCategory.Healing,
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

    public void ClearHospitalOnConquestOrRaze(VillageState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Array.Clear(state.WoundedUnitsBySlot, 0, state.WoundedUnitsBySlot.Length);
        state.TrainingQueues.RemoveAll(q => q.FacilityCategory == TrainingFacilityCategory.Healing);
    }

    public static ResourceCost CalculateBatchHealingCost(in ResourceCost unitBaseCost, int amount) =>
        new(
            wood: (int)Math.Ceiling(unitBaseCost.Wood * amount / 2.0),
            clay: (int)Math.Ceiling(unitBaseCost.Clay * amount / 2.0),
            iron: (int)Math.Ceiling(unitBaseCost.Iron * amount / 2.0),
            crop: (int)Math.Ceiling(unitBaseCost.Crop * amount / 2.0));

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

    private static TrainingEnrollmentResult Fail(string errorCode) =>
        new(false, errorCode, 0, 0, ResourceCost.Zero, 0, false);
}
