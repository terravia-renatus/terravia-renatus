using Terravia.Domain.Catalog;

namespace Terravia.Domain.Villages;

/// <summary>
/// Stateless, zero-allocation implementation of the deterministic 4-stage training speed pipeline (FR-006),
/// Hospital healing duration (FR-012), and Academy/Smithy research durations (FR-008..FR-009).
/// </summary>
public sealed class TrainingTimeCalculator : ITrainingTimeCalculator
{
    private readonly IGameCatalog _catalog;

    public TrainingTimeCalculator(IGameCatalog? catalog = null)
    {
        _catalog = catalog ?? FrozenGameCatalog.Default;
    }

    public int CalculateEachTimeSeconds(in TrainingSpeedContext context)
    {
        var unit = _catalog.GetUnit(context.UnitTypeId);
        int clampedLevel = Math.Clamp(context.FacilityLevel, 1, 20);
        double speed = context.ServerSpeed > 0.0 ? context.ServerSpeed : 1.0;

        // Stage 1: Building Level attri(L) (100.00 at Level 1 down to 13.51 at Level 20) & Roman Horse Drinking Trough (gid 41)
        double attriPercent = _catalog.GetBuildingLevelStats(19, clampedLevel).AttributeValue;
        if (context.FacilityCategory is TrainingFacilityCategory.Stable or TrainingFacilityCategory.GreatStable
            && context.HorseDrinkingTroughLevel is >= 1 and <= 20)
        {
            double hdtDivisor = _catalog.GetBuildingLevelStats(41, context.HorseDrinkingTroughLevel).AttributeValue;
            if (hdtDivisor > 0.0)
            {
                attriPercent /= hdtDivisor;
            }
        }

        double rawStage1 = (attriPercent / 100.0) * (unit.BaseTrainingSeconds / speed);
        int t1 = Math.Max(1, (int)Math.Round(rawStage1, MidpointRounding.AwayFromZero));

        // Stage 2: Single-Pass Training Speed Artifact (Kind 5: 1.0, 0.75, or 0.50)
        double artMult = context.TrainerArtifactMultiplier > 0.0 ? context.TrainerArtifactMultiplier : 1.0;
        int t2 = Math.Max(1, (int)Math.Round(t1 * artMult, MidpointRounding.AwayFromZero));

        // Stage 3: Stationed Living Hero Recruitment Helmet (ItemId 10..15)
        int heroBonusPercent = GetApplicableHeroHelmetBonusPercent(
            context.FacilityCategory,
            context.HasStationedLivingHero,
            context.StationedHeroHelmet);

        int t3 = heroBonusPercent > 0
            ? Math.Max(1, (int)Math.Round(t2 * ((100.0 - Math.Min(90, heroBonusPercent)) / 100.0), MidpointRounding.AwayFromZero))
            : t2;

        // Stage 4: T4 Alliance Recruitment Bonus (Levels 1..5, +2%..+10%)
        if (context.FacilityCategory == TrainingFacilityCategory.Healing)
        {
            return t3;
        }

        int effectiveAllianceLevel = CalculateEffectiveAllianceBonusLevel(
            context.AllianceRecruitmentBonusLevel,
            context.AllianceJoinedAtMs,
            context.CurrentTimestampMs,
            speed);

        if (effectiveAllianceLevel <= 0)
        {
            return t3;
        }

        double allianceDivisor = 1.0 + 0.02 * effectiveAllianceLevel;
        return Math.Max(1, (int)Math.Round(t3 / allianceDivisor, MidpointRounding.AwayFromZero));
    }

    public int CalculateHospitalHealingEachTimeSeconds(
        int unitTypeId,
        int hospitalBuildingTypeId,
        int hospitalLevel,
        double serverSpeed = 1.0)
    {
        var unit = _catalog.GetUnit(unitTypeId);
        int gid = hospitalBuildingTypeId is 46 or 48 ? hospitalBuildingTypeId : 46;
        int clampedLevel = Math.Clamp(hospitalLevel, 1, 20);
        double speed = serverSpeed > 0.0 ? serverSpeed : 1.0;

        double attriHospital = _catalog.GetBuildingLevelStats(gid, clampedLevel).AttributeValue;
        double rawSeconds = unit.BaseTrainingSeconds * (attriHospital / speed);
        return Math.Max(1, (int)Math.Round(rawSeconds, MidpointRounding.AwayFromZero));
    }

