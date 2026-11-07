namespace Terravia.Domain.Villages;

/// <summary>
/// Input context for the deterministic 4-stage training speed pipeline (FR-006).
/// </summary>
public readonly record struct TrainingSpeedContext(
    int UnitTypeId,
    TrainingFacilityCategory FacilityCategory,
    int FacilityLevel,
    int HorseDrinkingTroughLevel = 0,
    double TrainerArtifactMultiplier = 1.0,
    bool HasStationedLivingHero = false,
    HeroRecruitmentHelmetKind StationedHeroHelmet = HeroRecruitmentHelmetKind.None,
    int AllianceRecruitmentBonusLevel = 0,
    long AllianceJoinedAtMs = 0,
    long CurrentTimestampMs = 0,
    double ServerSpeed = 1.0);
