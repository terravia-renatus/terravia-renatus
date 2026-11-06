using System.Collections.Immutable;

namespace Terravia.Domain.Catalog;

/// <summary>
/// Hero stat-scaling template (h1..h86) for the 5 eligible combat units of each playable tribe.
/// </summary>
public sealed record HeroUnitTemplate
{
    public int UnitTypeId { get; }
    public double BaseAttack { get; }
    public double AttackPerPoint { get; }
    public double BaseDefenseInfantry { get; }
    public double DefenseInfantryPerPoint { get; }
    public double BaseDefenseCavalry { get; }
    public double DefenseCavalryPerPoint { get; }
    public ImmutableArray<HeroReviveLevelStats> ReviveTable { get; }

    public HeroUnitTemplate(
        int unitTypeId,
        double baseAttack,
        double attackPerPoint,
        double baseDefenseInfantry,
        double defenseInfantryPerPoint,
        double baseDefenseCavalry,
        double defenseCavalryPerPoint,
        ImmutableArray<HeroReviveLevelStats> reviveTable)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseAttack);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attackPerPoint);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseDefenseInfantry);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(defenseInfantryPerPoint);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseDefenseCavalry);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(defenseCavalryPerPoint);

        UnitTypeId = unitTypeId;
        BaseAttack = baseAttack;
        AttackPerPoint = attackPerPoint;
        BaseDefenseInfantry = baseDefenseInfantry;
        DefenseInfantryPerPoint = defenseInfantryPerPoint;
        BaseDefenseCavalry = baseDefenseCavalry;
        DefenseCavalryPerPoint = defenseCavalryPerPoint;
        ReviveTable = reviveTable;
    }
}
