namespace Terravia.Domain.Villages;

/// <summary>
/// Categorizes troop stacks fed by or stationed at a village or oasis.
/// </summary>
public enum TroopStackType
{
    Home = 0,
    OasisReinforcement = 1,
    VillageReinforcement = 2,
    Trapped = 3,
    Hospital = 4
}
