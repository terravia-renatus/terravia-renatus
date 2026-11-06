namespace Terravia.Infrastructure.Oases;

/// <summary>
/// Persistence model for the <c>dbo.MapTiles</c> table storing toroidal coordinates,
/// habitable <c>FieldTopologyId</c> (1..12), <c>OasisTypeId</c> (1..12), and precomputed <c>BestOasisCropBonus</c> (0..150).
/// </summary>
public sealed class MapTile
{
    public int TileId { get; set; }
    public short X { get; set; }
    public short Y { get; set; }
    public byte FieldTopologyId { get; set; }
    public byte OasisTypeId { get; set; }
    public byte BestOasisCropBonus { get; set; }
    public int? OccupiedByVillageId { get; set; }
}
