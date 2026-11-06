using System.Text.Json;
using Terravia.Domain.Catalog;

namespace Terravia.Domain.Tests.Catalog;

public sealed class BuildingCatalogAndPrerequisiteTests
{
    private readonly IGameCatalog _catalog = FrozenGameCatalog.Default;
    private readonly IConstructionCalculator _calculator;
    private readonly IBuildingRuleCatalog _ruleCatalog;

    public BuildingCatalogAndPrerequisiteTests()
    {
        _calculator = new ConstructionCalculator(_catalog);
        _ruleCatalog = new BuildingRuleCatalog(_catalog, _calculator);
    }

    private static JsonDocument LoadCanonicalJson()
    {
        using var stream = typeof(FrozenGameCatalog).Assembly
            .GetManifestResourceStream(CatalogJsonLoader.EmbeddedResourceName)!;
        return JsonDocument.Parse(stream);
    }

    private static VillagePrerequisiteContext CreateContext(
        int tribeId = 1,
        bool isCapital = true,
        bool isArchonVillage = false,
        bool isWorldWonderVillage = false,
        bool hasAccountPalace = false,
        bool hasGreatStorageArtifact = false,
        int ownedWwPlanCount = 0,
        int allianceWwPlanCount = 0,
        bool greatWorkshopEnabled = true,
        int worldWonderLevel = 0,
        int maxQueuedWorldWonderLevel = 0,
        params (int Slot, int Gid, int Level)[] existingBuildings)
    {
        var types = new int[41];
        var levels = new int[41];
        foreach (var (slot, gid, level) in existingBuildings)
        {
            types[slot] = gid;
            levels[slot] = level;
        }

        return new VillagePrerequisiteContext(
            TribeId: tribeId,
            IsCapital: isCapital,
            IsArchonVillage: isArchonVillage,
            IsWorldWonderVillage: isWorldWonderVillage,
            HasAccountPalace: hasAccountPalace,
            HasGreatStorageArtifact: hasGreatStorageArtifact,
            OwnedWwPlanCount: ownedWwPlanCount,
            AllianceWwPlanCount: allianceWwPlanCount,
            GreatWorkshopEnabled: greatWorkshopEnabled,
            SlotBuildingTypes: types,
            SlotLevels: levels,
            WorldWonderLevel: worldWonderLevel,
            MaxQueuedWorldWonderLevel: maxQueuedWorldWonderLevel);
    }

