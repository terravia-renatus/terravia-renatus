namespace Terravia.Domain.Villages;

/// <summary>
/// Represents a group of attacking troops captured by a Gaul village's Trapper (or Archon Capital) (FR-014..FR-015).
/// </summary>
public sealed class PrisonerStack
{
    public long PrisonerStackId { get; set; }
    public int CaptorVillageId { get; set; }
    public int OwnerVillageId { get; set; }
    public int OwnerPlayerId { get; set; }
    public int OwnerAllianceId { get; set; }
    public int OwnerTribeId { get; set; }
    public int[] UnitCountsBySlot { get; init; } = new int[11];
    public bool HasCapturedHero { get; set; }

    public int TotalOccupiedTraps
    {
        get
        {
            int sum = HasCapturedHero ? 1 : 0;
            for (int i = 1; i <= 10 && i < UnitCountsBySlot.Length; i++)
            {
                sum += UnitCountsBySlot[i];
            }
            return sum;
        }
    }
}
