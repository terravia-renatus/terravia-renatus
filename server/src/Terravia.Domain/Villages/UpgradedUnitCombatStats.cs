namespace Terravia.Domain.Villages;

/// <summary>
/// Upgraded combat stats and Ram/Catapult structural demolition multiplier for a unit at a given Blacksmith/Armoury level (FR-010).
/// </summary>
public readonly record struct UpgradedUnitCombatStats(
    double EffectiveOffense,
    double EffectiveDefenseInfantry,
    double EffectiveDefenseCavalry,
    double SiegeDemolitionMultiplier);
