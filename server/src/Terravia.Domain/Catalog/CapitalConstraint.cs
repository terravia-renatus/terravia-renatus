namespace Terravia.Domain.Catalog;

/// <summary>
/// Capital vs. Non-Capital placement restriction for a building type.
/// </summary>
public enum CapitalConstraint
{
    Any = 0,
    CapitalOnly = 1,
    NonCapitalOnly = 2
}