    [Fact]
    public void AllFiftyBuildings_AndAllLevels_ShouldMatchCanonicalCatalogDataJson()
    {
        using var doc = LoadCanonicalJson();
        var buildingsJson = doc.RootElement.GetProperty("buildingsByGid");

        var allBuildings = _catalog.GetAllBuildings();
        Assert.Equal(50, allBuildings.Length);

        for (int gid = 1; gid <= 50; gid++)
        {
            var expected = buildingsJson.GetProperty(gid.ToString());
            var actual = _catalog.GetBuilding(gid);

            Assert.Equal(gid, actual.BuildingTypeId);
            Assert.Equal(expected.GetProperty("name").GetString(), actual.Name);
            Assert.Equal(expected.GetProperty("maxLevel").GetInt32(), actual.MaxLevel);
            Assert.Equal(expected.GetProperty("nonCapitalMaxLevel").GetInt32(), actual.NonCapitalMaxLevel);
            Assert.Equal(expected.GetProperty("canFinishInstantlyWithGold").GetBoolean(), actual.CanFinishInstantlyWithGold);

            var levelsJson = expected.GetProperty("levels");
            if (gid <= 4)
            {
                // Level 0..20
                Assert.Equal(2.0, actual.GetLevelStats(0).AttributeValue, 4);
                for (int lvl = 1; lvl <= 20; lvl++)
                {
                    var lExpected = levelsJson[lvl];
                    var lActual = actual.GetLevelStats(lvl);
                    Assert.Equal(lExpected.GetProperty("wood").GetInt32(), lActual.Cost.Wood);
                    Assert.Equal(lExpected.GetProperty("clay").GetInt32(), lActual.Cost.Clay);
                    Assert.Equal(lExpected.GetProperty("iron").GetInt32(), lActual.Cost.Iron);
                    Assert.Equal(lExpected.GetProperty("crop").GetInt32(), lActual.Cost.Crop);
                    Assert.Equal(lExpected.GetProperty("pop").GetInt32(), lActual.Population);
                    Assert.Equal(lExpected.GetProperty("cp").GetInt32(), lActual.CulturePoints);
                    Assert.Equal(lExpected.GetProperty("time").GetInt32(), lActual.BaseTimeSeconds);
                    Assert.Equal(lExpected.GetProperty("prod").GetDouble(), lActual.AttributeValue, 4);
                }
            }
            else
            {
                for (int lvl = 1; lvl <= actual.MaxLevel; lvl++)
                {
                    var lExpected = levelsJson.GetProperty(lvl.ToString());
                    var lActual = actual.GetLevelStats(lvl);
                    Assert.Equal(lExpected.GetProperty("wood").GetInt32(), lActual.Cost.Wood);
                    Assert.Equal(lExpected.GetProperty("clay").GetInt32(), lActual.Cost.Clay);
                    Assert.Equal(lExpected.GetProperty("iron").GetInt32(), lActual.Cost.Iron);
                    Assert.Equal(lExpected.GetProperty("crop").GetInt32(), lActual.Cost.Crop);
                    Assert.Equal(lExpected.GetProperty("pop").GetInt32(), lActual.Population);
                    Assert.Equal(lExpected.GetProperty("cp").GetInt32(), lActual.CulturePoints);
                    Assert.Equal(lExpected.GetProperty("time").GetInt32(), lActual.BaseTimeSeconds);
                }
            }
        }
    }

    [Fact]
    public void SeparateBlacksmithAndArmoury_ShouldEnforceDistinctPrerequisites()
    {
        // Blacksmith (gid 12) requires Academy (22) >= 3 and Main Building (15) >= 3
        // Armoury (gid 13) requires Main Building (15) >= 3 and Academy (22) >= 1
        var academy1Context = CreateContext(tribeId: 1, existingBuildings: [(19, 15, 3), (20, 22, 1)]);
        Assert.False(_ruleCatalog.MeetsPrerequisites(12, in academy1Context));
        Assert.True(_ruleCatalog.MeetsPrerequisites(13, in academy1Context));

        var academy3Context = CreateContext(tribeId: 1, existingBuildings: [(19, 15, 3), (20, 22, 3)]);
        Assert.True(_ruleCatalog.MeetsPrerequisites(12, in academy3Context));
        Assert.True(_ruleCatalog.MeetsPrerequisites(13, in academy3Context));
    }

    [Fact]
    public void HunCommandCenter_ShouldReplaceResidenceAndPalace_AndUnlockStonemasonsLodge()
    {
        // Huns (Tribe 6) cannot build Residence (25) or Palace (26), even with MB 5 and Embassy 1
        var hunVillage = CreateContext(tribeId: 6, isCapital: true, existingBuildings: [(19, 15, 5), (20, 18, 1)]);
        Assert.False(_ruleCatalog.MeetsPrerequisites(25, in hunVillage));
        Assert.False(_ruleCatalog.MeetsPrerequisites(26, in hunVillage));
        Assert.True(_ruleCatalog.MeetsPrerequisites(44, in hunVillage)); // Command Center allowed

        // Stonemason's Lodge (gid 34) in Hun capital requires Command Center (44) >= 3 and MB (15) >= 5
        var hunWithCc2 = CreateContext(tribeId: 6, isCapital: true, existingBuildings: [(19, 15, 5), (20, 44, 2)]);
        Assert.False(_ruleCatalog.MeetsPrerequisites(34, in hunWithCc2));

        var hunWithCc3 = CreateContext(tribeId: 6, isCapital: true, existingBuildings: [(19, 15, 5), (20, 44, 3)]);
        Assert.True(_ruleCatalog.MeetsPrerequisites(34, in hunWithCc3));

        // Non-Huns cannot build Command Center (44)
        var romanVillage = CreateContext(tribeId: 1, isCapital: true, existingBuildings: [(19, 15, 5)]);
        Assert.False(_ruleCatalog.MeetsPrerequisites(44, in romanVillage));
        Assert.True(_ruleCatalog.MeetsPrerequisites(25, in romanVillage));
    }

