using Terravia.Domain;
using Terravia.Domain.Catalog;
using Terravia.Domain.Villages;
using Xunit;

namespace Terravia.Domain.Tests.Villages;

public sealed class CelebrationAndMeadFestivalManagerTests
{
    private readonly CelebrationManager _manager = new();

    [Theory]
    [InlineData(1, 86_400, 0)]
    [InlineData(5, 74_614, 0)]
    [InlineData(9, 64_436, 0)]
    [InlineData(10, 62_117, 155_291)]
    [InlineData(15, 51_712, 129_280)]
    [InlineData(20, 43_050, 107_626)]
    public void TownHallCelebrationDurations_MatchCanonicalLookupTable_At1xSpeed(
        int townHallLevel,
        int expectedSmallSec,
        int expectedGreatSec)
    {
        Assert.Equal(expectedSmallSec, CelebrationManager.GetSmallCelebrationBaseDurationSeconds(townHallLevel));
        Assert.Equal(expectedGreatSec, CelebrationManager.GetGreatCelebrationBaseDurationSeconds(townHallLevel));
    }

    [Fact]
    public void EvaluateStartCelebration_EnforcesTownHallLevel_SingleSlot_Cost_AndAwardsOnCompletion()
    {
        var ampleResources = new ResourceCost(50_000, 50_000, 50_000, 50_000);

        // Great Celebration at Town Hall Level 9 must fail
        var lowLevelRes = _manager.EvaluateStartCelebration(
            celebrationType: CelebrationType.Great,
            townHallLevel: 9,
            hasActiveCelebration: false,
            availableResources: in ampleResources,
            serverSpeed: 1.0);

        Assert.False(lowLevelRes.Succeeded);
        Assert.Equal(TerraviaDomainErrorCodes.Villages.InsufficientTownHallLevel, lowLevelRes.ErrorCode);

        // Great Celebration at Town Hall Level 10 succeeds and awards 2,000 CP on completion
        var greatRes = _manager.EvaluateStartCelebration(
            celebrationType: CelebrationType.Great,
            townHallLevel: 10,
            hasActiveCelebration: false,
            availableResources: in ampleResources,
            serverSpeed: 1.0);

        Assert.True(greatRes.Succeeded);
        Assert.Equal(2_000, greatRes.CompletionCulturePointsAward);
        Assert.Equal(155_291, greatRes.DurationSeconds);

        // Starting another celebration while one is active must fail
        var busyRes = _manager.EvaluateStartCelebration(
            celebrationType: CelebrationType.Small,
            townHallLevel: 10,
            hasActiveCelebration: true,
            availableResources: in ampleResources,
            serverSpeed: 1.0);

        Assert.False(busyRes.Succeeded);
        Assert.Equal(TerraviaDomainErrorCodes.Villages.CelebrationAlreadyActive, busyRes.ErrorCode);
    }

    [Fact]
    public void EvaluateStartMeadFestival_RequiresTeutonCapitalWithBrewery_AndCalculatesModifiers()
    {
        var ampleResources = new ResourceCost(50_000, 50_000, 50_000, 50_000);

        // Roman Capital cannot start Mead Festival
        var romanRes = _manager.EvaluateStartMeadFestival(
            ownerTribeId: 1,
            isCapital: true,
            breweryLevel: 10,
            hasActiveFestival: false,
            availableResources: in ampleResources,
            serverSpeed: 1.0);

        Assert.False(romanRes.Succeeded);
        Assert.Equal(TerraviaDomainErrorCodes.Villages.MeadFestivalRequiresTeutonCapitalBrewery, romanRes.ErrorCode);

        // Teuton Capital with Brewery Level 10 succeeds with 72h duration (259,200s) and +10% attack bonus
        var teutonRes = _manager.EvaluateStartMeadFestival(
            ownerTribeId: 2,
            isCapital: true,
            breweryLevel: 10,
            hasActiveFestival: false,
            availableResources: in ampleResources,
            serverSpeed: 1.0);

        Assert.True(teutonRes.Succeeded);
        Assert.Equal(259_200, teutonRes.DurationSeconds);
        Assert.Equal(10, teutonRes.AttackBonusPercent);
        Assert.Equal(0.5, teutonRes.ChiefPersuasionMultiplier, precision: 4);
        Assert.True(teutonRes.ForceRandomCatapultTargets);
    }
}
