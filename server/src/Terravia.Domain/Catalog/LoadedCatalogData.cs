using System.Collections.Immutable;

namespace Terravia.Domain.Catalog;

/// <summary>
/// Immutable hydrated catalog payload returned by <see cref="CatalogJsonLoader"/>.
/// </summary>
public sealed record LoadedCatalogData(
    string SchemaVersion,
    string ContentHash,
    ImmutableArray<TribeDefinition> Tribes,
    ImmutableArray<UnitDefinition> Units,
    ImmutableArray<HeroUnitTemplate> HeroTemplates,
    ImmutableArray<int> HeroExperienceThresholds,
    ImmutableArray<BuildingDefinition> Buildings);
