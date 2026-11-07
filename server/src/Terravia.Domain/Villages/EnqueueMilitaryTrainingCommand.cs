namespace Terravia.Domain.Villages;

/// <summary>
/// Command to enqueue a batch of military units, expansion units, or Gaul traps (FR-001..FR-005).
/// </summary>
public sealed record EnqueueMilitaryTrainingCommand(
    int VillageId,
    TrainingFacilityCategory FacilityCategory,
    int UnitTypeId,
    int RequestedAmount,
    int? SecondExpansionUnitTypeId = null,
    int SecondExpansionRequestedAmount = 0);
