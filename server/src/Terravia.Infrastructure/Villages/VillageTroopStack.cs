using Terravia.Domain.Villages;

namespace Terravia.Infrastructure.Villages;

/// <summary>
/// Persistence model for <c>dbo.VillageTroopStacks</c> storing home, reinforcement, trapped prisoner, and hospital stacks.
/// </summary>
public sealed class VillageTroopStack
{
    public long StackId { get; set; }
    public int LocationTileId { get; set; }
    public int FeedingVillageId { get; set; }
    public int OwnerVillageId { get; set; }
    public TroopStackType StackType { get; set; }
    public int[] UnitCounts { get; set; } = new int[91];
    public bool HasHero { get; set; }
}
