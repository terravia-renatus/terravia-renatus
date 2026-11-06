using System.Collections.Immutable;

namespace Terravia.Domain.Catalog;

/// <summary>
/// Immutable unit definition for u1..u90 and u99 (Gaul Trap).
/// </summary>
public sealed record UnitDefinition
{
    public int UnitTypeId { get; }
    public int TribeId { get; }
    public int TribeUnitSlot { get; }
    public string Name { get; }
    public UnitCategory Category { get; }
    public UnitTacticalRole TacticalRole { get; }
    public int TrainingBuildingTypeId { get; }
    public ImmutableArray<int> AllowedTrainingBuildingTypeIds { get; }
    public int AlternateGreatTrainingBuildingTypeId { get; }
    public int Offense { get; }
    public int DefenseInfantry { get; }
    public int DefenseCavalry { get; }
    public ResourceCost Cost { get; }
    public int UpkeepCropPerHour { get; }
    public int SpeedTilesPerHour { get; }
    public int BaseTrainingSeconds { get; }
    public int CarryCapacity { get; }
    public int? HorseDrinkingTroughThresholdLevel { get; }
    public ImmutableArray<BuildingLevelRequirement> ResearchPrerequisites { get; }
    public ResourceCost? ResearchCost { get; }
    public int BaseResearchSeconds { get; }
    public ImmutableArray<SmithyUpgradeLevelStats> SmithyUpgrades { get; }

    public UnitDefinition(
        int unitTypeId,
        int tribeId,
        int tribeUnitSlot,
        string name,
        UnitCategory category,
        UnitTacticalRole tacticalRole,
        int trainingBuildingTypeId,
        ImmutableArray<int> allowedTrainingBuildingTypeIds,
        int alternateGreatTrainingBuildingTypeId,
        int offense,
        int defenseInfantry,
        int defenseCavalry,
        ResourceCost cost,
        int upkeepCropPerHour,
        int speedTilesPerHour,
        int baseTrainingSeconds,
        int carryCapacity,
        int? horseDrinkingTroughThresholdLevel,
        ImmutableArray<BuildingLevelRequirement> researchPrerequisites,
        ResourceCost? researchCost,
        int baseResearchSeconds,
        ImmutableArray<SmithyUpgradeLevelStats> smithyUpgrades)
    {
        if (unitTypeId is (< 1 or > 90) and not 99)
            throw new ArgumentOutOfRangeException(nameof(unitTypeId), "UnitTypeId must be 1..90 or 99.");
        if (tribeId is < 1 or > 9)
            throw new ArgumentOutOfRangeException(nameof(tribeId), "TribeId must be 1..9.");
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name must be non-empty.", nameof(name));

        UnitTypeId = unitTypeId;
        TribeId = tribeId;
        TribeUnitSlot = tribeUnitSlot;
        Name = name;
        Category = category;
        TacticalRole = tacticalRole;
        TrainingBuildingTypeId = trainingBuildingTypeId;
        AllowedTrainingBuildingTypeIds = allowedTrainingBuildingTypeIds;
        AlternateGreatTrainingBuildingTypeId = alternateGreatTrainingBuildingTypeId;
        Offense = offense;
        DefenseInfantry = defenseInfantry;
        DefenseCavalry = defenseCavalry;
        Cost = cost;
        UpkeepCropPerHour = upkeepCropPerHour;
        SpeedTilesPerHour = speedTilesPerHour;
        BaseTrainingSeconds = baseTrainingSeconds;
        CarryCapacity = carryCapacity;
        HorseDrinkingTroughThresholdLevel = horseDrinkingTroughThresholdLevel;
        ResearchPrerequisites = researchPrerequisites;
        ResearchCost = researchCost;
        BaseResearchSeconds = baseResearchSeconds;
        SmithyUpgrades = smithyUpgrades;
    }
}
