namespace Terravia.Domain.Villages;

/// <summary>
/// Input context for evaluating unlocked expansion slots and trainable Settler/Administrator quotas (<c>FR-014</c>).
/// </summary>
public readonly record struct ExpansionQuotaContext(
    int ResidenceLevel,
    int PalaceLevel,
    int CommandCenterLevel,
    int OccupiedExpansionSlotsCount,
    int ExistingOrQueuedSettlersCount,
    int ExistingOrQueuedAdministratorsCount);
