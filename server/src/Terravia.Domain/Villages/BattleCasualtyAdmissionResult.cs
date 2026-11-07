namespace Terravia.Domain.Villages;

/// <summary>
/// Result of evaluating T4 Hero Bandage revival and Hospital/Big Hospital casualty interception across combat slots 1..8 (FR-011).
/// </summary>
public readonly record struct BattleCasualtyAdmissionResult(
    int[] BandagedUnitsBySlot,
    int TotalBandagesConsumed,
    int[] AdmittedWoundedBySlot,
    int TotalAdmittedToHospital,
    int RemainingFreeHospitalCapacity);
