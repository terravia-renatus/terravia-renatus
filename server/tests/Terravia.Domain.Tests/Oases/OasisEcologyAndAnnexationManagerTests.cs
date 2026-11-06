using Terravia.Domain.Oases;

namespace Terravia.Domain.Tests.Oases;

public sealed class OasisEcologyAndAnnexationManagerTests
{
    private readonly OasisEcologyCatalog _ecology = OasisEcologyCatalog.Default;
    private readonly OasisAnnexationManager _manager = new();

    [Theory]
    [InlineData(1, 1, 0, 0, 0)]
    [InlineData(2, 1, 0, 0, 0)]
    [InlineData(3, 1, 0, 0, 1)]
    [InlineData(4, 0, 1, 0, 0)]
    [InlineData(5, 0, 1, 0, 0)]
    [InlineData(6, 0, 1, 0, 1)]
    [InlineData(7, 0, 0, 1, 0)]
    [InlineData(8, 0, 0, 1, 0)]
    [InlineData(9, 0, 0, 1, 1)]
    [InlineData(10, 0, 0, 0, 1)]
    [InlineData(11, 0, 0, 0, 1)]
    [InlineData(12, 0, 0, 0, 2)]
    public void AllTwelveOasisTypes_MatchCanonicalResourceBonusUnits(
        int oasisTypeId,
        int expectedWood,
        int expectedClay,
        int expectedIron,
        int expectedCrop)
    {
        var def = _ecology.GetOasisType(oasisTypeId);
        Assert.Equal(expectedWood, def.WoodBonusUnits);
        Assert.Equal(expectedClay, def.ClayBonusUnits);
        Assert.Equal(expectedIron, def.IronBonusUnits);
        Assert.Equal(expectedCrop, def.CropBonusUnits);
        Assert.NotEmpty(def.FaunaSpawnRules);
    }

    [Fact]
    public void GrowthFactorAndRichnessTierCaps_BoundBetweenMinAndHardCaps()
    {
        Assert.Equal(0.3, OasisEcologyCatalog.CalculateGrowthFactor(50));
        Assert.Equal(1.5, OasisEcologyCatalog.CalculateGrowthFactor(1500));
        Assert.Equal(3.0, OasisEcologyCatalog.CalculateGrowthFactor(9000));

        var (_, maxTier0) = OasisEcologyCatalog.CalculateRichnessTierUnitCaps(0, 500, 3.0);
        var (_, maxTier1) = OasisEcologyCatalog.CalculateRichnessTierUnitCaps(1, 500, 3.0);
        var (_, maxTier2) = OasisEcologyCatalog.CalculateRichnessTierUnitCaps(2, 500, 3.0);

        Assert.Equal(30, maxTier0);
        Assert.Equal(60, maxTier1);
        Assert.Equal(90, maxTier2);
    }

    [Fact]
    public void ResolveHeroCageCapture_CapturesSequentiallyFromU31ToU40_AndSkipsCombat()
    {
        // Wild fauna: 3 Rats (u31), 4 Spiders (u32), 0 Snakes (u33), 5 Boars (u35)
        int[] wildFauna = [3, 4, 0, 0, 5, 0, 0, 0, 0, 0];

        var result = _manager.ResolveHeroCageCapture(6, wildFauna, heroSentAlone: true);

        Assert.Equal(6, result.CagesConsumed);
        Assert.Equal(3, result.CapturedAnimalsByUnitType31To40[0]); // all 3 Rats captured
        Assert.Equal(3, result.CapturedAnimalsByUnitType31To40[1]); // 3 of 4 Spiders captured
        Assert.Equal(0, result.CapturedAnimalsByUnitType31To40[4]); // 0 Boars captured (cages exhausted)
        Assert.Equal(6, result.RemainingWildDefenders);             // 1 Spider + 5 Boars left
        Assert.True(result.SkipCombat);                             // Hero sent alone with cages -> no combat
    }

    [Theory]
    [InlineData(9, 0)]
    [InlineData(10, 1)]
    [InlineData(14, 1)]
    [InlineData(15, 2)]
    [InlineData(19, 2)]
    [InlineData(20, 3)]
    public void CalculateUnlockedOasisSlots_MatchesHeroMansionThresholds(int mansionLevel, int expectedSlots)
    {
        Assert.Equal(expectedSlots, _manager.CalculateUnlockedOasisSlots(mansionLevel));
    }

    [Theory]
    [InlineData(1, 33, 67.0, false)]
    [InlineData(2, 50, 50.0, false)]
    [InlineData(3, 100, 100.0, true)]
    public void EvaluateHeroOasisStrike_PlayerAnnexedOasis_ReducesLoyaltyBy33_50_Or100Percent(
        int defenderOwnedOases,
        int expectedDrop,
        double expectedResultingLoyalty,
        bool expectedConquered)
    {
        var ctx = new OasisStrikeContext(
            AttackerVillageX: 0,
            AttackerVillageY: 0,
            OasisX: 2,
            OasisY: -3,
            MapRadius: 200,
            AttackerHeroMansionLevel: 10,
            AttackerCurrentlyOwnedOases: 0,
            AttackerHeroSurvived: true,
            IsNormalAttack: true,
            RemainingOasisDefenders: 0,
            CurrentAnnexedByVillageId: 99,
            DefenderCurrentlyOwnedOases: defenderOwnedOases,
            CurrentOasisLoyalty: 100.0);

        var eval = _manager.EvaluateHeroOasisStrike(in ctx);
        Assert.Equal(expectedDrop, eval.LoyaltyReductionPercent);
        Assert.Equal(expectedResultingLoyalty, eval.ResultingLoyalty);
        Assert.Equal(expectedConquered, eval.WasAnnexedOrConquered);
    }

    [Fact]
    public void CalculateAnnexedOasisRaidLootPool_ExposesTenPercentOfOwningVillageStock()
    {
        var pool = _manager.CalculateAnnexedOasisRaidLootPool(1234, 5678, 999, 45);
        Assert.Equal(123, pool.Wood);
        Assert.Equal(567, pool.Clay);
        Assert.Equal(99, pool.Iron);
        Assert.Equal(4, pool.Crop);
    }
}
