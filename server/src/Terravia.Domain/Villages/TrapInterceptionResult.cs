namespace Terravia.Domain.Villages;

/// <summary>
/// Outcome of pre-combat Gaul Trapper (or Archon Capital) interception of an incoming army (FR-014).
/// </summary>
public readonly record struct TrapInterceptionResult(
    int[] RemainingAttackingUnitsBySlot,
    bool AttackingHeroRemains,
    int[] CapturedUnitsBySlot,
    bool HeroWasCaptured,
    int TotalCapturedUnits,
    PrisonerStack? CreatedOrUpdatedPrisonerStack);
