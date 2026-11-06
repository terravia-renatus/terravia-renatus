using Terravia.Domain.Players;

namespace Terravia.Domain.Villages;

/// <summary>
/// Authoritative strictly monotonic Culture Point (CP) threshold catalog across <c>Villages 1..125</c> (<c>FR-014</c>).
/// Standard curve: <c>round(1600 * (N - 1)^2.3, -3)</c> (with <c>N = 2</c> at <c>2,000</c>).
/// Fast curve: <c>round((1/3) * 1600 * (N - 1)^2.3, -2)</c> (with <c>N = 2</c> at <c>500</c> and <c>N = 33</c> fixed to <c>1,544,700</c>).
/// </summary>
public sealed class VillageExpansionCatalog
{
    public static readonly VillageExpansionCatalog Default = new();

    private static readonly int[] StandardThresholds = BuildStandardTable();
    private static readonly int[] FastThresholds = BuildFastTable();

    public int GetRequiredCulturePoints(int targetVillageNumber, CulturePointCurveMode curveMode)
    {
        if (targetVillageNumber <= 1)
            return 0;

        if (targetVillageNumber <= PlayerConsts.MaxCulturePointTableVillageNumber)
        {
            return curveMode == CulturePointCurveMode.Fast
                ? FastThresholds[targetVillageNumber]
                : StandardThresholds[targetVillageNumber];
        }

        return curveMode == CulturePointCurveMode.Fast
            ? ComputeFastThreshold(targetVillageNumber)
            : ComputeStandardThreshold(targetVillageNumber);
    }

    public static int ComputeStandardThreshold(int n)
    {
        if (n <= 1) return 0;
        if (n == 2) return 2000;
        double raw = 1600.0 * Math.Pow(n - 1, 2.3);
        return (int)(Math.Round(raw / 1000.0, MidpointRounding.AwayFromZero) * 1000.0);
    }

    public static int ComputeFastThreshold(int n)
    {
        if (n <= 1) return 0;
        if (n == 2) return 500;
        double raw = (1600.0 / 3.0) * Math.Pow(n - 1, 2.3);
        return (int)(Math.Round(raw / 100.0, MidpointRounding.AwayFromZero) * 100.0);
    }

    private static int[] BuildStandardTable()
    {
        var arr = new int[PlayerConsts.MaxCulturePointTableVillageNumber + 1];
        for (int n = 1; n <= PlayerConsts.MaxCulturePointTableVillageNumber; n++)
        {
            arr[n] = ComputeStandardThreshold(n);
        }
        return arr;
    }

    private static int[] BuildFastTable()
    {
        var arr = new int[PlayerConsts.MaxCulturePointTableVillageNumber + 1];
        for (int n = 1; n <= PlayerConsts.MaxCulturePointTableVillageNumber; n++)
        {
            arr[n] = ComputeFastThreshold(n);
        }
        return arr;
    }
}
