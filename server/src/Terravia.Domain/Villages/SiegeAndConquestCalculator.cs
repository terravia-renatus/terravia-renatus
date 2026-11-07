using Terravia.Domain.Catalog;

namespace Terravia.Domain.Villages;

/// <summary>
/// Pure domain calculator for administrator loyalty reduction (<c>u9, u19, u29, u49, u59, u69, u79, u89</c>),
/// village conquest state resets (<c>FR-017</c>), bigger-player <c>-1</c> building level penalties,
/// 8-tribe incompatible building cleanup (<c>gid 35, 36, 41, 44, 45, 48</c>), and village razing (<c>Population == 0</c>).
/// </summary>
public sealed class SiegeAndConquestCalculator
{
    private readonly IGameCatalog _catalog;
    private readonly VillageExpansionManager _expansionManager;

    public SiegeAndConquestCalculator(
        IGameCatalog? catalog = null,
        VillageExpansionManager? expansionManager = null)
    {
        _catalog = catalog ?? FrozenGameCatalog.Default;
        _expansionManager = expansionManager ?? new VillageExpansionManager(gameCatalog: _catalog);
    }

    public static (int MinPersuasion, int MaxPersuasion) GetAdministratorBasePersuasionRange(int unitTypeId) =>
        unitTypeId switch
        {
            9 => (20, 30), // Roman Senator (u9)
            19 or 29 or 49 or 59 or 69 or 79 or 89 => (20, 25),
            _ => (0, 0)
        };

    public ConquestResolutionResult EvaluateAdministratorConquest(
        in ConquestEvaluationContext context,
        Func<int, int, int>? inclusiveRandomProvider = null)
    {
        if (context.SurvivingAdministratorCount <= 0)
        {
            return new ConquestResolutionResult(false, null, 0.0, context.CurrentTargetLoyalty, false, 0, false);
        }

        // Cannot conquer a Capital, an Archon WW Building Plan village, or a player's sole village (unless IsArchon)
        if (context.TargetIsCapital ||
            context.TargetIsWwBuildingPlanVillage ||
            (!context.TargetIsArchonVillage && context.DefenderOwnedVillageCount <= 1))
        {
            return new ConquestResolutionResult(
                false,
                TerraviaDomainErrorCodes.Villages.CannotConquerCapitalOrLastVillage,
                0.0,
                context.CurrentTargetLoyalty,
                false,
                0,
                false);
        }

        if (context.TargetHasStandingExpansionBuilding)
        {
            return new ConquestResolutionResult(
                false,
                TerraviaDomainErrorCodes.Villages.TargetExpansionBuildingStillStanding,
                0.0,
                context.CurrentTargetLoyalty,
                false,
                0,
                false);
        }

        if (context.AttackerLaunchingVillageFreeExpansionSlots <= 0)
        {
            return new ConquestResolutionResult(
                false,
                TerraviaDomainErrorCodes.Villages.NoFreeExpansionSlot,
                0.0,
                context.CurrentTargetLoyalty,
                false,
                0,
                false);
        }

        if (context.AttackerAccumulatedCulturePoints < context.RequiredCulturePointsForNextVillage)
        {
            return new ConquestResolutionResult(
                false,
                TerraviaDomainErrorCodes.Villages.InsufficientCulturePointsForExpansion,
                0.0,
                context.CurrentTargetLoyalty,
                false,
                0,
                false);
        }

        var (minRand, maxRand) = GetAdministratorBasePersuasionRange(context.SurvivingAdministratorUnitTypeId);
        if (maxRand <= 0)
        {
            return new ConquestResolutionResult(false, null, 0.0, context.CurrentTargetLoyalty, false, 0, false);
        }

        var rng = inclusiveRandomProvider ?? ((min, max) => Random.Shared.Next(min, max + 1));
        int attackerCelBonus = context.AttackerVillageHasActiveGreatCelebration ? 5 : 0;
        int defenderCelBonus = context.DefenderVillageHasActiveGreatCelebration ? 5 : 0;
        double morale = Math.Max(1.0, context.MoraleBonus);
        double meadMultiplier = context.AttackerHasActiveTeutonMeadFestival ? 0.5 : 1.0;

        double totalReduction = 0.0;
        for (int i = 0; i < context.SurvivingAdministratorCount; i++)
        {
            int basePersuasion = rng(minRand, maxRand);
            double perAdmin = ((basePersuasion + attackerCelBonus - defenderCelBonus) / morale) * meadMultiplier;
            totalReduction += Math.Max(1.0, perAdmin);
        }

        double newLoyalty = context.CurrentTargetLoyalty - totalReduction;
        if (newLoyalty <= 0.0)
        {
            // Pre-conquest population comparison for bigger-player -1 building level penalty
            bool applyPenalty = !context.TargetIsArchonVillage &&
                                context.AttackerTotalAccountPopulation > context.DefenderTotalAccountPopulation;

            return new ConquestResolutionResult(
                true,
                null,
                totalReduction,
                0.0,
                WasVillageConquered: true,
                AdministratorsConsumed: 1,
                ApplyBiggerPlayerMinusOneBuildingLevelPenalty: applyPenalty);
        }

        return new ConquestResolutionResult(
            true,
            null,
            totalReduction,
            newLoyalty,
            WasVillageConquered: false,
            AdministratorsConsumed: 0,
            ApplyBiggerPlayerMinusOneBuildingLevelPenalty: false);
    }

