namespace Terravia.Domain.Villages;

/// <summary>
/// Stateless, zero-allocation calculator for military training, trap crafting, Hospital healing, and Academy/Smithy research durations.
/// </summary>
public interface ITrainingTimeCalculator
{
    int CalculateEachTimeSeconds(in TrainingSpeedContext context);
    int CalculateHospitalHealingEachTimeSeconds(int unitTypeId, int hospitalBuildingTypeId, int hospitalLevel, double serverSpeed = 1.0);
    int CalculateAcademyResearchDurationSeconds(int unitTypeId, int academyLevel, double serverSpeed = 1.0);
    int CalculateSmithyUpgradeDurationSeconds(int unitTypeId, int targetLevel, int smithyLevel, double serverSpeed = 1.0);
    int CalculateEffectiveAllianceBonusLevel(int allianceBonusLevel, long allianceJoinedAtMs, long currentTimestampMs, double serverSpeed = 1.0);
}
