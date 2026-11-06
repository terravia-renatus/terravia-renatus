namespace Terravia.Domain.Catalog;

/// <summary>
/// Snapshot of village state used to evaluate slot upgrade eligibility, resources, and queue capacity.
/// </summary>
public readonly record struct VillageConstructionContext(
    VillagePrerequisiteContext PrerequisiteContext,
    int AvailableWood,
    int AvailableClay,
    int AvailableIron,
    int AvailableCrop,
    int WarehouseCapacity,
    int GranaryCapacity,
    int NetCropProductionPerHour,
    bool HasPlusSubscription,
    bool AllowAllTribeRomanQueue,
    int MaxMasterBuilderSlots,
    int ActiveResourceFieldJobs,
    int ActiveInnerBuildingJobs,
    int WaitingLoopJobs,
    int MasterBuilderJobs,
    int QueuedJobsForTargetSlot,
    bool HasCroplandUpgradeQueued,
    int? ActiveDemolitionSlotId = null,
    double ServerSpeed = 1.0);
