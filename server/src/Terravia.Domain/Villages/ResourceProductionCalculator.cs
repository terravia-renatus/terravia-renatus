using Terravia.Domain.Catalog;
using Terravia.Domain.Oases;

namespace Terravia.Domain.Villages;

/// <summary>
/// Authoritative 6-step village resource production, storage capacity, troop upkeep, and lazy accrual calculator.
/// </summary>
public sealed class ResourceProductionCalculator : IResourceProductionCalculator
{
    private static readonly int[] FieldProductionCurve =
    [
        2, 5, 9, 15, 22, 33, 50, 70, 100, 145, 200,
        280, 375, 495, 635, 800, 1000, 1300, 1600, 2000, 2450
    ];

    private static readonly int[] StandardStorageCurve =
    [
        800, 1200, 1700, 2300, 3100, 4000, 5000, 6300, 7800, 9600, 11800,
        14400, 17600, 21400, 25900, 31300, 37900, 45700, 55100, 66400, 80000
    ];

    private static readonly int[] GreatStorageCurve =
    [
        800, 3600, 5100, 6900, 9300, 12000, 15000, 18900, 23400, 28800, 35400,
        43200, 52800, 64200, 77700, 93900, 113700, 137100, 165300, 199200, 240000
    ];

    private readonly IGameCatalog _catalog;

    public ResourceProductionCalculator(IGameCatalog? catalog = null)
    {
        _catalog = catalog ?? FrozenGameCatalog.Default;
    }

    public static int GetBaseFieldProductionPerHour(int level)
    {
        int clamped = Math.Clamp(level, 0, 20);
        return FieldProductionCurve[clamped];
    }

    public static int GetStandardStorageCapacity(int level)
    {
        int clamped = Math.Clamp(level, 0, 20);
        return StandardStorageCurve[clamped];
    }

    public static int GetGreatStorageCapacity(int level)
    {
        int clamped = Math.Clamp(level, 0, 20);
        return GreatStorageCurve[clamped];
    }

    public ResourceProductionSnapshot CalculateProduction(in VillageProductionContext context)
    {
        var slots = context.SlotBuildingTypes;
        var levels = context.SlotLevels;

        double baseWood = 0;
        double baseClay = 0;
        double baseIron = 0;
        double baseCrop = 0;

        for (int s = VillageConsts.MinResourceFieldSlotId; s <= VillageConsts.MaxResourceFieldSlotId && s < slots.Length; s++)
        {
            int gid = slots[s];
            int lvl = s < levels.Length ? levels[s] : 0;
            int output = GetBaseFieldProductionPerHour(lvl);

            switch (gid)
            {
                case 1: baseWood += output; break;
                case 2: baseClay += output; break;
                case 3: baseIron += output; break;
                case 4: baseCrop += output; break;
            }
        }

        int sawmillLevel = GetHighestBuildingLevel(5, slots, levels);
        int brickyardLevel = GetHighestBuildingLevel(6, slots, levels);
        int ironFoundryLevel = GetHighestBuildingLevel(7, slots, levels);
        int grainMillLevel = GetHighestBuildingLevel(8, slots, levels);
        int bakeryLevel = GetHighestBuildingLevel(9, slots, levels);
        int waterworksLevel = GetHighestBuildingLevel(45, slots, levels);
        int horseTroughLevel = GetHighestBuildingLevel(41, slots, levels);

        double speed = context.ServerSpeed > 0 ? context.ServerSpeed : 1.0;

        int grossWood = ComputeResourceGrossPerHour(
            1, baseWood, context.AnnexedOasisWoodUnits, waterworksLevel,
            sawmillLevel * 5, context.HasStationedLivingHero, context.HeroResourceBonusPoints,
            context.HeroResourceFocusMode, context.PlusWoodBonusActive, speed);

        int grossClay = ComputeResourceGrossPerHour(
            2, baseClay, context.AnnexedOasisClayUnits, waterworksLevel,
            brickyardLevel * 5, context.HasStationedLivingHero, context.HeroResourceBonusPoints,
            context.HeroResourceFocusMode, context.PlusClayBonusActive, speed);

        int grossIron = ComputeResourceGrossPerHour(
            3, baseIron, context.AnnexedOasisIronUnits, waterworksLevel,
            ironFoundryLevel * 5, context.HasStationedLivingHero, context.HeroResourceBonusPoints,
            context.HeroResourceFocusMode, context.PlusIronBonusActive, speed);

        int grossCrop = ComputeResourceGrossPerHour(
            4, baseCrop, context.AnnexedOasisCropUnits, waterworksLevel,
            (grainMillLevel + bakeryLevel) * 5, context.HasStationedLivingHero, context.HeroResourceBonusPoints,
            context.HeroResourceFocusMode, context.PlusCropBonusActive, speed);

        int effectivePop = context.IsArchonVillage
            ? (int)Math.Ceiling(context.Population / 2.0)
            : context.Population;

        int totalTroopUpkeep = CalculateTotalTroopUpkeepPerHour(
            context.FedTroopStacks,
            context.VillageTribeId,
            horseTroughLevel,
            context.IsArchonVillage,
            context.DietControlArtifactMultiplier);

        int netCrop = grossCrop - effectivePop - totalTroopUpkeep;

        return new ResourceProductionSnapshot(
            GrossWoodPerHour: grossWood,
            GrossClayPerHour: grossClay,
            GrossIronPerHour: grossIron,
            GrossCropPerHour: grossCrop,
            EffectivePopulationUpkeep: effectivePop,
            TotalTroopUpkeepPerHour: totalTroopUpkeep,
            NetCropPerHour: netCrop);
    }