    /// <summary>
    /// Applies the full village capture state reset (<c>FR-017</c>):
    /// pre-conquest bigger-player <c>-1</c> building level penalty, 8-tribe incompatible building destruction
    /// (<c>gid 35, 36, 41, 44, 45, 48</c>), Wall (<c>Slot 40</c>) destruction, active queue cancellations,
    /// oasis release list extraction, and ownership transfer.
    /// </summary>
    public IReadOnlyList<int> ApplyVillageConquestStateReset(
        VillageState conqueredVillage,
        int newOwnerPlayerId,
        int newOwnerTribeId,
        bool applyBiggerPlayerMinusOnePenalty)
    {
        ArgumentNullException.ThrowIfNull(conqueredVillage);

        int previousOwnerPlayerId = conqueredVillage.OwnerPlayerId;
        bool isCrossPlayerConquest = previousOwnerPlayerId != newOwnerPlayerId;

        // 1. Bigger-player -1 building level penalty (evaluated using pre-conquest account populations)
        if (applyBiggerPlayerMinusOnePenalty && !conqueredVillage.IsArchonVillage)
        {
            for (int s = VillageConsts.MinResourceFieldSlotId; s <= VillageConsts.MaxStandardSlotId; s++)
            {
                int gid = conqueredVillage.SlotBuildingTypes[s];
                int lvl = conqueredVillage.SlotLevels[s];
                if (gid <= 0 || lvl <= 0)
                    continue;

                // Brewery (35), Trapper (36), Horse Drinking Trough (41), and Waterworks (45) drop to 0
                if (gid is 35 or 36 or 41 or 45)
                {
                    conqueredVillage.SetSlot(s, 0, 0);
                    continue;
                }

                // Main Building (15) cannot drop below Level 1
                if (gid == 15 && lvl == 1)
                    continue;

                int newLvl = lvl - 1;
                if (newLvl <= 0 && s >= VillageConsts.MinInnerBuildingSlotId)
                {
                    conqueredVillage.SetSlot(s, 0, 0);
                }
                else
                {
                    conqueredVillage.SetSlot(s, gid, Math.Max(0, newLvl));
                }
            }
        }

        // 2. Destroy tribe-exclusive buildings (gid 35, 36, 41, 44, 45, 48) incompatible with the new owner's tribe
        // (and destroy 35, 36, 41, 45 on cross-player conquest)
        for (int s = VillageConsts.MinInnerBuildingSlotId; s <= VillageConsts.MaxStandardSlotId; s++)
        {
            int gid = conqueredVillage.SlotBuildingTypes[s];
            if (gid <= 0)
                continue;

            if (isCrossPlayerConquest && gid is 35 or 36 or 41 or 45)
            {
                conqueredVillage.SetSlot(s, 0, 0);
                continue;
            }

            if (gid is 35 or 36 or 41 or 44 or 45 or 48 && !IsBuildingCompatibleWithTribe(gid, newOwnerTribeId))
            {
                conqueredVillage.SetSlot(s, 0, 0);
            }
        }

        // 3. Destroy defensive Wall (Slot 40 -> Level 0)
        conqueredVillage.SetSlot(VillageConsts.WallSlotId, 0, 0);

        // 4. Release all annexed oases back to wild Nature state
        var releasedOasisTileIds = conqueredVillage.AnnexedOasisTileIds.ToList();
        conqueredVillage.AnnexedOasisTileIds.Clear();
        conqueredVillage.AnnexedOasisWoodUnits = 0;
        conqueredVillage.AnnexedOasisClayUnits = 0;
        conqueredVillage.AnnexedOasisIronUnits = 0;
        conqueredVillage.AnnexedOasisCropUnits = 0;

        // 5. Cancel all active village queues & clear own troop/trapped/research/hospital state
        conqueredVillage.ConstructionQueue.Clear();
        conqueredVillage.ActiveDemolition = null;
        conqueredVillage.HasActiveAcademyOrSmithyResearch = false;
        conqueredVillage.TrainingQueues.Clear();
        conqueredVillage.ResearchQueue.Clear();
        Array.Clear(conqueredVillage.WoundedUnitsBySlot, 0, conqueredVillage.WoundedUnitsBySlot.Length);
        conqueredVillage.HeldPrisoners.Clear();
        conqueredVillage.ActiveCelebrationType = CelebrationType.None;
        conqueredVillage.CelebrationEndsAtMs = 0;
        conqueredVillage.FestivalEndsAtMs = 0;
        conqueredVillage.TotalTrapsCount = 0;
        conqueredVillage.OccupiedTrapsCount = 0;
        conqueredVillage.TroopStacks.RemoveAll(st =>
            st.Tier is StarvationPriorityTier.OwnStationed or StarvationPriorityTier.TrappedPrisoner or StarvationPriorityTier.OwnMoving);

        // 6. Transfer ownership & recount population, CP, and storage
        conqueredVillage.OwnerPlayerId = newOwnerPlayerId;
        conqueredVillage.TribeId = newOwnerTribeId;
        conqueredVillage.IsCapital = false;
        conqueredVillage.Loyalty = 0.0;

        _expansionManager.RecountVillagePopulationCpAndStorage(conqueredVillage);

        return releasedOasisTileIds;
    }

    private bool IsBuildingCompatibleWithTribe(int buildingTypeId, int tribeId)
    {
        var b = _catalog.GetBuilding(buildingTypeId);
        if (!b.AllowedTribeIds.IsEmpty && !b.AllowedTribeIds.Contains(tribeId))
            return false;
        if (!b.DeniedTribeIds.IsEmpty && b.DeniedTribeIds.Contains(tribeId))
            return false;
        return true;
    }
}
