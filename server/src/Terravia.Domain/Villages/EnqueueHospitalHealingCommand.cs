namespace Terravia.Domain.Villages;

/// <summary>
/// Command to enqueue wounded combat units (slots X1..X8) for healing in a Hospital (gid 46) or Big Hospital (gid 48) (FR-012).
/// </summary>
public sealed record EnqueueHospitalHealingCommand(
    int VillageId,
    int UnitTypeId,
    int RequestedAmount);
