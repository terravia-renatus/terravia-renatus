namespace Terravia.Domain.Catalog;

/// <summary>
/// Shared domain constants for catalog bounds across tribes, units, hero templates, and buildings.
/// </summary>
public static class CatalogConsts
{
    public const int MinTribeId = 1;
    public const int MaxTribeId = 9;
    public const int TribeCount = 9;
    public const int UnitsPerTribe = 10;

    public const int MinUnitId = 1;
    public const int MaxStandardUnitId = 90;
    public const int TrapUnitId = 99;
    public const int TotalUnitDefinitions = 91;
    public const int HeroTemplateCount = 35;

    public const int MinBuildingTypeId = 1;
    public const int MaxBuildingTypeId = 50;
    public const int BuildingCount = 50;
}
