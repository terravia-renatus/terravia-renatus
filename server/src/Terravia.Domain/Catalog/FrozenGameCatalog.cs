using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Terravia.Domain.Catalog;

/// <summary>
/// Zero-allocation, O(1) array-indexed in-memory implementation of <see cref="IGameCatalog"/>.
/// </summary>
public sealed class FrozenGameCatalog : IGameCatalog
{
    public static FrozenGameCatalog Default { get; } = new(CatalogJsonLoader.LoadFromEmbeddedResource());

    private readonly ImmutableArray<TribeDefinition> _tribes;
    private readonly ImmutableArray<UnitDefinition> _allUnits;
    private readonly UnitDefinition?[] _unitsById; // Indexed 0..99
    private readonly ImmutableArray<UnitDefinition>[] _unitsByTribe; // Indexed 1..9
    private readonly ImmutableArray<HeroUnitTemplate> _allHeroTemplates;
    private readonly FrozenDictionary<int, HeroUnitTemplate> _heroTemplatesByUnitId;
    private readonly ImmutableArray<int> _heroExperienceThresholds;
    private readonly ImmutableArray<BuildingDefinition> _buildings; // Indexed 0..49 (for gid 1..50)

    public string SchemaVersion { get; }
    public string ContentHash { get; }

    public FrozenGameCatalog(LoadedCatalogData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        SchemaVersion = data.SchemaVersion;
        ContentHash = data.ContentHash;
        _tribes = data.Tribes;
        _allUnits = data.Units;
        _allHeroTemplates = data.HeroTemplates;
        _heroExperienceThresholds = data.HeroExperienceThresholds;
        _buildings = data.Buildings;

        _unitsById = new UnitDefinition?[100];
        foreach (var unit in _allUnits)
        {
            _unitsById[unit.UnitTypeId] = unit;
        }

        _unitsByTribe = new ImmutableArray<UnitDefinition>[10];
        for (int tid = CatalogConsts.MinTribeId; tid <= CatalogConsts.MaxTribeId; tid++)
        {
            var builder = ImmutableArray.CreateBuilder<UnitDefinition>(CatalogConsts.UnitsPerTribe);
            for (int slot = 1; slot <= CatalogConsts.UnitsPerTribe; slot++)
            {
                int uid = (tid - 1) * CatalogConsts.UnitsPerTribe + slot;
                builder.Add(_unitsById[uid]!);
            }
            _unitsByTribe[tid] = builder.MoveToImmutable();
        }

        _heroTemplatesByUnitId = _allHeroTemplates.ToFrozenDictionary(h => h.UnitTypeId);
    }

    public ReadOnlySpan<TribeDefinition> GetAllTribes() => ImmutableCollectionsMarshal.AsArray(_tribes);

    public TribeDefinition GetTribe(int tribeId)
    {
        if (tribeId is < CatalogConsts.MinTribeId or > CatalogConsts.MaxTribeId)
            throw new ArgumentOutOfRangeException(nameof(tribeId), $"Invalid TribeId '{tribeId}'. Expected 1..9.");
        return _tribes[tribeId - 1];
    }

    public ReadOnlySpan<UnitDefinition> GetAllUnits() => ImmutableCollectionsMarshal.AsArray(_allUnits);

    public UnitDefinition GetUnit(int unitTypeId)
    {
        if (unitTypeId is (< CatalogConsts.MinUnitId or > CatalogConsts.TrapUnitId) || _unitsById[unitTypeId] is not { } unit)
            throw new ArgumentOutOfRangeException(nameof(unitTypeId), $"Invalid UnitTypeId '{unitTypeId}'. Expected 1..90 or 99.");
        return unit;
    }

    public ReadOnlySpan<UnitDefinition> GetUnitsForTribe(int tribeId)
    {
        if (tribeId is < CatalogConsts.MinTribeId or > CatalogConsts.MaxTribeId)
            throw new ArgumentOutOfRangeException(nameof(tribeId), $"Invalid TribeId '{tribeId}'. Expected 1..9.");
        return ImmutableCollectionsMarshal.AsArray(_unitsByTribe[tribeId]);
    }

    public bool TryGetHeroTemplate(int unitTypeId, out HeroUnitTemplate template)
    {
        if (_heroTemplatesByUnitId.TryGetValue(unitTypeId, out var found))
        {
            template = found;
            return true;
        }

        template = null!;
        return false;
    }

    public ReadOnlySpan<HeroUnitTemplate> GetAllHeroTemplates() => ImmutableCollectionsMarshal.AsArray(_allHeroTemplates);

    public ReadOnlySpan<int> GetHeroExperienceThresholds() => ImmutableCollectionsMarshal.AsArray(_heroExperienceThresholds);

    public ReadOnlySpan<BuildingDefinition> GetAllBuildings() => ImmutableCollectionsMarshal.AsArray(_buildings);

    public BuildingDefinition GetBuilding(int buildingTypeId)
    {
        if (buildingTypeId is < CatalogConsts.MinBuildingTypeId or > CatalogConsts.MaxBuildingTypeId)
            throw new ArgumentOutOfRangeException(nameof(buildingTypeId), $"Invalid BuildingTypeId '{buildingTypeId}'. Expected 1..50.");
        return _buildings[buildingTypeId - 1];
    }

    public BuildingLevelStats GetBuildingLevelStats(int buildingTypeId, int level)
    {
        return GetBuilding(buildingTypeId).GetLevelStats(level);
    }
}
