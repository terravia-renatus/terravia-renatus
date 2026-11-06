using Terravia.Domain.Catalog;
using Terravia.Domain.Players;

namespace Terravia.Domain.Villages;

/// <summary>
/// Pure domain manager for Town Hall (<c>gid 24</c>) Small/Great Celebrations
/// and Teuton Brewery (<c>gid 35</c>, Capital only) Mead Festivals (<c>FR-013</c>).
/// </summary>
public sealed class CelebrationManager
{
    public static readonly ResourceCost SmallCelebrationCost = new(6400, 6650, 5940, 1340);
    public static readonly ResourceCost GreatCelebrationCost = new(29700, 33250, 32000, 6700);
    public static readonly ResourceCost MeadFestivalCost = new(3870, 1680, 215, 10900);
    public const int MeadFestivalBaseDurationSeconds = 259_200; // 72 hours at 1x speed

    // Index 1..20 -> Small Celebration base duration in seconds at 1x speed
    private static readonly int[] SmallCelebrationBaseDurations =
    [
        0,
        86400, 83290, 80291, 77401, 74614, 71928, 69338, 66843, 64436, 62117,
        59880, 57725, 55647, 53643, 51712, 49850, 48056, 46326, 44658, 43050
    ];

    // Index 10..20 -> Great Celebration base duration in seconds at 1x speed
    private static readonly int[] GreatCelebrationBaseDurations =
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        155291, 149701, 144312, 139116, 134108, 129280, 124626, 120140, 115815, 111645, 107626
    ];

    public static int GetSmallCelebrationBaseDurationSeconds(int townHallLevel) =>
        townHallLevel is >= 1 and <= 20 ? SmallCelebrationBaseDurations[townHallLevel] : 0;

    public static int GetGreatCelebrationBaseDurationSeconds(int townHallLevel) =>
        townHallLevel is >= 10 and <= 20 ? GreatCelebrationBaseDurations[townHallLevel] : 0;

    public CelebrationStartResult EvaluateStartCelebration(
        CelebrationType celebrationType,
        int townHallLevel,
        bool hasActiveCelebration,
        in ResourceCost availableResources,
        double serverSpeed = 1.0)
    {
        if (hasActiveCelebration)
        {
            return new CelebrationStartResult(
                false,
                TerraviaDomainErrorCodes.Villages.CelebrationAlreadyActive,
                celebrationType,
                ResourceCost.Zero,
                0,
                0);
        }

        double speed = serverSpeed > 0 ? serverSpeed : 1.0;

        if (celebrationType == CelebrationType.Small)
        {
            if (townHallLevel < 1)
            {
                return new CelebrationStartResult(
                    false,
                    TerraviaDomainErrorCodes.Villages.InsufficientTownHallLevel,
                    celebrationType,
                    ResourceCost.Zero,
                    0,
                    0);
            }

            if (!HasEnoughResources(in availableResources, in SmallCelebrationCost))
            {
                return new CelebrationStartResult(
                    false,
                    TerraviaDomainErrorCodes.Villages.InsufficientResources,
                    celebrationType,
                    SmallCelebrationCost,
                    0,
                    0);
            }

            int baseSec = GetSmallCelebrationBaseDurationSeconds(Math.Clamp(townHallLevel, 1, 20));
            int durationSec = Math.Max(1, (int)Math.Round(baseSec / speed, MidpointRounding.AwayFromZero));

            return new CelebrationStartResult(
                true,
                null,
                CelebrationType.Small,
                SmallCelebrationCost,
                durationSec,
                PlayerConsts.SmallCelebrationCpReward);
        }

        if (celebrationType == CelebrationType.Great)
        {
            if (townHallLevel < 10)
            {
                return new CelebrationStartResult(
                    false,
                    TerraviaDomainErrorCodes.Villages.InsufficientTownHallLevel,
                    celebrationType,
                    ResourceCost.Zero,
                    0,
                    0);
            }

            if (!HasEnoughResources(in availableResources, in GreatCelebrationCost))
            {
                return new CelebrationStartResult(
                    false,
                    TerraviaDomainErrorCodes.Villages.InsufficientResources,
                    celebrationType,
                    GreatCelebrationCost,
                    0,
                    0);
            }

            int baseSec = GetGreatCelebrationBaseDurationSeconds(Math.Clamp(townHallLevel, 10, 20));
            int durationSec = Math.Max(1, (int)Math.Round(baseSec / speed, MidpointRounding.AwayFromZero));

            return new CelebrationStartResult(
                true,
                null,
                CelebrationType.Great,
                GreatCelebrationCost,
                durationSec,
                PlayerConsts.GreatCelebrationCpReward);
        }

        return new CelebrationStartResult(
            false,
            TerraviaDomainErrorCodes.Villages.InsufficientTownHallLevel,
            celebrationType,
            ResourceCost.Zero,
            0,
            0);
    }

    public MeadFestivalStartResult EvaluateStartMeadFestival(
        int ownerTribeId,
        bool isCapital,
        int breweryLevel,
        bool hasActiveFestival,
        in ResourceCost availableResources,
        double serverSpeed = 1.0)
    {
        if (ownerTribeId != 2 || !isCapital || breweryLevel < 1)
        {
            return new MeadFestivalStartResult(
                false,
                TerraviaDomainErrorCodes.Villages.MeadFestivalRequiresTeutonCapitalBrewery,
                ResourceCost.Zero,
                0,
                0,
                false,
                1.0);
        }

        if (hasActiveFestival)
        {
            return new MeadFestivalStartResult(
                false,
                TerraviaDomainErrorCodes.Villages.FestivalAlreadyActive,
                ResourceCost.Zero,
                0,
                0,
                false,
                1.0);
        }

        if (!HasEnoughResources(in availableResources, in MeadFestivalCost))
        {
            return new MeadFestivalStartResult(
                false,
                TerraviaDomainErrorCodes.Villages.InsufficientResources,
                MeadFestivalCost,
                0,
                0,
                false,
                1.0);
        }

        double speed = serverSpeed > 0 ? serverSpeed : 1.0;
        int durationSec = Math.Max(1, (int)Math.Round(MeadFestivalBaseDurationSeconds / speed, MidpointRounding.AwayFromZero));
        int attackBonusPercent = Math.Clamp(breweryLevel, 1, 10);

        return new MeadFestivalStartResult(
            true,
            null,
            MeadFestivalCost,
            durationSec,
            attackBonusPercent,
            ForceRandomCatapultTargets: true,
            ChiefPersuasionMultiplier: 0.5);
    }

    private static bool HasEnoughResources(in ResourceCost available, in ResourceCost required) =>
        available.Wood >= required.Wood &&
        available.Clay >= required.Clay &&
        available.Iron >= required.Iron &&
        available.Crop >= required.Crop;
}
