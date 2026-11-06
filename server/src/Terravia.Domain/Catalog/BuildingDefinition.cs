using System.Collections.Immutable;

namespace Terravia.Domain.Catalog;

/// <summary>
/// Immutable building definition for gid 1..50.
/// </summary>
public sealed record BuildingDefinition
{
    public int BuildingTypeId { get; }
    public string Name { get; }
    public BuildingCategory Category { get; }
    public SlotPlacementRule SlotPlacement { get; }
    public int MaxLevel { get; }
    public int NonCapitalMaxLevel { get; }
    public CapitalConstraint CapitalConstraint { get; }
    public ImmutableArray<int> AllowedTribeIds { get; }
    public ImmutableArray<int> DeniedTribeIds { get; }
    public ImmutableArray<BuildingLevelRequirement> MinLevelRequirements { get; }
    public ImmutableArray<BuildingLevelRequirement> ExactLevelRequirements { get; }
    public ImmutableArray<BuildingLevelRequirement> AnyOfMinLevelRequirements { get; }
    public bool IsUniquePerVillage { get; }
    public int? RebuildAtLevel { get; }
    public bool IsAccountWideUnique { get; }
    public bool RequiresGreatStorageQualification { get; }
    public bool RequiresWorldWonderPlan { get; }
    public bool RequiresRoundConfigGreatWorkshop { get; }
    public bool CanFinishInstantlyWithGold { get; }
    public ImmutableArray<BuildingLevelStats> Levels { get; }

    public BuildingDefinition(
        int buildingTypeId,
        string name,
        BuildingCategory category,
        SlotPlacementRule slotPlacement,
        int maxLevel,
        int nonCapitalMaxLevel,
        CapitalConstraint capitalConstraint,
        ImmutableArray<int> allowedTribeIds,
        ImmutableArray<int> deniedTribeIds,
        ImmutableArray<BuildingLevelRequirement> minLevelRequirements,
        ImmutableArray<BuildingLevelRequirement> exactLevelRequirements,
        ImmutableArray<BuildingLevelRequirement> anyOfMinLevelRequirements,
        bool isUniquePerVillage,
        int? rebuildAtLevel,
        bool isAccountWideUnique,
        bool requiresGreatStorageQualification,
        bool requiresWorldWonderPlan,
        bool requiresRoundConfigGreatWorkshop,
        bool canFinishInstantlyWithGold,
        ImmutableArray<BuildingLevelStats> levels)
    {
        if (buildingTypeId is < 1 or > 50)
            throw new ArgumentOutOfRangeException(nameof(buildingTypeId), "BuildingTypeId must be 1..50.");
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name must be non-empty.", nameof(name));

        BuildingTypeId = buildingTypeId;
        Name = name;
        Category = category;
        SlotPlacement = slotPlacement;
        MaxLevel = maxLevel;
        NonCapitalMaxLevel = nonCapitalMaxLevel;
        CapitalConstraint = capitalConstraint;
        AllowedTribeIds = allowedTribeIds;
        DeniedTribeIds = deniedTribeIds;
        MinLevelRequirements = minLevelRequirements;
        ExactLevelRequirements = exactLevelRequirements;
        AnyOfMinLevelRequirements = anyOfMinLevelRequirements;
        IsUniquePerVillage = isUniquePerVillage;
        RebuildAtLevel = rebuildAtLevel;
        IsAccountWideUnique = isAccountWideUnique;
        RequiresGreatStorageQualification = requiresGreatStorageQualification;
        RequiresWorldWonderPlan = requiresWorldWonderPlan;
        RequiresRoundConfigGreatWorkshop = requiresRoundConfigGreatWorkshop;
        CanFinishInstantlyWithGold = canFinishInstantlyWithGold;
        Levels = levels;
    }

    public BuildingLevelStats GetLevelStats(int level)
    {
        if (BuildingTypeId <= 4)
        {
            if (level is < 0 or > 20)
                throw new ArgumentOutOfRangeException(nameof(level));
            return Levels[level];
        }

        if (level < 1 || level > MaxLevel)
            throw new ArgumentOutOfRangeException(nameof(level));
        return Levels[level - 1];
    }
}
