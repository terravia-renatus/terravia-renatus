using System.Collections.Immutable;

namespace Terravia.Domain.Oases;

/// <summary>
/// Immutable definition of one of the 12 oasis types (<c>OasisTypeId 1..12</c>),
/// its resource bonus units (<c>+1</c> per <c>+25%</c>, <c>+2</c> for <c>+50%</c> Crop), and Nature fauna spawn rules.
/// </summary>
public sealed record OasisTypeDefinition(
    int OasisTypeId,
    string Name,
    int WoodBonusUnits,
    int ClayBonusUnits,
    int IronBonusUnits,
    int CropBonusUnits,
    ImmutableArray<OasisFaunaSpawnRule> FaunaSpawnRules);
