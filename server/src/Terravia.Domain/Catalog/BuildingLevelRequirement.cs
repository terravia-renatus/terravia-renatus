namespace Terravia.Domain.Catalog;

/// <summary>
/// Prerequisite building level requirement (BuildingTypeId and RequiredLevel).
/// </summary>
public readonly record struct BuildingLevelRequirement(int BuildingTypeId, int Level);
