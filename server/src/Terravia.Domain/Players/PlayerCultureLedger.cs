namespace Terravia.Domain.Players;

/// <summary>
/// In-memory Culture Point and village expansion ledger managed by <c>PlayerActor</c>.
/// </summary>
public sealed class PlayerCultureLedger
{
    public int PlayerId { get; }
    public double AccumulatedCulturePoints { get; set; }
    public int TotalVillageBaseCpPerDay { get; set; }
    public int HeroHelmetCpPerDay { get; set; }
    public double AlliancePhilosophyMultiplier { get; set; } = PlayerConsts.MinAlliancePhilosophyMultiplier;
    public long LastCpUpdatedMs { get; set; }
    public int OwnedVillageCount { get; set; } = 1;
    public int CapitalVillageId { get; set; }

    public PlayerCultureLedger(int playerId, int capitalVillageId, long initialTimestampMs = 0)
    {
        PlayerId = playerId;
        CapitalVillageId = capitalVillageId;
        LastCpUpdatedMs = initialTimestampMs;
    }

    public double EffectiveDailyCulturePointsRate
    {
        get
        {
            double philosophy = Math.Clamp(
                AlliancePhilosophyMultiplier,
                PlayerConsts.MinAlliancePhilosophyMultiplier,
                PlayerConsts.MaxAlliancePhilosophyMultiplier);

            return Math.Max(0, TotalVillageBaseCpPerDay) * philosophy + Math.Max(0, HeroHelmetCpPerDay);
        }
    }

    public void AccrueCulturePointsUpTo(long nowMs)
    {
        if (nowMs <= LastCpUpdatedMs)
            return;

        double elapsedDays = (nowMs - LastCpUpdatedMs) / 86_400_000.0;
        AccumulatedCulturePoints += EffectiveDailyCulturePointsRate * elapsedDays;
        LastCpUpdatedMs = nowMs;
    }

    public void AwardCelebrationCulturePoints(int flatCpAward)
    {
        if (flatCpAward > 0)
        {
            AccumulatedCulturePoints += flatCpAward;
        }
    }
}