    public (int WarehouseCap, int GranaryCap) CalculateStorageCapacities(
        ReadOnlySpan<int> slotBuildingTypes,
        ReadOnlySpan<int> slotLevels,
        double storageMultiplier = 1.0)
    {
        double mult = storageMultiplier > 0 ? storageMultiplier : 1.0;
        int totalWarehouse = 0;
        int totalGranary = 0;

        int len = Math.Min(slotBuildingTypes.Length, slotLevels.Length);
        for (int s = VillageConsts.MinInnerBuildingSlotId; s < len && s <= VillageConsts.MaxStandardSlotId; s++)
        {
            int gid = slotBuildingTypes[s];
            int lvl = slotLevels[s];
            if (lvl <= 0)
                continue;

            switch (gid)
            {
                case 10:
                    totalWarehouse += (int)Math.Round(GetStandardStorageCapacity(lvl) * mult, MidpointRounding.AwayFromZero);
                    break;
                case 38:
                    totalWarehouse += (int)Math.Round(GetGreatStorageCapacity(lvl) * mult, MidpointRounding.AwayFromZero);
                    break;
                case 11:
                    totalGranary += (int)Math.Round(GetStandardStorageCapacity(lvl) * mult, MidpointRounding.AwayFromZero);
                    break;
                case 39:
                    totalGranary += (int)Math.Round(GetGreatStorageCapacity(lvl) * mult, MidpointRounding.AwayFromZero);
                    break;
            }
        }

        int baseFloor = (int)Math.Round(VillageConsts.BaseStorageCapacity * mult, MidpointRounding.AwayFromZero);
        return (
            WarehouseCap: totalWarehouse > 0 ? totalWarehouse : baseFloor,
            GranaryCap: totalGranary > 0 ? totalGranary : baseFloor);
    }

    public LazyAccrualResult AccrueVillageResourcesAndLoyalty(
        in VillageAccrualInput input,
        long nowMs)
    {
        double elapsedHours = Math.Max(0.0, (nowMs - input.LastUpdatedMs) / 3_600_000.0);

        double wood = Math.Clamp(
            input.CurrentWood + input.GrossWoodPerHour * elapsedHours,
            0.0,
            input.WarehouseCap);

        double clay = Math.Clamp(
            input.CurrentClay + input.GrossClayPerHour * elapsedHours,
            0.0,
            input.WarehouseCap);

        double iron = Math.Clamp(
            input.CurrentIron + input.GrossIronPerHour * elapsedHours,
            0.0,
            input.WarehouseCap);

        double crop = Math.Clamp(
            input.CurrentCrop + input.NetCropPerHour * elapsedHours,
            0.0,
            input.GranaryCap);

        double loyalty = Math.Clamp(
            input.CurrentLoyalty + input.LoyaltyRegenPerHour * elapsedHours,
            VillageConsts.MinLoyalty,
            VillageConsts.MaxLoyalty);

        return new LazyAccrualResult(
            Wood: wood,
            Clay: clay,
            Iron: iron,
            Crop: crop,
            Loyalty: loyalty,
            UpdatedAtMs: Math.Max(input.LastUpdatedMs, nowMs));
    }

