namespace Terravia.Domain.Villages;

/// <summary>
/// Trainable expansion unit quota returned by <see cref="VillageExpansionManager.EvaluateExpansionTrainingQuota"/>.
/// </summary>
public readonly record struct ExpansionTrainingQuota(
    int UnlockedExpansionSlots,
    int TotalCapacityInSettlerEquivalents,
    int ConsumedInSettlerEquivalents,
    int RemainingTrainableSettlers,
    int RemainingTrainableAdministrators);
