namespace Terravia.Infrastructure.Villages;

/// <summary>
/// Persistence model for a single village building slot (VillageBuildings table).
/// </summary>
public sealed class VillageBuilding
{
    public int VillageId { get; set; }
    public int SlotId { get; set; }
    public int BuildingTypeId { get; set; }
    public int Level { get; set; }
}
