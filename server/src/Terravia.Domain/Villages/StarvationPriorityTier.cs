namespace Terravia.Domain.Villages;

/// <summary>
/// Deterministic 5-tier starvation priority order (FR-012).
/// </summary>
public enum StarvationPriorityTier
{
    OasisReinforcement = 1,
    VillageReinforcement = 2,
    TrappedPrisoner = 3,
    OwnStationed = 4,
    OwnMoving = 5
}
