using Terravia.Domain.Oases;
using Terravia.Domain.Villages;

namespace Terravia.Domain.Tests.Villages;

public sealed class VillageFieldTopologyAndCropFinderTests
{
    private readonly VillageFieldTopologyCatalog _catalog = VillageFieldTopologyCatalog.Default;

    [Theory]
    [InlineData(1, 3, 3, 3, 9, new[] { 4, 4, 1, 4, 4, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2 })]
    [InlineData(2, 3, 4, 5, 6, new[] { 3, 4, 1, 3, 2, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2 })]
    [InlineData(3, 4, 4, 4, 6, new[] { 1, 4, 1, 3, 2, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2 })]
    [InlineData(4, 4, 5, 3, 6, new[] { 1, 4, 1, 2, 2, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2 })]
    [InlineData(5, 5, 3, 4, 6, new[] { 1, 4, 1, 3, 1, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2 })]
    [InlineData(6, 1, 1, 1, 15, new[] { 4, 4, 1, 3, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 2, 4, 4 })]
    [InlineData(7, 4, 4, 3, 7, new[] { 1, 4, 4, 1, 2, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2 })]
    [InlineData(8, 3, 4, 4, 7, new[] { 3, 4, 4, 1, 2, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2 })]
    [InlineData(9, 4, 3, 4, 7, new[] { 3, 4, 4, 1, 1, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2 })]
    [InlineData(10, 3, 5, 4, 6, new[] { 3, 4, 1, 2, 2, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2 })]
    [InlineData(11, 4, 3, 5, 6, new[] { 3, 1, 1, 3, 1, 4, 4, 3, 3, 2, 2, 3, 1, 4, 4, 2, 4, 4 })]
    [InlineData(12, 5, 4, 3, 6, new[] { 1, 4, 1, 1, 2, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2 })]
    public void AllTwelveVillageTopologies_MatchCanonicalSlotMappingsAndCounts(
        int topologyId,
        int expectedWood,
        int expectedClay,
        int expectedIron,
        int expectedCrop,
        int[] expectedSlots1To18)
    {
        var def = _catalog.GetTopology(topologyId);

        Assert.Equal(expectedWood, def.WoodFields);
        Assert.Equal(expectedClay, def.ClayFields);
        Assert.Equal(expectedIron, def.IronFields);
        Assert.Equal(expectedCrop, def.CropFields);

        int actualWood = 0, actualClay = 0, actualIron = 0, actualCrop = 0;
        for (int slot = 1; slot <= 18; slot++)
        {
            int gid = def.SlotResourceTypes[slot];
            Assert.Equal(expectedSlots1To18[slot - 1], gid);
            if (gid == 1) actualWood++;
            else if (gid == 2) actualClay++;
            else if (gid == 3) actualIron++;
            else if (gid == 4) actualCrop++;
        }

        Assert.Equal(expectedWood, actualWood);
        Assert.Equal(expectedClay, actualClay);
        Assert.Equal(expectedIron, actualIron);
        Assert.Equal(expectedCrop, actualCrop);
    }

    [Fact]
    public void StartingCapitalDefault_IsType3_StandardBalanced()
    {
        Assert.Equal(3, VillageConsts.DefaultStartingCapitalTopologyId);
        var def = _catalog.GetTopology(VillageConsts.DefaultStartingCapitalTopologyId);
        Assert.Equal(4, def.WoodFields);
        Assert.Equal(4, def.ClayFields);
        Assert.Equal(4, def.IronFields);
        Assert.Equal(6, def.CropFields);
    }

    [Fact]
    public void BestOasisCropBonus_CountsAllFive25PercentCropTypesAnd50PercentType12_WithToroidalWraparound()
    {
        // Map radius 200 -> diameter 401. Tile at (199, -199) is within 3 tiles toroidally of (-200, 200)
        ReadOnlySpan<OasisCoordinateBonus> oases =
        [
            new OasisCoordinateBonus(199, -198, 3),    // +25% (Wood + Crop)
            new OasisCoordinateBonus(200, -199, 6),    // +25% (Clay + Crop)
            new OasisCoordinateBonus(-200, -199, 9),   // +25% (Iron + Crop, toroidal X wraparound: dx = 2)
            new OasisCoordinateBonus(199, 200, 10),    // +25% (+25% Crop, toroidal Y wraparound: dy = 2)
            new OasisCoordinateBonus(198, -199, 11),   // +25% (+25% Crop)
            new OasisCoordinateBonus(-200, 200, 12),   // +50% (+50% Crop, toroidal corner wraparound: dx=2, dy=2)
            new OasisCoordinateBonus(199, -199, 1),    // +0% (Wood only)
            new OasisCoordinateBonus(0, 0, 12)         // Out of 7x7 range
        ];

        // Top 3 among (50, 25, 25, 25, 25, 25) = 50 + 25 + 25 = 100
        byte bonus9Cropper = _catalog.CalculateBestOasisCropBonus(199, -199, 1, 200, oases);
        byte bonus15Cropper = _catalog.CalculateBestOasisCropBonus(199, -199, 6, 200, oases);
        byte bonusStandard = _catalog.CalculateBestOasisCropBonus(199, -199, 3, 200, oases);

        Assert.Equal(100, bonus9Cropper);
        Assert.Equal(100, bonus15Cropper);
        Assert.Equal(0, bonusStandard);
    }

    [Fact]
    public void BestOasisCropBonus_ThreeFiftyPercentCropOases_Reaches150Max()
    {
        ReadOnlySpan<OasisCoordinateBonus> oases =
        [
            new OasisCoordinateBonus(10, 10, 12),
            new OasisCoordinateBonus(12, 13, 12),
            new OasisCoordinateBonus(7, 7, 12),
            new OasisCoordinateBonus(11, 11, 12)
        ];

        byte bonus = _catalog.CalculateBestOasisCropBonus(10, 10, 6, 200, oases);
        Assert.Equal(150, bonus);
    }
}