    [Fact]
    public void SpartansAndVikings_ShouldBuildBigHospitalInsteadOfStandardHospital()
    {
        // Hospital (46) & Big Hospital (48) require MB (15) >= 10, Academy (22) >= 15
        var romanHospitalCtx = CreateContext(tribeId: 1, existingBuildings: [(19, 15, 10), (20, 22, 15)]);
        Assert.True(_ruleCatalog.MeetsPrerequisites(46, in romanHospitalCtx));
        Assert.False(_ruleCatalog.MeetsPrerequisites(48, in romanHospitalCtx));

        var spartanHospitalCtx = CreateContext(tribeId: 8, existingBuildings: [(19, 15, 10), (20, 22, 15)]);
        Assert.False(_ruleCatalog.MeetsPrerequisites(46, in spartanHospitalCtx));
        Assert.True(_ruleCatalog.MeetsPrerequisites(48, in spartanHospitalCtx));

        var vikingHospitalCtx = CreateContext(tribeId: 9, existingBuildings: [(19, 15, 10), (20, 22, 15)]);
        Assert.False(_ruleCatalog.MeetsPrerequisites(46, in vikingHospitalCtx));
        Assert.True(_ruleCatalog.MeetsPrerequisites(48, in vikingHospitalCtx));
    }

    [Fact]
    public void MultiInstanceBuildings_ShouldEnforceRebuildAtLevelThresholds()
    {
        // Warehouse (10) requires MB >= 1; second instance requires existing Warehouse == 20
        var wh19 = CreateContext(tribeId: 1, existingBuildings: [(19, 15, 1), (20, 10, 19)]);
        Assert.False(_ruleCatalog.MeetsPrerequisites(10, in wh19));

        var wh20 = CreateContext(tribeId: 1, existingBuildings: [(19, 15, 1), (20, 10, 20)]);
        Assert.True(_ruleCatalog.MeetsPrerequisites(10, in wh20));

        // Cranny (23) requires existing Cranny == 10 for second instance
        var cranny9 = CreateContext(tribeId: 3, existingBuildings: [(19, 23, 9)]);
        Assert.False(_ruleCatalog.MeetsPrerequisites(23, in cranny9));

        var cranny10 = CreateContext(tribeId: 3, existingBuildings: [(19, 23, 10)]);
        Assert.True(_ruleCatalog.MeetsPrerequisites(23, in cranny10));

        // Trapper (36, Gauls only) requires Rally Point >= 1; second instance requires existing Trapper == 20
        var trapper19 = CreateContext(tribeId: 3, existingBuildings: [(39, 16, 1), (19, 36, 19)]);
        Assert.False(_ruleCatalog.MeetsPrerequisites(36, in trapper19));

        var trapper20 = CreateContext(tribeId: 3, existingBuildings: [(39, 16, 1), (19, 36, 20)]);
        Assert.True(_ruleCatalog.MeetsPrerequisites(36, in trapper20));
    }

    [Fact]
    public void Palace_ShouldEnforceAccountWideUniqueness_AndMutualExclusionWithResidence()
    {
        var eligible = CreateContext(tribeId: 1, hasAccountPalace: false, existingBuildings: [(19, 15, 5), (20, 18, 1)]);
        Assert.True(_ruleCatalog.MeetsPrerequisites(26, in eligible));

        var alreadyHasAccountPalace = CreateContext(tribeId: 1, hasAccountPalace: true, existingBuildings: [(19, 15, 5), (20, 18, 1)]);
        Assert.False(_ruleCatalog.MeetsPrerequisites(26, in alreadyHasAccountPalace));

        var hasResidenceInVillage = CreateContext(tribeId: 1, hasAccountPalace: false, existingBuildings: [(19, 15, 5), (20, 18, 1), (21, 25, 1)]);
        Assert.False(_ruleCatalog.MeetsPrerequisites(26, in hasResidenceInVillage));
    }

