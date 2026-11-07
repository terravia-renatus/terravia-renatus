using Terravia.Domain.Catalog;
using Terravia.Domain.Villages;
using Xunit;

namespace Terravia.Domain.Tests.Villages;

public sealed class TrainingTimeCalculatorTests
{
    private readonly IGameCatalog _catalog = FrozenGameCatalog.Default;
    private readonly TrainingTimeCalculator _calculator = new();

    [Fact]
    public void CalculateEachTimeSeconds_Stage1_RomanHorseDrinkingTrough_ReducesStableAndGreatStableCavalryTime()
    {
        // Equites Imperatoris (u5, base time 2640s), Stable L20 (attri = 13.51), HDT L20 (attri = 1.20)
        // t1 = round(((13.51 / 1.20) / 100) * 2640 / 1) = round(0.11258333 * 2640) = round(297.22) = 297s
        var ctx = new TrainingSpeedContext(
            UnitTypeId: 5,
            FacilityCategory: TrainingFacilityCategory.Stable,
            FacilityLevel: 20,
            HorseDrinkingTroughLevel: 20,
            ServerSpeed: 1.0);

        int seconds = _calculator.CalculateEachTimeSeconds(in ctx);
        Assert.Equal(297, seconds);

        // Verify HDT does NOT affect Barracks (infantry)
        var barracksCtx = new TrainingSpeedContext(
            UnitTypeId: 1,
            FacilityCategory: TrainingFacilityCategory.Barracks,
            FacilityLevel: 20,
            HorseDrinkingTroughLevel: 20,
            ServerSpeed: 1.0);
        var barracksNoHdtCtx = barracksCtx with { HorseDrinkingTroughLevel = 0 };
        Assert.Equal(_calculator.CalculateEachTimeSeconds(in barracksNoHdtCtx), _calculator.CalculateEachTimeSeconds(in barracksCtx));
    }

    [Fact]
    public void CalculateEachTimeSeconds_Full4StagePipeline_AppliesArtifactOnce_StationedHeroHelmet_AndAllianceBonus()
    {
        // Legionnaire (u1, base time 1600s), Barracks L1 (attri = 100) at 16x speed -> t1 = 100s
        // Stage 2: Small Trainer Artifact (0.5x) -> t2 = round(100 * 0.5) = 50s (applied ONCE, not twice!)
        // Stage 3: Stationed living Hero with Ruler Helmet (-20%) -> t3 = round(50 * 0.80) = 40s
        // Stage 4: Alliance Recruitment Bonus Level 5 (+10%, divisor 1.10) -> t4 = round(40 / 1.10) = 36s
        var ctx = new TrainingSpeedContext(
            UnitTypeId: 1,
            FacilityCategory: TrainingFacilityCategory.Barracks,
            FacilityLevel: 1,
            TrainerArtifactMultiplier: 0.50,
            HasStationedLivingHero: true,
            StationedHeroHelmet: HeroRecruitmentHelmetKind.Ruler20,
            AllianceRecruitmentBonusLevel: 5,
            AllianceJoinedAtMs: 0,
            CurrentTimestampMs: 0,
            ServerSpeed: 16.0);

        int result = _calculator.CalculateEachTimeSeconds(in ctx);
        Assert.Equal(36, result);

        // If Hero is NOT stationed (or dead), Stage 3 is skipped: t3 = 50 -> t4 = round(50 / 1.10) = 45s
        var unstationedCtx = ctx with { HasStationedLivingHero = false };
        Assert.Equal(45, _calculator.CalculateEachTimeSeconds(in unstationedCtx));

        // Workshop, ResidencePalace, and Trapper ignore Hero Helmets
        var workshopCtx = new TrainingSpeedContext(
            UnitTypeId: 7,
            FacilityCategory: TrainingFacilityCategory.Workshop,
            FacilityLevel: 1,
            HasStationedLivingHero: true,
            StationedHeroHelmet: HeroRecruitmentHelmetKind.Ruler20,
            ServerSpeed: 1.0);
        var workshopNoHelmetCtx = workshopCtx with { StationedHeroHelmet = HeroRecruitmentHelmetKind.None };
        Assert.Equal(_calculator.CalculateEachTimeSeconds(in workshopNoHelmetCtx), _calculator.CalculateEachTimeSeconds(in workshopCtx));
    }

    [Fact]
    public void CalculateEffectiveAllianceBonusLevel_EnforcesProgressive24HourUnlockThresholds()
    {
        long joinedAtMs = 1_000_000L;

        // 0 hours elapsed -> Level 1 unlocked
        Assert.Equal(1, _calculator.CalculateEffectiveAllianceBonusLevel(5, joinedAtMs, joinedAtMs, 1.0));

        // 30 hours elapsed at 1x speed -> Level 2 unlocked (24h elapsed, 48h not yet reached)
        long thirtyHoursMs = joinedAtMs + 30L * 3600L * 1000L;
        Assert.Equal(2, _calculator.CalculateEffectiveAllianceBonusLevel(5, joinedAtMs, thirtyHoursMs, 1.0));

        // 96 hours elapsed at 1x speed -> all 5 levels unlocked
        long ninetySixHoursMs = joinedAtMs + 96L * 3600L * 1000L;
        Assert.Equal(5, _calculator.CalculateEffectiveAllianceBonusLevel(5, joinedAtMs, ninetySixHoursMs, 1.0));
    }

    [Fact]
    public void CalculateEffectiveUnitPopUpkeep_RomanHorseDrinkingTrough_ReducesUpkeepAtLevels10_15_20()
    {
        var manager = new MilitaryTrainingManager(_catalog, _calculator);
        var u4 = _catalog.GetUnit(4); // Equites Legati (base pop 2)
        var u5 = _catalog.GetUnit(5); // Equites Imperatoris (base pop 3)
        var u6 = _catalog.GetUnit(6); // Equites Caesaris (base pop 4)

        Assert.Equal(2, manager.CalculateEffectiveUnitPopUpkeep(1, u4, 9));
        Assert.Equal(1, manager.CalculateEffectiveUnitPopUpkeep(1, u4, 10));

        Assert.Equal(3, manager.CalculateEffectiveUnitPopUpkeep(1, u5, 14));
        Assert.Equal(2, manager.CalculateEffectiveUnitPopUpkeep(1, u5, 15));

        Assert.Equal(4, manager.CalculateEffectiveUnitPopUpkeep(1, u6, 19));
        Assert.Equal(3, manager.CalculateEffectiveUnitPopUpkeep(1, u6, 20));
    }

    [Fact]
    public void CalculateEachTimeSeconds_HighSpeedServer_NeverFallsBelowOneSecondFloor()
    {
        var ctx = new TrainingSpeedContext(
            UnitTypeId: 11, // Clubswinger
            FacilityCategory: TrainingFacilityCategory.Barracks,
            FacilityLevel: 20,
            TrainerArtifactMultiplier: 0.50,
            HasStationedLivingHero: true,
            StationedHeroHelmet: HeroRecruitmentHelmetKind.Ruler20,
            AllianceRecruitmentBonusLevel: 5,
            ServerSpeed: 10_000.0);

        Assert.Equal(1, _calculator.CalculateEachTimeSeconds(in ctx));
    }
}
