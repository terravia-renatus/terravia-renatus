
namespace Terravia.Domain.Catalog;

/// <summary>
/// Authoritative read-only catalog of all 9 tribes, 91 units, 35 hero templates, and 50 buildings.
/// </summary>
public interface IGameCatalog
{
    string SchemaVersion { get; }
    string ContentHash { get; }
    ReadOnlySpan<TribeDefinition> GetAllTribes();
    TribeDefinition GetTribe(int tribeId);

    ReadOnlySpan<UnitDefinition> GetAllUnits();
    UnitDefinition GetUnit(int unitTypeId);
    ReadOnlySpan<UnitDefinition> GetUnitsForTribe(int tribeId);
    bool TryGetHeroTemplate(int unitTypeId, out HeroUnitTemplate template);
    ReadOnlySpan<HeroUnitTemplate> GetAllHeroTemplates();
    ReadOnlySpan<int> GetHeroExperienceThresholds();

    ReadOnlySpan<BuildingDefinition> GetAllBuildings();
    BuildingDefinition GetBuilding(int buildingTypeId);
    BuildingLevelStats GetBuildingLevelStats(int buildingTypeId, int level);
}
