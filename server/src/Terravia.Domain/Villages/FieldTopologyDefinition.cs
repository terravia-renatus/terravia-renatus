using System.Collections.Immutable;

namespace Terravia.Domain.Villages;

/// <summary>
/// Immutable definition of one of the 12 canonical village outer resource field layouts (<c>FieldTopologyId 1..12</c>).
/// </summary>
public sealed record FieldTopologyDefinition(
    int FieldTopologyId,
    string CommonName,
    int WoodFields,
    int ClayFields,
    int IronFields,
    int CropFields,
    ImmutableArray<int> SlotResourceTypes);
