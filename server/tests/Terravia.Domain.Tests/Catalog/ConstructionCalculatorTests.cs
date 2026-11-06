using Terravia.Domain.Catalog;

namespace Terravia.Domain.Tests.Catalog;

public sealed class ConstructionCalculatorTests
{
    private readonly IGameCatalog _catalog = FrozenGameCatalog.Default;
    private readonly IConstructionCalculator _calculator;

    public ConstructionCalculatorTests()
    {
        _calculator = new ConstructionCalculator(_catalog);
    }

    [Fact]
    public void CalculateBuildTimeSeconds_ShouldMatchMainBuildingPercentageTable_AndZeroLevelPenalty()
    {
        // Woodcutter (gid 1) Level 1 base time = 260s
        // At Main Building Level 1 (attri = 100%): 260s at 1x speed
        Assert.Equal(260, _calculator.CalculateBuildTimeSeconds(1, targetLevel: 1, mainBuildingLevel: 1, serverSpeed: 1.0));

        // At Main Building Level 0 (destroyed): 5x penalty = 1300s at 1x speed
        Assert.Equal(1300, _calculator.CalculateBuildTimeSeconds(1, targetLevel: 1, mainBuildingLevel: 0, serverSpeed: 1.0));

        // At Main Building Level 20 (attri = 49.83%): round(260 * 0.4983 / 1.0) = round(129.558) = 130s
        Assert.Equal(130, _calculator.CalculateBuildTimeSeconds(1, targetLevel: 1, mainBuildingLevel: 20, serverSpeed: 1.0));

        // At 3x server speed, Main Building Level 1: round(260 / 3.0) = 87s
        Assert.Equal(87, _calculator.CalculateBuildTimeSeconds(1, targetLevel: 1, mainBuildingLevel: 1, serverSpeed: 3.0));
    }

    [Fact]
    public void CalculateDemolitionTimeSeconds_ShouldBeFloorHalfOfBuildTime()
    {
        // Woodcutter Level 1 at MB 10 (attri = 71.89%): buildTime = round(260 * 0.7189) = round(186.914) = 187s
        // Demolition time = floor(187 / 2.0) = 93s
        int buildTime = _calculator.CalculateBuildTimeSeconds(19, targetLevel: 5, mainBuildingLevel: 10, serverSpeed: 1.0);
        int demoTime = _calculator.CalculateDemolitionTimeSeconds(19, currentLevel: 5, mainBuildingLevel: 10, serverSpeed: 1.0);
        Assert.Equal(buildTime / 2, demoTime);
    }

    [Fact]
    public void CalculateVillageCulturePoints_ShouldSumAllSlotLevelsAndWorldWonder()
    {
        var types = new int[41];
        var levels = new int[41];

        // Main Building (15) Level 10 -> CP = 12
        types[19] = 15;
        levels[19] = 10;

        // Academy (22) Level 10 -> CP = 24
        types[20] = 22;
        levels[20] = 10;

        int expectedMbCp = _catalog.GetBuildingLevelStats(15, 10).CulturePoints;
        int expectedAcCp = _catalog.GetBuildingLevelStats(22, 10).CulturePoints;
        int expectedWwCp = _catalog.GetBuildingLevelStats(40, 50).CulturePoints;

        Assert.Equal(expectedMbCp + expectedAcCp, _calculator.CalculateVillageCulturePoints(types, levels));
        Assert.Equal(expectedMbCp + expectedAcCp + expectedWwCp, _calculator.CalculateVillageCulturePoints(types, levels, worldWonderLevel: 50));
    }
}
