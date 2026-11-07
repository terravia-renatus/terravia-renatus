using Terravia.Domain.Catalog;

namespace Terravia.Domain.Villages;

/// <summary>
/// Outcome of attempting to start an Academy discovery or Blacksmith/Armoury upgrade.
/// </summary>
public readonly record struct ResearchEnrollmentResult(
    bool Succeeded,
    string? ErrorCode,
    ResearchCategory Category,
    int UnitTypeId,
    int TargetLevel,
    bool IsPlusWaitingLoop,
    ResourceCost DeductedCost,
    long FinishesAtMs);
