namespace Terravia.Infrastructure.Villages;

/// <summary>
/// Dapper persistence row model for <c>dbo.VillageTechnologies</c>, storing unlocked unit slot bitmask,
/// Blacksmith/Armoury levels (1..8), and Hospital wounded unit counts (1..8).
/// </summary>
public sealed class VillageTechnology
{
    public int VillageId { get; init; }
    public int UnlockedSlotsMask { get; init; } = (1 << 1) | (1 << 10);
    public int[] OffenseUpgradeLevels { get; init; } = new int[9];
    public int[] DefenseUpgradeLevels { get; init; } = new int[9];
    public int[] WoundedUnitsBySlot { get; init; } = new int[9];
}
