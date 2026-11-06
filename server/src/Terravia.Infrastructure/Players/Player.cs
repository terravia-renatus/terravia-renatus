using Terravia.Domain.Players;

namespace Terravia.Infrastructure.Players;

/// <summary>
/// Singular database persistence model for <c>dbo.Players</c> Culture Point and Capital village state.
/// </summary>
public sealed class Player
{
    public int PlayerId { get; set; }
    public double AccumulatedCulturePoints { get; set; }
    public int TotalVillageBaseCpPerDay { get; set; }
    public int HeroHelmetCpPerDay { get; set; }
    public double AlliancePhilosophyMultiplier { get; set; } = PlayerConsts.MinAlliancePhilosophyMultiplier;
    public long LastCpUpdatedMs { get; set; }
    public int OwnedVillageCount { get; set; } = 1;
    public int CapitalVillageId { get; set; }
}
