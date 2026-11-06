using Terravia.Domain.Villages;

namespace Terravia.Domain.Catalog;

/// <summary>
/// Stateless, pure domain rule engine for building prerequisites, slot upgrades, unit research, and demolition.
/// </summary>
public interface IBuildingRuleCatalog
{
    /// <summary>
    /// Evaluates whether a building type (gid 1..50) satisfies all tribe, capital,
    /// prerequisite building levels, uniqueness/rebuildAtLevel, artifact, and WW plan rules.
    /// </summary>
    bool MeetsPrerequisites(int buildingTypeId, in VillagePrerequisiteContext context);

    /// <summary>
    /// Evaluates whether a specific slot can be constructed or upgraded right now,
    /// returning the canonical BuildSlotEvaluation status indicator.
    /// </summary>
    BuildSlotEvaluation EvaluateSlotUpgrade(
        int slotId,
        int buildingTypeId,
        in VillageConstructionContext context);

    /// <summary>
    /// Evaluates whether a unit type (u1..u90) can be researched in the Academy (gid 22)
    /// given the village's current building levels.
    /// </summary>
    bool MeetsUnitResearchPrerequisites(
        int unitTypeId,
        ReadOnlySpan<int> slotBuildingTypes,
        ReadOnlySpan<int> slotLevels);

    /// <summary>
    /// Evaluates whether an inner building slot (19..40, 99) can be demolished (timed or 10-Gold instant).
    /// Enforces Main Building >= 10, no active/loop build on slot, and Alliance Founder Embassy (gid 18) safeguard.
    /// </summary>
    DemolitionEligibilityResult EvaluateDemolition(
        int slotId,
        bool isInstantGold,
        in VillageDemolitionContext context,
        double serverSpeed = 1.0);
}
