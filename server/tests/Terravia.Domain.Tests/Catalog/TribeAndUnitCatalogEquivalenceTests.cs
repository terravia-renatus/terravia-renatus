using System.Text.Json;
using Terravia.Domain.Catalog;

namespace Terravia.Domain.Tests.Catalog;

public sealed class TribeAndUnitCatalogEquivalenceTests
{
    private readonly IGameCatalog _catalog = FrozenGameCatalog.Default;
    private readonly IConstructionCalculator _calculator = new ConstructionCalculator(FrozenGameCatalog.Default);

    private static JsonDocument LoadCanonicalJson()
    {
        using var stream = typeof(FrozenGameCatalog).Assembly
            .GetManifestResourceStream(CatalogJsonLoader.EmbeddedResourceName)!;
        return JsonDocument.Parse(stream);
    }

    [Fact]
    public void Catalog_ShouldExposeDeterministicSha256ContentHash_AndVersion()
    {
        Assert.Equal("1.0.0", _catalog.SchemaVersion);
        Assert.StartsWith("sha256:", _catalog.ContentHash);
        Assert.Equal(7 + 64, _catalog.ContentHash.Length);
    }

    [Fact]
    public void AllNineTribes_ShouldMatchCanonicalCatalogDataJson_OneToOne()
    {
        using var doc = LoadCanonicalJson();
        var tribesJson = doc.RootElement.GetProperty("tribesById");

        var allTribes = _catalog.GetAllTribes();
        Assert.Equal(9, allTribes.Length);

        for (int tid = 1; tid <= 9; tid++)
        {
            var expected = tribesJson.GetProperty(tid.ToString());
            var actual = _catalog.GetTribe(tid);

            Assert.Equal(tid, actual.TribeId);
            Assert.Equal(expected.GetProperty("name").GetString(), actual.Name);
            Assert.Equal(expected.GetProperty("playable").GetBoolean(), actual.IsPlayable);
            Assert.Equal(expected.GetProperty("merchantCapacity").GetInt32(), actual.MerchantCapacity);
            Assert.Equal(expected.GetProperty("merchantSpeed").GetInt32(), actual.MerchantSpeed);
            Assert.Equal(expected.GetProperty("wallFactor").GetDouble(), actual.WallFactor, 4);
            Assert.Equal(expected.GetProperty("dualBuildQueue").GetBoolean(), actual.HasDualBuildQueue);

            double expectedCranny = expected.TryGetProperty("crannyMultiplier", out var cm) ? cm.GetDouble() : 1.0;
            Assert.Equal(expectedCranny, actual.CrannyMultiplier, 4);

            int expectedWallGid = expected.GetProperty("wallGid").ValueKind == JsonValueKind.Null
                ? 0
                : expected.GetProperty("wallGid").GetInt32();
            Assert.Equal(expectedWallGid, actual.WallBuildingTypeId);

            // Verify 10 designated units (T-1)*10 + 1 .. +10
            Assert.Equal(10, actual.UnitIds.Length);
            var tribeUnits = _catalog.GetUnitsForTribe(tid);
            Assert.Equal(10, tribeUnits.Length);
            for (int slot = 1; slot <= 10; slot++)
            {
                int expectedUid = (tid - 1) * 10 + slot;
                Assert.Equal(expectedUid, actual.UnitIds[slot - 1]);
                Assert.Equal(expectedUid, tribeUnits[slot - 1].UnitTypeId);
                Assert.Equal(slot, tribeUnits[slot - 1].TribeUnitSlot);
                Assert.Equal(tid, tribeUnits[slot - 1].TribeId);
            }
        }

        // Playable vs NPC check
        Assert.False(_catalog.GetTribe(4).IsPlayable); // Nature
        Assert.False(_catalog.GetTribe(5).IsPlayable); // Archons
        Assert.Equal("Archons", _catalog.GetTribe(5).Name);

        // Huns command building check
        Assert.False(_catalog.GetTribe(6).CanBuildResidenceOrPalace);
        Assert.Equal([44], _catalog.GetTribe(6).CommandBuildingTypeIds.ToArray());
        Assert.True(_catalog.GetTribe(1).CanBuildResidenceOrPalace);
        Assert.Equal([25, 26], _catalog.GetTribe(1).CommandBuildingTypeIds.ToArray());

        // Spartans & Vikings Big Hospital check
        Assert.Equal(48, _catalog.GetTribe(8).HospitalBuildingTypeId);
        Assert.Equal(48, _catalog.GetTribe(9).HospitalBuildingTypeId);
        Assert.Equal(46, _catalog.GetTribe(1).HospitalBuildingTypeId);
    }

