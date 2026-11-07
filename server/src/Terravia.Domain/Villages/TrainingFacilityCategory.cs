namespace Terravia.Domain.Villages;

/// <summary>
/// Identifies the 9 independent, concurrent FIFO recruitment, crafting, and healing lanes within a village (FR-001..FR-002).
/// </summary>
public enum TrainingFacilityCategory
{
    Barracks = 1,
    GreatBarracks = 2,
    Stable = 3,
    GreatStable = 4,
    Workshop = 5,
    GreatWorkshop = 6,
    ResidencePalace = 7,
    Trapper = 8,
    Healing = 9
}
