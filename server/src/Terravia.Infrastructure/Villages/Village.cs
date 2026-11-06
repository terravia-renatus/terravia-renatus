using Terravia.Domain.Villages;

namespace Terravia.Infrastructure.Villages;

/// <summary>
/// Persistence model for the <c>dbo.Villages</c> table (clean singular noun without Entity suffix).
/// </summary>
public sealed class Village
{
    public int VillageId { get; set; }
    public int TileId { get; set; }
    public int OwnerPlayerId { get; set; }
    public int OwnerTribeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int FieldTopologyId { get; set; } = VillageConsts.DefaultStartingCapitalTopologyId;
    public bool IsCapital { get; set; }
    public bool IsArchon { get; set; }
    public bool IsWwBuildingPlanVillage { get; set; }
    public int Population { get; set; }
    public int CulturePointsPerDay { get; set; }
    public double Loyalty { get; set; } = VillageConsts.MaxLoyalty;
    public double Wood { get; set; }
    public double Clay { get; set; }
    public double Iron { get; set; }
    public double Crop { get; set; }
    public int WarehouseCap { get; set; } = VillageConsts.BaseStorageCapacity;
    public int GranaryCap { get; set; } = VillageConsts.BaseStorageCapacity;
    public long LastUpdatedMs { get; set; }
    public CelebrationType ActiveCelebrationType { get; set; }
    public long CelebrationEndsAtMs { get; set; }
    public long FestivalEndsAtMs { get; set; }
    public int? ExpansionSlot1VillageId { get; set; }
    public int? ExpansionSlot2VillageId { get; set; }
    public int? ExpansionSlot3VillageId { get; set; }
    public int TotalTrapsCount { get; set; }
    public int OccupiedTrapsCount { get; set; }
}
