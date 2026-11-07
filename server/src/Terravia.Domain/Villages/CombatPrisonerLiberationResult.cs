namespace Terravia.Domain.Villages;

/// <summary>
/// Aggregate outcome of combat liberation when a Normal Attack with >= 1 surviving attacker frees prisoner stacks (FR-015 Path A).
/// </summary>
public readonly record struct CombatPrisonerLiberationResult(
    IReadOnlyList<PrisonerLiberationOutcome> LiberatedStacks,
    int[] MergedIntoAttackerReturnBySlot,
    bool MergedHeroIntoAttackerReturn,
    int TotalDestroyedTraps,
    int FreeAutoRepairedTrapsQueued);
