namespace Terravia.Domain.Catalog;

/// <summary>
/// Snapshot of village state used to evaluate building prerequisites in O(1) without heap allocations.
/// </summary>
public readonly record struct VillagePrerequisiteContext(
    int TribeId,
    bool IsCapital,
    bool IsArchonVillage,
    bool IsWorldWonderVillage,
    bool HasAccountPalace,
    bool HasGreatStorageArtifact,
    int OwnedWwPlanCount,
    int AllianceWwPlanCount,
    bool GreatWorkshopEnabled,
    int[] SlotBuildingTypes,
    int[] SlotLevels,
    int WorldWonderLevel = 0,
    int MaxQueuedWorldWonderLevel = 0);