    [Fact]
    public void WorldWonder_ShouldRequireSecondAlliancePlan_WhenCurrentOrQueuedLevelReaches50()
    {
        // Level 48 with 1 queued upgrade (maxQueued = 49): 1 owned plan suffices
        var ww49 = CreateContext(
            tribeId: 1,
            isWorldWonderVillage: true,
            ownedWwPlanCount: 1,
            allianceWwPlanCount: 1,
            worldWonderLevel: 48,
            maxQueuedWorldWonderLevel: 49);
        Assert.True(_ruleCatalog.MeetsPrerequisites(40, in ww49));

        // Level 48 with 2 queued upgrades (maxQueued = 50): requires 2nd alliance plan
        var ww50SinglePlan = CreateContext(
            tribeId: 1,
            isWorldWonderVillage: true,
            ownedWwPlanCount: 1,
            allianceWwPlanCount: 1,
            worldWonderLevel: 48,
            maxQueuedWorldWonderLevel: 50);
        Assert.False(_ruleCatalog.MeetsPrerequisites(40, in ww50SinglePlan));

        var ww50DualPlan = CreateContext(
            tribeId: 1,
            isWorldWonderVillage: true,
            ownedWwPlanCount: 1,
            allianceWwPlanCount: 2,
            worldWonderLevel: 48,
            maxQueuedWorldWonderLevel: 50);
        Assert.True(_ruleCatalog.MeetsPrerequisites(40, in ww50DualPlan));
    }

    [Fact]
    public void CapitalVsNonCapitalRestrictions_AndUnitResearchPrerequisites_ShouldMatchTravianZ()
    {
        // Great Barracks (29) and Great Stable (30) prohibited in Capital
        var capWithBarracks20 = CreateContext(tribeId: 1, isCapital: true, existingBuildings: [(19, 19, 20)]);
        Assert.False(_ruleCatalog.MeetsPrerequisites(29, in capWithBarracks20));

        var nonCapWithBarracks20 = CreateContext(tribeId: 1, isCapital: false, existingBuildings: [(19, 19, 20)]);
        Assert.True(_ruleCatalog.MeetsPrerequisites(29, in nonCapWithBarracks20));

        // Brewery (35, Teutons) allowed only in Capital
        var teutonNonCap = CreateContext(tribeId: 2, isCapital: false, existingBuildings: [(19, 11, 20), (39, 16, 10)]);
        Assert.False(_ruleCatalog.MeetsPrerequisites(35, in teutonNonCap));

        var teutonCap = CreateContext(tribeId: 2, isCapital: true, existingBuildings: [(19, 11, 20), (39, 16, 10)]);
        Assert.True(_ruleCatalog.MeetsPrerequisites(35, in teutonCap));

        // Unit research prerequisites check (e.g. u52 Scout Rider requires Academy 1 + MB 5; u74 Perioikoi Scout requires Academy 1 + Blacksmith 1)
        var slots = new int[41];
        var levels = new int[41];
        slots[19] = 22; levels[19] = 1; // Academy 1
        slots[20] = 15; levels[20] = 5; // Main Building 5
        Assert.True(_ruleCatalog.MeetsUnitResearchPrerequisites(52, slots, levels));
        Assert.False(_ruleCatalog.MeetsUnitResearchPrerequisites(74, slots, levels));

        slots[21] = 12; levels[21] = 1; // Blacksmith 1
        Assert.True(_ruleCatalog.MeetsUnitResearchPrerequisites(74, slots, levels));
    }
}
