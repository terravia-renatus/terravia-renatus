namespace Terravia.Domain.Villages;

/// <summary>
/// Outcome of evaluating a specific village building slot for timed or 10-Gold instant demolition.
/// </summary>
public readonly record struct DemolitionEligibilityResult(
    DemolitionStatus Status,
    int TargetLevel,
    int DurationSeconds)
{
    public bool IsAllowed => Status == DemolitionStatus.Allowed;
}
