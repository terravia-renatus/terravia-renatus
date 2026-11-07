namespace Terravia.Domain.Villages;

/// <summary>
/// Input context for evaluating post-battle T4 Hero Bandage revival and Hospital/Big Hospital casualty admission (FR-011).
/// </summary>
public readonly record struct BattleCasualtyAdmissionContext(
    ReadOnlyMemory<int> DeadUnitsBySlot,
    bool HasSurvivingHeroWithBandages = false,
    HeroBandageTier BandageTier = HeroBandageTier.None,
    int AvailableBandageItemCount = 0);
