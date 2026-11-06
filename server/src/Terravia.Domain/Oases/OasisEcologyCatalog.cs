using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Terravia.Domain.Oases;

/// <summary>
/// Canonical catalog of all 12 oasis types (<c>OasisTypeId 1..12</c>), Nature (<c>Tribe 4</c>) fauna
/// regeneration rules (<c>u31..u40</c>), <c>RichnessTier</c> (<c>0, 1, 2</c>) stack caps, and server progression scaling.
/// </summary>
public sealed class OasisEcologyCatalog
{
    public static readonly OasisEcologyCatalog Default = new();

    private static readonly FrozenDictionary<int, OasisTypeDefinition> OasisTypes = CreateOasisTypes();

    public IReadOnlyCollection<OasisTypeDefinition> GetAllOasisTypes() => OasisTypes.Values;

    public OasisTypeDefinition GetOasisType(int oasisTypeId)
    {
        if (!OasisTypes.TryGetValue(oasisTypeId, out var def))
        {
            throw new ArgumentOutOfRangeException(nameof(oasisTypeId), oasisTypeId, "OasisTypeId must be between 1 and 12.");
        }
        return def;
    }

    /// <summary>
    /// Computes the server progression growth factor bounded to <c>[0.3, 3.0]</c>:
    /// <c>min(3.0, max(0.3, avgPlayerProgress / 1000.0))</c>.
    /// </summary>
    public static double CalculateGrowthFactor(double avgPlayerProgress) =>
        Math.Min(OasisConsts.MaxGrowthFactor, Math.Max(OasisConsts.MinGrowthFactor, avgPlayerProgress / 1000.0));

    /// <summary>
    /// Computes per-unit-type minimum and maximum fauna stack caps for a given <c>RichnessTier</c> (<c>0, 1, 2</c>),
    /// active player count, and <c>growthFactor</c>.
    /// </summary>
    public static (int MinUnits, int MaxUnits) CalculateRichnessTierUnitCaps(
        int richnessTier,
        int activePlayerCount,
        double growthFactor)
    {
        double gf = Math.Clamp(growthFactor, OasisConsts.MinGrowthFactor, OasisConsts.MaxGrowthFactor);
        int players = Math.Max(0, activePlayerCount);

        return richnessTier switch
        {
            0 => ComputeTier(5, 30, 5, 1.5, players, gf),
            1 => ComputeTier(10, 60, 10, 2.0, players, gf),
            2 => ComputeTier(20, 90, 15, 3.0, players, gf),
            _ => throw new ArgumentOutOfRangeException(nameof(richnessTier), richnessTier, "RichnessTier must be 0, 1, or 2.")
        };

        static (int Min, int Max) ComputeTier(
            int baseFloor,
            int hardCap,
            int baseSpread,
            double playerCoef,
            int playerCount,
            double growth)
        {
            int minUnits = Math.Max(baseFloor, (int)Math.Floor(baseFloor * growth));
            int maxUnits = Math.Min(hardCap, (int)Math.Floor(minUnits + baseSpread + playerCoef * playerCount * growth));
            return (minUnits, Math.Max(minUnits, maxUnits));
        }
    }

    /// <summary>
    /// Regenerates wild Nature fauna (<c>u31..u40</c>) in-place toward the oasis's <c>RichnessTier</c> cap.
    /// </summary>
    public void RegenerateWildOasisFauna(
        int oasisTypeId,
        int richnessTier,
        int activePlayerCount,
        double avgPlayerProgress,
        Span<int> unitCounts31To40,
        Func<int, int, int>? inclusiveRandomProvider = null)
    {
        var def = GetOasisType(oasisTypeId);
        double growthFactor = CalculateGrowthFactor(avgPlayerProgress);
        var (_, maxUnits) = CalculateRichnessTierUnitCaps(richnessTier, activePlayerCount, growthFactor);
        var rng = inclusiveRandomProvider ?? ((min, max) => Random.Shared.Next(min, max + 1));

        foreach (var rule in def.FaunaSpawnRules)
        {
            int idx = rule.UnitTypeId - OasisConsts.MinNatureUnitTypeId;
            if (idx < 0 || idx >= unitCounts31To40.Length)
                continue;

            int current = unitCounts31To40[idx];
            if (current >= maxUnits)
                continue;

            int roll = rule.BaseMinIncrement + rng(0, rule.RandomRangeMax);
            int scaledIncrement = Math.Max(1, (int)Math.Floor(roll * growthFactor));
            unitCounts31To40[idx] = Math.Min(maxUnits, current + scaledIncrement);
        }
    }

    private static FrozenDictionary<int, OasisTypeDefinition> CreateOasisTypes()
    {
        ImmutableArray<OasisFaunaSpawnRule> woodRules =
        [
            new(35, 5, 10),
            new(36, 0, 5),
            new(37, 0, 5)
        ];

        ImmutableArray<OasisFaunaSpawnRule> woodCropRules =
        [
            new(35, 5, 15),
            new(36, 0, 5),
            new(37, 0, 5),
            new(38, 0, 5),
            new(40, 0, 3)
        ];

        ImmutableArray<OasisFaunaSpawnRule> clayRules =
        [
            new(31, 10, 15),
            new(32, 5, 15),
            new(35, 0, 10)
        ];

        ImmutableArray<OasisFaunaSpawnRule> clayCropRules =
        [
            new(31, 15, 20),
            new(32, 10, 15),
            new(35, 0, 10),
            new(40, 0, 3)
        ];

        ImmutableArray<OasisFaunaSpawnRule> ironRules =
        [
            new(31, 10, 15),
            new(32, 5, 15),
            new(34, 0, 10)
        ];

        ImmutableArray<OasisFaunaSpawnRule> ironCropRules =
        [
            new(31, 15, 20),
            new(32, 10, 15),
            new(34, 0, 10),
            new(39, 0, 3)
        ];

        ImmutableArray<OasisFaunaSpawnRule> crop25Rules =
        [
            new(31, 5, 15),
            new(33, 5, 10),
            new(37, 0, 10),
            new(38, 0, 5),
            new(39, 0, 5)
        ];

        ImmutableArray<OasisFaunaSpawnRule> crop50Rules =
        [
            new(31, 10, 15),
            new(33, 5, 10),
            new(37, 0, 10),
            new(38, 0, 5),
            new(39, 0, 5),
            new(40, 0, 3)
        ];

        var dict = new Dictionary<int, OasisTypeDefinition>(12)
        {
            [1] = new(1, "+25% Wood", 1, 0, 0, 0, woodRules),
            [2] = new(2, "+25% Wood", 1, 0, 0, 0, woodRules),
            [3] = new(3, "+25% Wood + +25% Crop", 1, 0, 0, 1, woodCropRules),
            [4] = new(4, "+25% Clay", 0, 1, 0, 0, clayRules),
            [5] = new(5, "+25% Clay", 0, 1, 0, 0, clayRules),
            [6] = new(6, "+25% Clay + +25% Crop", 0, 1, 0, 1, clayCropRules),
            [7] = new(7, "+25% Iron", 0, 0, 1, 0, ironRules),
            [8] = new(8, "+25% Iron", 0, 0, 1, 0, ironRules),
            [9] = new(9, "+25% Iron + +25% Crop", 0, 0, 1, 1, ironCropRules),
            [10] = new(10, "+25% Crop", 0, 0, 0, 1, crop25Rules),
            [11] = new(11, "+25% Crop", 0, 0, 0, 1, crop25Rules),
            [12] = new(12, "+50% Crop", 0, 0, 0, 2, crop50Rules)
        };

        return dict.ToFrozenDictionary();
    }
}
