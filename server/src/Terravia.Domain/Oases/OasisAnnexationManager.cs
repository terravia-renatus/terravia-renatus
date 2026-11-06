using Terravia.Domain.Catalog;
using Terravia.Domain.Villages;

namespace Terravia.Domain.Oases;

/// <summary>
/// Pure domain manager for toroidal 7x7 oasis distance validation, Hero's Mansion (<c>gid 37</c>) slot unlocking,
/// pre-combat Hero Cage (<c>ItemTypeId = 114</c>) sequential capture, oasis annexation/loyalty reduction, and raid loot caps.
/// </summary>
public sealed class OasisAnnexationManager
{
    public bool IsWithinToroidalAnnexationRadius(
        short villageX,
        short villageY,
        short oasisX,
        short oasisY,
        int mapRadius)
    {
        int mapDiameter = 2 * mapRadius + 1;
        return VillageFieldTopologyCatalog.IsWithinSevenBySevenToroidal(villageX, villageY, oasisX, oasisY, mapDiameter);
    }

    public int CalculateUnlockedOasisSlots(int heroMansionLevel)
    {
        if (heroMansionLevel < 10)
            return 0;

        return Math.Clamp((heroMansionLevel - 5) / 5, 0, OasisConsts.MaxOasisSlotsPerVillage);
    }

    /// <summary>
    /// Pre-combat Hero Cage (<c>ItemTypeId = 114</c>) capture against an unoccupied wild oasis (<c>AnnexedByVillageId == null</c>).
    /// Captures 1 animal per Cage sequentially from <c>u31..u40</c> before combat resolution.
    /// </summary>
    public HeroCageCaptureResult ResolveHeroCageCapture(
        int availableCages,
        Span<int> wildOasisFaunaCounts31To40,
        bool heroSentAlone = true)
    {
        var captured = new int[10];
        int cagesUsed = 0;
        int maxCages = Math.Max(0, availableCages);

        for (int i = 0; i < wildOasisFaunaCounts31To40.Length && i < 10 && cagesUsed < maxCages; i++)
        {
            int available = wildOasisFaunaCounts31To40[i];
            if (available <= 0)
                continue;

            int toCapture = Math.Min(available, maxCages - cagesUsed);
            captured[i] = toCapture;
            wildOasisFaunaCounts31To40[i] -= toCapture;
            cagesUsed += toCapture;
        }

        int remaining = 0;
        for (int i = 0; i < wildOasisFaunaCounts31To40.Length && i < 10; i++)
        {
            remaining += Math.Max(0, wildOasisFaunaCounts31To40[i]);
        }

        bool skipCombat = remaining == 0 || (cagesUsed > 0 && heroSentAlone);

        return new HeroCageCaptureResult(
            CagesConsumed: cagesUsed,
            CapturedAnimalsByUnitType31To40: captured,
            RemainingWildDefenders: remaining,
            SkipCombat: skipCombat);
    }

    /// <summary>
    /// Evaluates oasis annexation (<c>100%</c> immediate on cleared wild oasis) or loyalty reduction
    /// against a player-annexed oasis (<c>floor(100 / min(3, 4 - defenderOwnedOases))</c> -&gt; <c>33%</c>, <c>50%</c>, or <c>100%</c>).
    /// </summary>
    public OasisConquestEvaluation EvaluateHeroOasisStrike(in OasisStrikeContext context)
    {
        if (!context.AttackerHeroSurvived || context.RemainingOasisDefenders > 0)
        {
            return new OasisConquestEvaluation(false, 0, context.CurrentOasisLoyalty);
        }

        if (!IsWithinToroidalAnnexationRadius(
                context.AttackerVillageX,
                context.AttackerVillageY,
                context.OasisX,
                context.OasisY,
                context.MapRadius))
        {
            return new OasisConquestEvaluation(false, 0, context.CurrentOasisLoyalty);
        }

        int unlockedSlots = CalculateUnlockedOasisSlots(context.AttackerHeroMansionLevel);
        if (unlockedSlots <= context.AttackerCurrentlyOwnedOases)
        {
            return new OasisConquestEvaluation(false, 0, context.CurrentOasisLoyalty);
        }

        // Case 1: Unoccupied wild oasis -> immediate annexation on NormalAttack or Raid
        if (context.CurrentAnnexedByVillageId is null)
        {
            return new OasisConquestEvaluation(true, 100, OasisConsts.MaxLoyalty);
        }

        // Case 2: Player-annexed oasis -> requires NormalAttack
        if (!context.IsNormalAttack)
        {
            return new OasisConquestEvaluation(false, 0, context.CurrentOasisLoyalty);
        }

        int defOases = Math.Clamp(context.DefenderCurrentlyOwnedOases, 1, 3);
        int hitsNeeded = Math.Min(3, 4 - defOases);
        int loyaltyDrop = 100 / hitsNeeded; // 33, 50, or 100

        double newLoyalty = context.CurrentOasisLoyalty - loyaltyDrop;
        if (newLoyalty <= 0.0 || context.CurrentOasisLoyalty <= loyaltyDrop)
        {
            return new OasisConquestEvaluation(true, loyaltyDrop, OasisConsts.MaxLoyalty);
        }

        return new OasisConquestEvaluation(false, loyaltyDrop, newLoyalty);
    }

    /// <summary>
    /// Computes the maximum raidable resources from a player-annexed oasis:
    /// up to 10% of each resource currently stored in the owning village (<c>floor(villageStock / 10)</c>).
    /// </summary>
    public ResourceCost CalculateAnnexedOasisRaidLootPool(
        int owningVillageWood,
        int owningVillageClay,
        int owningVillageIron,
        int owningVillageCrop) =>
        new(
            Math.Max(0, owningVillageWood / 10),
            Math.Max(0, owningVillageClay / 10),
            Math.Max(0, owningVillageIron / 10),
            Math.Max(0, owningVillageCrop / 10));
}
