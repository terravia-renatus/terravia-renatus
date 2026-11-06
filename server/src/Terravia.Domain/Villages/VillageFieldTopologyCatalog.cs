using System.Collections.Frozen;
using System.Collections.Immutable;
using Terravia.Domain.Oases;

namespace Terravia.Domain.Villages;

/// <summary>
/// Canonical catalog of all 12 village outer resource field topologies (<c>Slot 1..18</c>)
/// and the Crop Finder <c>BestOasisCropBonus</c> (<c>0%..150%</c>) precomputation engine.
/// </summary>
public sealed class VillageFieldTopologyCatalog
{
    public static readonly VillageFieldTopologyCatalog Default = new();

    private static readonly FrozenDictionary<int, FieldTopologyDefinition> Topologies = CreateTopologies();

    public IReadOnlyCollection<FieldTopologyDefinition> GetAllTopologies() => Topologies.Values;

    public FieldTopologyDefinition GetTopology(int fieldTopologyId)
    {
        if (!Topologies.TryGetValue(fieldTopologyId, out var def))
        {
            throw new ArgumentOutOfRangeException(nameof(fieldTopologyId), fieldTopologyId, "FieldTopologyId must be between 1 and 12.");
        }
        return def;
    }

    public void InitializeOuterResourceSlots(int fieldTopologyId, Span<int> slotBuildingTypes, Span<int> slotLevels)
    {
        var topology = GetTopology(fieldTopologyId);
        for (int slot = VillageConsts.MinResourceFieldSlotId; slot <= VillageConsts.MaxResourceFieldSlotId; slot++)
        {
            slotBuildingTypes[slot] = topology.SlotResourceTypes[slot];
            slotLevels[slot] = 0;
        }
    }

    /// <summary>
    /// Precomputes <c>MapTiles.BestOasisCropBonus</c> (<c>0, 25, 50, 75, 100, 125, 150</c>) for 9-cropper (<c>Type 1</c>)
    /// and 15-cropper (<c>Type 6</c>) tiles by scanning all oases within the <c>7x7</c> toroidal neighborhood
    /// (<c>|dx| &lt;= 3 &amp;&amp; |dy| &lt;= 3</c>), assigning <c>+50%</c> to <c>OasisTypeId = 12</c> and <c>+25%</c> to
    /// all five <c>+25%</c> crop-bearing oasis types (<c>OasisTypeId = 3, 6, 9, 10, 11</c>), and summing the top 3.
    /// </summary>
    public byte CalculateBestOasisCropBonus(
        short tileX,
        short tileY,
        byte fieldTopologyId,
        int mapRadius,
        ReadOnlySpan<OasisCoordinateBonus> nearbyOases)
    {
        if (fieldTopologyId is not (1 or 6))
            return 0;

        int top1 = 0;
        int top2 = 0;
        int top3 = 0;
        int mapDiameter = 2 * mapRadius + 1;

        foreach (ref readonly var oasis in nearbyOases)
        {
            if (!IsWithinSevenBySevenToroidal(tileX, tileY, oasis.X, oasis.Y, mapDiameter))
                continue;

            int cropBonus = GetOasisCropBonusPercent(oasis.OasisTypeId);
            if (cropBonus <= 0)
                continue;

            if (cropBonus >= top1)
            {
                top3 = top2;
                top2 = top1;
                top1 = cropBonus;
            }
            else if (cropBonus >= top2)
            {
                top3 = top2;
                top2 = cropBonus;
            }
            else if (cropBonus > top3)
            {
                top3 = cropBonus;
            }
        }

        return (byte)Math.Min(150, top1 + top2 + top3);
    }

    public static int GetOasisCropBonusPercent(byte oasisTypeId) =>
        oasisTypeId switch
        {
            12 => 50,
            3 or 6 or 9 or 10 or 11 => 25,
            _ => 0
        };

    public static bool IsWithinSevenBySevenToroidal(short x1, short y1, short x2, short y2, int mapDiameter)
    {
        int rawDx = Math.Abs(x1 - x2);
        int rawDy = Math.Abs(y1 - y2);
        int dx = mapDiameter > 0 ? Math.Min(rawDx, mapDiameter - rawDx) : rawDx;
        int dy = mapDiameter > 0 ? Math.Min(rawDy, mapDiameter - rawDy) : rawDy;
        return dx <= OasisConsts.MaxToroidalAnnexationDistance && dy <= OasisConsts.MaxToroidalAnnexationDistance;
    }

    private static FrozenDictionary<int, FieldTopologyDefinition> CreateTopologies()
    {
        var dict = new Dictionary<int, FieldTopologyDefinition>(12)
        {
            [1] = Build(1, "9-Cropper", 3, 3, 3, 9, [0, 4, 4, 1, 4, 4, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2]),
            [2] = Build(2, "Iron-Heavy (5 Iron)", 3, 4, 5, 6, [0, 3, 4, 1, 3, 2, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2]),
            [3] = Build(3, "Standard Balanced", 4, 4, 4, 6, [0, 1, 4, 1, 3, 2, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2]),
            [4] = Build(4, "Clay-Heavy (5 Clay)", 4, 5, 3, 6, [0, 1, 4, 1, 2, 2, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2]),
            [5] = Build(5, "Wood-Heavy (5 Wood)", 5, 3, 4, 6, [0, 1, 4, 1, 3, 1, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2]),
            [6] = Build(6, "15-Cropper", 1, 1, 1, 15, [0, 4, 4, 1, 3, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 2, 4, 4]),
            [7] = Build(7, "7-Cropper (Wood/Clay)", 4, 4, 3, 7, [0, 1, 4, 4, 1, 2, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2]),
            [8] = Build(8, "7-Cropper (Clay/Iron)", 3, 4, 4, 7, [0, 3, 4, 4, 1, 2, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2]),
            [9] = Build(9, "7-Cropper (Wood/Iron)", 4, 3, 4, 7, [0, 3, 4, 4, 1, 1, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2]),
            [10] = Build(10, "Clay/Iron (5 Clay)", 3, 5, 4, 6, [0, 3, 4, 1, 2, 2, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2]),
            [11] = Build(11, "Wood/Iron (5 Iron)", 4, 3, 5, 6, [0, 3, 1, 1, 3, 1, 4, 4, 3, 3, 2, 2, 3, 1, 4, 4, 2, 4, 4]),
            [12] = Build(12, "Wood/Clay (5 Wood)", 5, 4, 3, 6, [0, 1, 4, 1, 1, 2, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2])
        };
        return dict.ToFrozenDictionary();
    }

    private static FieldTopologyDefinition Build(
        int id,
        string name,
        int wood,
        int clay,
        int iron,
        int crop,
        int[] slots) =>
        new(id, name, wood, clay, iron, crop, ImmutableArray.Create(slots));
}
