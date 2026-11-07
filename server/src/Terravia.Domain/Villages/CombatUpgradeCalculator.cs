using Terravia.Domain.Catalog;

namespace Terravia.Domain.Villages;

/// <summary>
/// Stateless, zero-allocation implementation of the canonical exponential pop-weighted combat upgrade formula
/// and Ram/Catapult structural demolition upgrade factor (FR-010, SC-004).
/// </summary>
public sealed class CombatUpgradeCalculator : ICombatUpgradeCalculator
{
    private const int ReconnaissanceScoutBaseOffense = 35;
    private const int ReconnaissanceScoutBaseDefense = 20;

    private readonly IGameCatalog _catalog;

    public CombatUpgradeCalculator(IGameCatalog? catalog = null)
    {
        _catalog = catalog ?? FrozenGameCatalog.Default;
    }

    public double CalculateUpgradedStat(int baseStat, int cropUpkeep, int upgradeLevel)
    {
        int clampedLevel = Math.Clamp(upgradeLevel, 0, 20);
        if (clampedLevel == 0)
        {
            return baseStat;
        }

        double factor = Math.Pow(1.007, clampedLevel) - 1.0;
        double raw = baseStat + (baseStat + (300.0 * cropUpkeep / 7.0)) * factor;
        return Math.Round(raw, 4, MidpointRounding.AwayFromZero);
    }

    public double CalculateSiegeDemolitionMultiplier(int upgradeLevel)
    {
        int clampedLevel = Math.Clamp(upgradeLevel, 0, 20);
        if (clampedLevel == 0)
        {
            return 1.0;
        }

        double rounded200 = Math.Round(200.0 * Math.Pow(1.0205, clampedLevel), MidpointRounding.AwayFromZero);
        return rounded200 / 200.0;
    }

    public UpgradedUnitCombatStats EvaluateUnitCombatStats(
        int unitTypeId,
        int offenseUpgradeLevel,
        int defenseUpgradeLevel,
        bool isReconnaissanceMission = false)
    {
        var unit = _catalog.GetUnit(unitTypeId);
        int pop = unit.UpkeepCropPerHour;

        // Cultural leaders (slot 9), Settlers (slot 10), and Traps (99) are not upgraded in the Smithy
        bool isCombatUpgradable = unit.UnitTypeId != CatalogConsts.TrapUnitId && unit.TribeUnitSlot is >= 1 and <= 8;
        int effOffLevel = isCombatUpgradable ? Math.Clamp(offenseUpgradeLevel, 0, 20) : 0;
        int effDefLevel = isCombatUpgradable ? Math.Clamp(defenseUpgradeLevel, 0, 20) : 0;

        bool isScoutRecon = isReconnaissanceMission && unit.TacticalRole.HasFlag(UnitTacticalRole.Scout);
        int baseOff = isScoutRecon
            ? ReconnaissanceScoutBaseOffense
            : unit.Offense;
        int baseDefInf = isScoutRecon
            ? ReconnaissanceScoutBaseDefense
            : unit.DefenseInfantry;
        int baseDefCav = isScoutRecon
            ? ReconnaissanceScoutBaseDefense
            : unit.DefenseCavalry;

        double upgradedOff = CalculateUpgradedStat(baseOff, pop, effOffLevel);
        double upgradedDefInf = CalculateUpgradedStat(baseDefInf, pop, effDefLevel);
        double upgradedDefCav = CalculateUpgradedStat(baseDefCav, pop, effDefLevel);

        double siegeMult = unit.TribeUnitSlot is 7 or 8
            ? CalculateSiegeDemolitionMultiplier(effOffLevel)
            : 1.0;

        return new UpgradedUnitCombatStats(
            EffectiveOffense: upgradedOff,
            EffectiveDefenseInfantry: upgradedDefInf,
            EffectiveDefenseCavalry: upgradedDefCav,
            SiegeDemolitionMultiplier: siegeMult);
    }
}
