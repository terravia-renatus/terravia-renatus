namespace Terravia.Domain.Villages;

/// <summary>
/// Single-unit deterministic starvation step outcome returned by <see cref="IStarvationCalculator.TryResolveNextStarvationStep"/>.
/// </summary>
public readonly record struct StarvationStepResult(
    StarvationPriorityTier StarvedTier,
    long TargetStackOrMovementId,
    int StarvedUnitTypeId,
    bool IsHeroStarved,
    int CreditedCrop,
    bool ShouldDeleteEmptyStackOrMovement,
    int FreedTrapCount);
