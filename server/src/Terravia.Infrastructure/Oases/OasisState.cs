using Terravia.Domain.Oases;

namespace Terravia.Infrastructure.Oases;

/// <summary>
/// Aggregate and persistence model for <c>dbo.OasisState</c> managed by <c>OasisActor</c>.
/// </summary>
public sealed class OasisState
{
    public int TileId { get; set; }
    public short X { get; set; }
    public short Y { get; set; }
    public byte OasisTypeId { get; set; } = 1;
    public byte RichnessTier { get; set; }
    public int? AnnexedByVillageId { get; set; }
    public int OwnerPlayerId { get; set; } = OasisConsts.NaturePlayerId;
    public double Loyalty { get; set; } = OasisConsts.MaxLoyalty;
    public double Wood { get; set; }
    public double Clay { get; set; }
    public double Iron { get; set; }
    public double Crop { get; set; }
    public long LastUpdatedMs { get; set; }
    public long NextNatureRegenAtMs { get; set; }
    public int HeroMansionLevelInOwningVillage { get; set; }
    public double ServerSpeed { get; set; } = 1.0;
    public int NatureRegenIntervalSeconds { get; set; } = 3600;
    public int ActivePlayerCount { get; set; } = 100;
    public double AvgPlayerProgress { get; set; } = 300.0;

    // Defending wild Nature fauna (indices 0..9 -> u31..u40)
    public int[] WildFaunaCounts31To40 { get; } = new int[10];

    // Reinforcements stationed at this oasis when annexed
    public List<long> StationedReinforcementStackIds { get; } = [];
}
