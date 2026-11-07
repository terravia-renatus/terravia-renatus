namespace Terravia.Domain.Villages;

/// <summary>
/// Aggregate state of a village (slots 1..40 + WW 99, resources, population, culture points,
/// celebrations, expansion slots, traps, and active queues) managed exclusively by <c>VillageActor</c>.
/// </summary>
public sealed class VillageState
{
    public int VillageId { get; }
    public int TileId { get; set; }
    public int OwnerPlayerId { get; set; }
    public int TribeId { get; set; }
    public string Name { get; set; } = "New Village";
    public int FieldTopologyId { get; set; } = VillageConsts.DefaultStartingCapitalTopologyId;
    public bool IsCapital { get; set; }
    public bool IsArchonVillage { get; set; }
    public bool IsWorldWonderVillage { get; set; }
    public bool IsWwBuildingPlanVillage { get; set; }
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
    public bool IsPeaceMode { get; set; }

    // Plus +25% resource bonuses and Hero production state
    public bool PlusWoodBonusActive { get; set; }
    public bool PlusClayBonusActive { get; set; }
    public bool PlusIronBonusActive { get; set; }
    public bool PlusCropBonusActive { get; set; }
    public bool HasStationedLivingHero { get; set; }
    public int HeroResourceBonusPoints { get; set; }
    public HeroResourceFocusMode HeroResourceFocusMode { get; set; } = HeroResourceFocusMode.Balanced;
    public double DietControlArtifactMultiplier { get; set; } = 1.0;

    // Annexed oasis bonus counters (O_r in 0..6 across up to 3 annexed oases)
    public int AnnexedOasisWoodUnits { get; set; }
    public int AnnexedOasisClayUnits { get; set; }
    public int AnnexedOasisIronUnits { get; set; }
    public int AnnexedOasisCropUnits { get; set; }
    public List<int> AnnexedOasisTileIds { get; } = [];

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

    // Resources, capacities & loyalty
    public int Wood { get; set; }
    public int Clay { get; set; }
    public int Iron { get; set; }
    public int Crop { get; set; }
    public double ExactWood { get; set; }
    public double ExactClay { get; set; }
    public double ExactIron { get; set; }
    public double ExactCrop { get; set; }
    public int WarehouseCapacity { get; set; } = VillageConsts.BaseStorageCapacity;
    public int GranaryCapacity { get; set; } = VillageConsts.BaseStorageCapacity;
    public int GrossWoodPerHour { get; set; }
    public int GrossClayPerHour { get; set; }
    public int GrossIronPerHour { get; set; }
    public int GrossCropPerHour { get; set; } = 100;
    public int NetCropProductionPerHour { get; set; } = 100;
    public int Population { get; set; }
    public int CulturePointsPerDay { get; set; }
    public double Loyalty { get; set; } = VillageConsts.MaxLoyalty;
    public long LastResourceUpdateAtMs { get; set; }

    // Celebrations & Mead Festival
    public CelebrationType ActiveCelebrationType { get; set; } = CelebrationType.None;
    public long CelebrationEndsAtMs { get; set; }
    public long FestivalEndsAtMs { get; set; }

    // Expansion slots (up to 3 daughter villages)
    public int? ExpansionSlot1VillageId { get; set; }
    public int? ExpansionSlot2VillageId { get; set; }
    public int? ExpansionSlot3VillageId { get; set; }

    // Gaul Trapper state
    public int TotalTrapsCount { get; set; }
    public int OccupiedTrapsCount { get; set; }

    // Military training, accelerators, Academy/Smithy technology, Hospital & Trapper prisoners (003)
    public double TrainerArtifactMultiplier { get; set; } = 1.0;
    public HeroRecruitmentHelmetKind StationedHeroHelmet { get; set; } = HeroRecruitmentHelmetKind.None;
    public int AllianceRecruitmentBonusLevel { get; set; }
    public long AllianceJoinedAtMs { get; set; }
    public bool BlockTrainingOnNegativeCrop { get; set; }
    public bool[] UnlockedUnitSlots { get; } = new bool[11];
    public int[] OffenseUpgradeLevels { get; } = new int[9];
    public int[] DefenseUpgradeLevels { get; } = new int[9];
    public int[] WoundedUnitsBySlot { get; } = new int[9];
    public List<PrisonerStack> HeldPrisoners { get; } = [];
    public List<TrainingQueueItem> TrainingQueues { get; } = [];
    public List<ResearchQueueItem> ResearchQueue { get; } = [];

    // Queues & troop stacks
    public List<ConstructionQueueItem> ConstructionQueue { get; } = [];
    public DemolitionQueueItem? ActiveDemolition { get; set; }
    public bool HasActiveAcademyOrSmithyResearch { get; set; }
    public List<StarvationCandidateStack> TroopStacks { get; } = [];

    public bool IsArchon
    {
        get => IsArchonVillage;
        set => IsArchonVillage = value;
    }

    public VillageState(int villageId, int ownerPlayerId, int tribeId, bool isCapital = true)
    {
        VillageId = villageId;
        OwnerPlayerId = ownerPlayerId;
        TribeId = tribeId;
        IsCapital = isCapital;
        UnlockedUnitSlots[1] = true;
        UnlockedUnitSlots[10] = true;
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

    public void SyncIntegerResourcesFromExact()
    {
        Wood = (int)Math.Floor(ExactWood);
        Clay = (int)Math.Floor(ExactClay);
        Iron = (int)Math.Floor(ExactIron);
        Crop = (int)Math.Floor(ExactCrop);
    }

    public void SyncExactResourcesFromInteger()
    {
        ExactWood = Wood;
        ExactClay = Clay;
        ExactIron = Iron;
        ExactCrop = Crop;
    }
}