    [Fact]
    public void AllNinetyOneUnits_ShouldMatchCanonicalCatalogDataJson_OneToOne()
    {
        using var doc = LoadCanonicalJson();
        var unitsJson = doc.RootElement.GetProperty("unitsById");
        var researchJson = doc.RootElement.GetProperty("academyResearchByUnitId");
        var smithyJson = doc.RootElement.GetProperty("smithyUpgradesByUnitId");

        var allUnits = _catalog.GetAllUnits();
        Assert.Equal(91, allUnits.Length);

        foreach (var prop in unitsJson.EnumerateObject())
        {
            int uid = int.Parse(prop.Name);
            var expected = prop.Value;
            var actual = _catalog.GetUnit(uid);

            Assert.Equal(uid, actual.UnitTypeId);
            Assert.Equal(expected.GetProperty("tribeId").GetInt32(), actual.TribeId);
            Assert.Equal(expected.GetProperty("name").GetString(), actual.Name);
            Assert.Equal(expected.GetProperty("atk").GetInt32(), actual.Offense);
            Assert.Equal(expected.GetProperty("di").GetInt32(), actual.DefenseInfantry);
            Assert.Equal(expected.GetProperty("dc").GetInt32(), actual.DefenseCavalry);
            Assert.Equal(expected.GetProperty("wood").GetInt32(), actual.Cost.Wood);
            Assert.Equal(expected.GetProperty("clay").GetInt32(), actual.Cost.Clay);
            Assert.Equal(expected.GetProperty("iron").GetInt32(), actual.Cost.Iron);
            Assert.Equal(expected.GetProperty("crop").GetInt32(), actual.Cost.Crop);
            Assert.Equal(expected.GetProperty("pop").GetInt32(), actual.UpkeepCropPerHour);
            Assert.Equal(expected.GetProperty("speed").GetInt32(), actual.SpeedTilesPerHour);
            Assert.Equal(expected.GetProperty("time").GetInt32(), actual.BaseTrainingSeconds);
            Assert.Equal(expected.GetProperty("cap").GetInt32(), actual.CarryCapacity);

            if (researchJson.TryGetProperty(prop.Name, out var rExpected))
            {
                Assert.NotNull(actual.ResearchCost);
                Assert.Equal(rExpected.GetProperty("wood").GetInt32(), actual.ResearchCost.Value.Wood);
                Assert.Equal(rExpected.GetProperty("clay").GetInt32(), actual.ResearchCost.Value.Clay);
                Assert.Equal(rExpected.GetProperty("iron").GetInt32(), actual.ResearchCost.Value.Iron);
                Assert.Equal(rExpected.GetProperty("crop").GetInt32(), actual.ResearchCost.Value.Crop);
                Assert.Equal(rExpected.GetProperty("time").GetInt32(), actual.BaseResearchSeconds);
            }

            if (smithyJson.TryGetProperty(prop.Name, out var sExpected))
            {
                Assert.Equal(20, actual.SmithyUpgrades.Length);
                for (int lvl = 1; lvl <= 20; lvl++)
                {
                    var sRow = sExpected.GetProperty(lvl.ToString());
                    var actualLevel = actual.SmithyUpgrades[lvl - 1];
                    Assert.Equal(lvl, actualLevel.Level);
                    Assert.Equal(sRow.GetProperty("wood").GetInt32(), actualLevel.Cost.Wood);
                    Assert.Equal(sRow.GetProperty("clay").GetInt32(), actualLevel.Cost.Clay);
                    Assert.Equal(sRow.GetProperty("iron").GetInt32(), actualLevel.Cost.Iron);
                    Assert.Equal(sRow.GetProperty("crop").GetInt32(), actualLevel.Cost.Crop);
                    Assert.Equal(sRow.GetProperty("time").GetInt32(), actualLevel.BaseTimeSeconds);
                }
            }
        }

        // Verify special scout training buildings (u52, u74, u82 -> Barracks 19)
        Assert.Equal(19, _catalog.GetUnit(52).TrainingBuildingTypeId);
        Assert.Equal(19, _catalog.GetUnit(74).TrainingBuildingTypeId);
        Assert.Equal(19, _catalog.GetUnit(82).TrainingBuildingTypeId);
        Assert.True(_catalog.GetUnit(52).TacticalRole.HasFlag(UnitTacticalRole.Scout));
        Assert.True(_catalog.GetUnit(74).TacticalRole.HasFlag(UnitTacticalRole.Scout));
        Assert.True(_catalog.GetUnit(82).TacticalRole.HasFlag(UnitTacticalRole.Scout));

        // Verify Hun Chief/Settler train in Command Center (44)
        Assert.Equal([44], _catalog.GetUnit(59).AllowedTrainingBuildingTypeIds.ToArray());
        Assert.Equal([44], _catalog.GetUnit(60).AllowedTrainingBuildingTypeIds.ToArray());

        // Verify Trap (u99)
        var trap = _catalog.GetUnit(99);
        Assert.Equal(UnitCategory.Trap, trap.Category);
        Assert.Equal(36, trap.TrainingBuildingTypeId);
    }

