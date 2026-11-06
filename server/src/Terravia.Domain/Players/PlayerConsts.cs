namespace Terravia.Domain.Players;

/// <summary>
/// Domain constants for player Culture Point progression curves, Alliance Philosophy bonuses, and Hero CP helmets.
/// </summary>
public static class PlayerConsts
{
    public const int MinVillageNumber = 1;
    public const int MaxCulturePointTableVillageNumber = 125;

    public const double MinAlliancePhilosophyMultiplier = 1.00;
    public const double MaxAlliancePhilosophyMultiplier = 1.10;

    public const int MinHeroHelmetCpPerDay = 0;
    public const int MaxBaseHeroHelmetCpPerDay = 2400;

    public const int SmallCelebrationCpReward = 500;
    public const int GreatCelebrationCpReward = 2000;
}