    public int CalculateAcademyResearchDurationSeconds(
        int unitTypeId,
        int academyLevel,
        double serverSpeed = 1.0)
    {
        var unit = _catalog.GetUnit(unitTypeId);
        int clampedLevel = Math.Clamp(academyLevel, 1, 20);
        double speed = serverSpeed > 0.0 ? serverSpeed : 1.0;

        double attriAcademy = _catalog.GetBuildingLevelStats(22, clampedLevel).AttributeValue;
        double rawSeconds = unit.BaseResearchSeconds * (attriAcademy / 100.0) / speed;
        return Math.Max(1, (int)Math.Round(rawSeconds, MidpointRounding.AwayFromZero));
    }

    public int CalculateSmithyUpgradeDurationSeconds(
        int unitTypeId,
        int targetLevel,
        int smithyLevel,
        double serverSpeed = 1.0)
    {
        var unit = _catalog.GetUnit(unitTypeId);
        if (unit.SmithyUpgrades.IsDefaultOrEmpty || targetLevel is < 1 or > 20 || targetLevel > unit.SmithyUpgrades.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(targetLevel), $"Unit '{unitTypeId}' does not have Smithy upgrade level '{targetLevel}'.");
        }

        int clampedSmithyLevel = Math.Clamp(smithyLevel, 1, 20);
        double speed = serverSpeed > 0.0 ? serverSpeed : 1.0;

        int abBaseSeconds = unit.SmithyUpgrades[targetLevel - 1].BaseTimeSeconds;
        double attriSmithy = _catalog.GetBuildingLevelStats(12, clampedSmithyLevel).AttributeValue;
        double rawSeconds = abBaseSeconds * (attriSmithy / 100.0) / speed;
        return Math.Max(1, (int)Math.Round(rawSeconds, MidpointRounding.AwayFromZero));
    }

    public int CalculateEffectiveAllianceBonusLevel(
        int allianceBonusLevel,
        long allianceJoinedAtMs,
        long currentTimestampMs,
        double serverSpeed = 1.0)
    {
        int clampedLevel = Math.Clamp(allianceBonusLevel, 0, 5);
        if (clampedLevel <= 0)
        {
            return 0;
        }

        // If AllianceJoinedAtMs == 0 and CurrentTimestampMs == 0, treat membership tenure as already established
        if (allianceJoinedAtMs <= 0 && currentTimestampMs <= 0)
        {
            return clampedLevel;
        }

        double speed = serverSpeed > 0.0 ? serverSpeed : 1.0;
        long elapsedSeconds = Math.Max(0L, (currentTimestampMs - allianceJoinedAtMs) / 1000L);

        int maxUnlockedByTenure = 1;
        for (int k = 2; k <= 5; k++)
        {
            long requiredSeconds = (long)Math.Floor((k - 1) * 86400.0 / speed);
            if (elapsedSeconds >= requiredSeconds)
            {
                maxUnlockedByTenure = k;
            }
            else
            {
                break;
            }
        }

        return Math.Min(clampedLevel, maxUnlockedByTenure);
    }

    private static int GetApplicableHeroHelmetBonusPercent(
        TrainingFacilityCategory facility,
        bool hasStationedLivingHero,
        HeroRecruitmentHelmetKind helmet)
    {
        if (!hasStationedLivingHero || helmet == HeroRecruitmentHelmetKind.None)
        {
            return 0;
        }

        if (facility is TrainingFacilityCategory.Barracks or TrainingFacilityCategory.GreatBarracks)
        {
            return helmet switch
            {
                HeroRecruitmentHelmetKind.Mercenary10 => 10,
                HeroRecruitmentHelmetKind.Warrior15 => 15,
                HeroRecruitmentHelmetKind.Ruler20 => 20,
                _ => 0
            };
        }

        if (facility is TrainingFacilityCategory.Stable or TrainingFacilityCategory.GreatStable)
        {
            return helmet switch
            {
                HeroRecruitmentHelmetKind.Horseman10 => 10,
                HeroRecruitmentHelmetKind.Cavalry15 => 15,
                HeroRecruitmentHelmetKind.HeavyCavalry20 => 20,
                _ => 0
            };
        }

        return 0;
    }
}
