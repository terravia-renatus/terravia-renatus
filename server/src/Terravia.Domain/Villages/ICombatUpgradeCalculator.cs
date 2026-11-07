namespace Terravia.Domain.Villages;

/// <summary>
/// Stateless calculator evaluating Blacksmith (b1..b8), Armoury (a1..a8), and Ram/Catapult structural upgrade multipliers (FR-010).
/// </summary>
public interface ICombatUpgradeCalculator
{
    double CalculateUpgradedStat(int baseStat, int cropUpkeep, int upgradeLevel);
    double CalculateSiegeDemolitionMultiplier(int upgradeLevel);
    UpgradedUnitCombatStats EvaluateUnitCombatStats(
        int unitTypeId,
        int offenseUpgradeLevel,
        int defenseUpgradeLevel,
        bool isReconnaissanceMission = false);
}
