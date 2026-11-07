using Terravia.Domain.Catalog;
using Terravia.Domain.Villages;
using Xunit;

namespace Terravia.Domain.Tests.Villages;

public sealed class CombatUpgradeCalculatorTests
{
    private readonly CombatUpgradeCalculator _calculator = new();

    [Fact]
    public void CalculateUpgradedStat_MatchesCanonicalExponentialPopWeightedFormulaToFourDecimals()
    {
        // Imperian (u3): base offense = 70, pop = 1
        // Level 20: round(70 + (70 + 300 * 1 / 7) * (1.007^20 - 1), 4)
        double expectedL20 = Math.Round(70.0 + (70.0 + 300.0 / 7.0) * (Math.Pow(1.007, 20) - 1.0), 4, MidpointRounding.AwayFromZero);
        double actualL20 = _calculator.CalculateUpgradedStat(70, 1, 20);
        Assert.Equal(expectedL20, actualL20, 4);

        // Equites Caesaris (u6): base offense = 180, pop = 4
        double expectedCaesarisL20 = Math.Round(180.0 + (180.0 + 1200.0 / 7.0) * (Math.Pow(1.007, 20) - 1.0), 4, MidpointRounding.AwayFromZero);
        Assert.Equal(expectedCaesarisL20, _calculator.CalculateUpgradedStat(180, 4, 20), 4);
    }

    [Fact]
    public void CalculateSiegeDemolitionMultiplier_MatchesCanonical10205Formula()
    {
        Assert.Equal(1.0, _calculator.CalculateSiegeDemolitionMultiplier(0));

        // Level 20: round(200 * 1.0205^20) / 200 = round(300.14...) / 200 = 300 / 200 = 1.5
        Assert.Equal(1.5, _calculator.CalculateSiegeDemolitionMultiplier(20), 4);
    }

    [Fact]
    public void EvaluateUnitCombatStats_UsesReconScoutStatsWhenReconMission_AndExcludesExpansionUnitsFromUpgrades()
    {
        // Equites Legati (u4, Scout): normal offense = 0, recon offense = 35, recon def = 20
        var reconStats = _calculator.EvaluateUnitCombatStats(4, offenseUpgradeLevel: 10, defenseUpgradeLevel: 10, isReconnaissanceMission: true);
        Assert.True(reconStats.EffectiveOffense > 35.0);
        Assert.True(reconStats.EffectiveDefenseInfantry > 20.0);

        // Senator (u9, slot 9): not upgradable in Smithy
        var senatorStats = _calculator.EvaluateUnitCombatStats(9, offenseUpgradeLevel: 20, defenseUpgradeLevel: 20);
        Assert.Equal(50.0, senatorStats.EffectiveOffense);
        Assert.Equal(1.0, senatorStats.SiegeDemolitionMultiplier);
    }
}
