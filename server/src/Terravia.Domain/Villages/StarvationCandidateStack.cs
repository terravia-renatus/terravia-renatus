namespace Terravia.Domain.Villages;

/// <summary>
/// In-memory troop stack or movement candidate evaluated for crop upkeep and 5-tier FIFO starvation.
/// </summary>
public sealed class StarvationCandidateStack
{
    public long StackOrMovementId { get; init; }
    public StarvationPriorityTier Tier { get; init; }
    public int OwnerVillageId { get; init; }
    public int LocationTileId { get; init; }
    public int[] UnitCounts { get; init; } = new int[91];
    public int[] OffenseUpgradeLevels { get; init; } = new int[9];
    public int[] DefenseUpgradeLevels { get; init; } = new int[9];
    public bool HasHero { get; set; }

    public int TotalRegularUnits
    {
        get
        {
            int sum = 0;
            for (int i = 1; i < UnitCounts.Length && i <= 90; i++)
            {
                sum += UnitCounts[i];
            }
            return sum;
        }
    }

    public bool IsEmpty => TotalRegularUnits == 0 && !HasHero;
}
