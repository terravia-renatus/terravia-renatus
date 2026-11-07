namespace Terravia.Domain.Villages;

/// <summary>
/// Outcome for a single liberated or manually released PrisonerStack (FR-015).
/// </summary>
public readonly record struct PrisonerLiberationOutcome(
    long PrisonerStackId,
    int OwnerVillageId,
    int[] SurvivingUnitsBySlot,
    bool HeroSurvived,
    bool MergedIntoAttackingArmyReturn,
    int FreedTrapCount);
