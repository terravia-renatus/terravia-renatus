namespace Terravia.Domain.Villages;

/// <summary>
/// Input context for evaluating the 6-step village resource production pipeline and net crop upkeep.
/// </summary>
public readonly record struct VillageProductionContext(
    int[] SlotBuildingTypes,
    int[] SlotLevels,
    int AnnexedOasisWoodUnits,
    int AnnexedOasisClayUnits,
    int AnnexedOasisIronUnits,
    int AnnexedOasisCropUnits,
    bool HasStationedLivingHero,
    int HeroResourceBonusPoints,
    HeroResourceFocusMode HeroResourceFocusMode,
    bool PlusWoodBonusActive,
    bool PlusClayBonusActive,
    bool PlusIronBonusActive,
    bool PlusCropBonusActive,
    bool IsArchonVillage,
    int VillageTribeId,
    int Population,
    IReadOnlyList<StarvationCandidateStack>? FedTroopStacks = null,
    double DietControlArtifactMultiplier = 1.0,
    double ServerSpeed = 1.0);
