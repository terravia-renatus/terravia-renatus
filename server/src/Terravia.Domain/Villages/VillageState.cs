namespace Terravia.Domain.Villages;

/// <summary>
/// Aggregate state of a village (slots 1..40 + WW 99, resources, population, culture points,
/// and active queues) managed exclusively by <c>VillageActor</c>.
/// </summary>
public sealed class VillageState
{
    public int VillageId { get; }
    public int OwnerPlayerId { get; set; }
    public int TribeId { get; set; }
    public bool IsCapital { get; set; }
    public bool IsArchonVillage { get; set; }
    public bool IsWorldWonderVillage { get; set; }
    public bool HasAccountPalace { get; set; }
    public bool HasGreatStorageArtifact { get; set; }
    public int OwnedWwPlanCount { get; set; }
    public int AllianceWwPlanCount { get; set; }
    public bool GreatWorkshopEnabled { get; set; } = true;
    public bool HasPlusSubscription { get; set; }
    public bool AllowAllTribeRomanQueue { get; set; }
    public int MaxMasterBuilderSlots { get; set; } = 3;
    public double ServerSpeed { get; set; } = 1.0;
    public double StorageMultiplier { get; set; } = 1.0;

    // Alliance & Gold context for demolition and premium commands
    public bool IsAllianceOwner { get; set; }
    public int AllianceMemberCount { get; set; }
    public int QualifyingEmbassyCountAcrossPlayerVillages { get; set; } = 1;
    public int PlayerGoldBalance { get; set; }

    // Contiguous slot arrays (indices 1..40) + World Wonder (Slot 99)
    public int[] SlotBuildingTypes { get; } = new int[41];
    public int[] SlotLevels { get; } = new int[41];
    public int WorldWonderBuildingType { get; set; }
    public int WorldWonderLevel { get; set; }

    // Resources & capacities
    public int Wood { get; set; }
    public int Clay { get; set; }
    public int Iron { get; set; }
    public int Crop { get; set; }
    public int WarehouseCapacity { get; set; } = VillageConsts.BaseStorageCapacity;
    public int GranaryCapacity { get; set; } = VillageConsts.BaseStorageCapacity;
    public int NetCropProductionPerHour { get; set; } = 100;
    public int Population { get; set; }
    public int CulturePointsPerDay { get; set; }
    public long LastResourceUpdateAtMs { get; set; }

    // Queues
    public List<ConstructionQueueItem> ConstructionQueue { get; } = [];
    public DemolitionQueueItem? ActiveDemolition { get; set; }
    public bool HasActiveAcademyOrSmithyResearch { get; set; }

    public VillageState(int villageId, int ownerPlayerId, int tribeId, bool isCapital = true)
    {
        VillageId = villageId;
        OwnerPlayerId = ownerPlayerId;
        TribeId = tribeId;
        IsCapital = isCapital;
    }

    public int GetSlotBuildingType(int slotId) =>
        slotId == VillageConsts.WorldWonderSlotId ? WorldWonderBuildingType : SlotBuildingTypes[slotId];

    public int GetSlotLevel(int slotId) =>
        slotId == VillageConsts.WorldWonderSlotId ? WorldWonderLevel : SlotLevels[slotId];

    public void SetSlot(int slotId, int buildingTypeId, int level)
    {
        if (slotId == VillageConsts.WorldWonderSlotId)
        {
            WorldWonderBuildingType = buildingTypeId;
            WorldWonderLevel = level;
        }
        else
        {
            SlotBuildingTypes[slotId] = buildingTypeId;
            SlotLevels[slotId] = level;
        }
    }
}
