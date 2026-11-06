namespace Terravia.Domain.Catalog;

/// <summary>
/// Tactical role flags matching unitClassifications in catalog-data.json.
/// </summary>
[Flags]
public enum UnitTacticalRole
{
    None = 0,
    Infantry = 1 << 0,
    Cavalry = 1 << 1,
    Scout = 1 << 2,
    Siege = 1 << 3,
    Ram = 1 << 4,
    Catapult = 1 << 5,
    Expansion = 1 << 6,
    Chief = 1 << 7,
    Settler = 1 << 8,
    Trap = 1 << 9
}