    [Fact]
    public void AllThirtyFiveHeroTemplates_AndReviveTables_ShouldMatchCanonicalCatalog()
    {
        using var doc = LoadCanonicalJson();
        var heroJson = doc.RootElement.GetProperty("heroScalingTemplatesByUnitId");
        var reviveJson = doc.RootElement.GetProperty("heroReviveTablesByUnitId");

        Assert.Equal(35, _catalog.GetAllHeroTemplates().Length);
        Assert.Equal(121, _catalog.GetHeroExperienceThresholds().Length);
        Assert.Equal(0, _catalog.GetHeroExperienceThresholds()[0]);
        Assert.Equal(100, _catalog.GetHeroExperienceThresholds()[1]);

        foreach (var prop in heroJson.EnumerateObject())
        {
            int uid = int.Parse(prop.Name);
            var expected = prop.Value;

            Assert.True(_catalog.TryGetHeroTemplate(uid, out var actual));
            Assert.Equal(uid, actual.UnitTypeId);
            Assert.Equal(expected.GetProperty("atk").GetDouble(), actual.BaseAttack, 4);
            Assert.Equal(expected.GetProperty("atkp").GetDouble(), actual.AttackPerPoint, 4);
            Assert.Equal(expected.GetProperty("di").GetDouble(), actual.BaseDefenseInfantry, 4);
            Assert.Equal(expected.GetProperty("dip").GetDouble(), actual.DefenseInfantryPerPoint, 4);
            Assert.Equal(expected.GetProperty("dc").GetDouble(), actual.BaseDefenseCavalry, 4);
            Assert.Equal(expected.GetProperty("dcp").GetDouble(), actual.DefenseCavalryPerPoint, 4);

            var revExpected = reviveJson.GetProperty(prop.Name);
            Assert.Equal(revExpected.GetArrayLength(), actual.ReviveTable.Length);
            Assert.Equal(61, actual.ReviveTable.Length);
        }
    }

    [Fact]
    public void RomanHorseDrinkingTrough_ShouldReduceCavalryUpkeepAtThresholds()
    {
        // u4 Equites Scout (base pop 2, HDT threshold 10)
        Assert.Equal(20.0, _calculator.CalculateUnitUpkeepPerHour(4, count: 10, horseDrinkingTroughLevel: 9));
        Assert.Equal(10.0, _calculator.CalculateUnitUpkeepPerHour(4, count: 10, horseDrinkingTroughLevel: 10));

        // u5 Equites Eques (base pop 3, HDT threshold 15)
        Assert.Equal(30.0, _calculator.CalculateUnitUpkeepPerHour(5, count: 10, horseDrinkingTroughLevel: 14));
        Assert.Equal(20.0, _calculator.CalculateUnitUpkeepPerHour(5, count: 10, horseDrinkingTroughLevel: 15));

        // u6 Equites Cataphract (base pop 4, HDT threshold 20)
        Assert.Equal(40.0, _calculator.CalculateUnitUpkeepPerHour(6, count: 10, horseDrinkingTroughLevel: 19));
        Assert.Equal(30.0, _calculator.CalculateUnitUpkeepPerHour(6, count: 10, horseDrinkingTroughLevel: 20));
        Assert.Equal(15.0, _calculator.CalculateUnitUpkeepPerHour(6, count: 10, horseDrinkingTroughLevel: 20, artifactDietMultiplier: 0.5));
    }
}
