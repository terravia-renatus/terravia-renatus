namespace Terravia.Domain.Catalog;

/// <summary>
/// Outcome of evaluating a specific village slot for construction or upgrade.
/// </summary>
public readonly record struct BuildSlotEvaluation(
    BuildEligibilityStatus Status,
    int TargetLevel,
    ResourceCost RequiredCost,
    int BuildTimeSeconds)
{
    public bool IsAllowed => Status is BuildEligibilityStatus.CanBuildActive
        or BuildEligibilityStatus.CanBuildWaitingLoop
        or BuildEligibilityStatus.CanReserveMasterBuilder;
}