    private int CalculateTotalTroopUpkeepPerHour(
        IReadOnlyList<StarvationCandidateStack>? stacks,
        int villageTribeId,
        int horseTroughLevel,
        bool isArchonVillage,
        double dietControlMultiplier)
    {
        if (stacks is null || stacks.Count == 0)
            return 0;

        double rawUpkeep = 0.0;
        bool hasRomanTrough = villageTribeId == 1 && horseTroughLevel >= 10;

        foreach (var stack in stacks)
        {
            // FR-008: Captured Nature animals (OwnerVillageId == 0) consume 0 crop upkeep
            if (stack.OwnerVillageId == OasisConsts.CapturedNatureOwnerVillageId &&
                stack.Tier == StarvationPriorityTier.VillageReinforcement)
            {
                continue;
            }

            var counts = stack.UnitCounts;
            for (int u = 1; u < counts.Length && u <= 90; u++)
            {
                int count = counts[u];
                if (count <= 0)
                    continue;

                // Nature units (u31..u40) consume 0 upkeep when stationed as captured defense
                if (u is >= OasisConsts.MinNatureUnitTypeId and <= OasisConsts.MaxNatureUnitTypeId)
                    continue;

                int unitUpkeep = _catalog.GetUnit(u).UpkeepCropPerHour;
                if (hasRomanTrough)
                {
                    if (u == 4 && horseTroughLevel >= 10) unitUpkeep = Math.Max(0, unitUpkeep - 1);
                    else if (u == 5 && horseTroughLevel >= 15) unitUpkeep = Math.Max(0, unitUpkeep - 1);
                    else if (u == 6 && horseTroughLevel >= 20) unitUpkeep = Math.Max(0, unitUpkeep - 1);
                }

                rawUpkeep += count * unitUpkeep;
            }

            if (stack.HasHero)
            {
                rawUpkeep += VillageConsts.HeroBaseCropUpkeep;
            }
        }

        if (isArchonVillage)
        {
            rawUpkeep *= 0.5;
        }

        double artifactFactor = dietControlMultiplier > 0.0 ? dietControlMultiplier : 1.0;
        rawUpkeep *= artifactFactor;

        return (int)Math.Round(rawUpkeep, MidpointRounding.AwayFromZero);
    }

    private static int ComputeResourceGrossPerHour(
        int resourceId,
        double baseFieldSum,
        int oasisBonusUnits,
        int waterworksLevel,
        int bonusBuildingPercent,
        bool hasStationedLivingHero,
        int heroResourcePoints,
        HeroResourceFocusMode heroFocusMode,
        bool plusBonusActive,
        double serverSpeed)
    {
        // Step 1: Base field sum B_r
        double b = baseFieldSum;

        // Step 2: Annexed oasis bonus amplified by Egyptian Waterworks (gid 45)
        double oasisFactor = 0.25 * (1.0 + 0.05 * Math.Clamp(waterworksLevel, 0, 20));
        double a1 = b + b * oasisFactor * Math.Max(0, oasisBonusUnits);

        // Step 3: Resource bonus buildings (Sawmill 5, Brickyard 6, Iron Foundry 7, Grain Mill 8 + Bakery 9)
        double a2 = a1 + (a1 / 100.0) * Math.Clamp(bonusBuildingPercent, 0, 50);

        // Step 4: Stationed living Hero resource contribution (Balanced = 3/pt, SingleResource = 10/pt)
        double heroBonus = 0.0;
        if (hasStationedLivingHero && heroResourcePoints > 0)
        {
            int pts = Math.Clamp(heroResourcePoints, 0, 100);
            if (heroFocusMode == HeroResourceFocusMode.Balanced)
            {
                heroBonus = 3.0 * pts;
            }
            else if ((int)heroFocusMode == resourceId)
            {
                heroBonus = 10.0 * pts;
            }
        }
        double a3 = a2 + heroBonus;

        // Step 5: Plus account +25% bonus
        double a4 = plusBonusActive ? a3 * 1.25 : a3;

        // Step 6: Server speed scaling and rounding
        return (int)Math.Round(a4 * serverSpeed, MidpointRounding.AwayFromZero);
    }

    private static int GetHighestBuildingLevel(int buildingTypeId, int[] slotBuildingTypes, int[] slotLevels)
    {
        int highest = 0;
        int len = Math.Min(slotBuildingTypes.Length, slotLevels.Length);
        for (int s = VillageConsts.MinInnerBuildingSlotId; s < len && s <= VillageConsts.MaxStandardSlotId; s++)
        {
            if (slotBuildingTypes[s] == buildingTypeId && slotLevels[s] > highest)
            {
                highest = slotLevels[s];
            }
        }
        return highest;
    }
}
