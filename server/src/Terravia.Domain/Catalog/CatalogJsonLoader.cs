using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Terravia.Domain.Catalog;

/// <summary>
/// Loads the canonical catalog-data.json embedded resource (or file stream) and computes its deterministic SHA-256 hash.
/// </summary>
public static class CatalogJsonLoader
{
    public const string EmbeddedResourceName = "Terravia.Domain.Catalog.catalog-data.json";

    public static LoadedCatalogData LoadFromEmbeddedResource()
    {
        var assembly = typeof(CatalogJsonLoader).GetTypeInfo().Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{EmbeddedResourceName}' was not found in assembly '{assembly.FullName}'.");
        return LoadFromStream(stream);
    }

    public static LoadedCatalogData LoadFromFile(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return LoadFromStream(stream);
    }

    public static LoadedCatalogData LoadFromStream(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var bytes = ms.ToArray();

        // Compute deterministic SHA-256 content hash over normalized LF bytes
        var rawText = Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n");
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawText));
        var contentHash = $"sha256:{Convert.ToHexStringLower(hashBytes)}";

        using var doc = JsonDocument.Parse(bytes);
        var root = doc.RootElement;
        var schemaVersion = root.GetProperty("schemaVersion").GetString() ?? "1.0.0";

        // 1. Parse unitClassifications
        var classifications = root.GetProperty("unitClassifications");
        var infantrySet = ReadIntSet(classifications, "infantry");
        var cavalrySet = ReadIntSet(classifications, "cavalry");
        var siegeSet = ReadIntSet(classifications, "siege");
        var ramSet = ReadIntSet(classifications, "ram");
        var catapultSet = ReadIntSet(classifications, "catapult");
        var expansionSet = ReadIntSet(classifications, "expansion");
        var scoutSet = ReadIntSet(classifications, "scout");
        var chiefSet = ReadIntSet(classifications, "chief");

        // 2. Parse tribesById (1..9)
        var tribesElement = root.GetProperty("tribesById");
        var tribesBuilder = ImmutableArray.CreateBuilder<TribeDefinition>(9);
        for (int tid = 1; tid <= 9; tid++)
        {
            var tEl = tribesElement.GetProperty(tid.ToString());
            var id = tEl.GetProperty("id").GetInt32();
            var name = tEl.GetProperty("name").GetString()!;
            var playable = tEl.GetProperty("playable").GetBoolean();
            var merchantCap = tEl.GetProperty("merchantCapacity").GetInt32();
            var merchantSpeed = tEl.GetProperty("merchantSpeed").GetInt32();
            var crannyMultiplier = tEl.TryGetProperty("crannyMultiplier", out var cmEl) ? cmEl.GetDouble() : 1.0;
            var wallGid = tEl.GetProperty("wallGid").ValueKind == JsonValueKind.Null
                ? 0
                : tEl.GetProperty("wallGid").GetInt32();
            var wallFactor = tEl.GetProperty("wallFactor").GetDouble();
            var dualBuild = tEl.GetProperty("dualBuildQueue").GetBoolean();
            var exclusiveGids = ReadIntImmutableArray(tEl.GetProperty("exclusiveBuildingGids"));

            var canBuildResOrPalace = tid != 6;
            var commandBuildingIds = tid == 6
                ? ImmutableArray.Create(44)
                : ImmutableArray.Create(25, 26);
            var hospitalGid = tid is 8 or 9 ? 48 : 46;

            var unitIdsBuilder = ImmutableArray.CreateBuilder<int>(10);
            for (int slot = 1; slot <= 10; slot++)
            {
                unitIdsBuilder.Add((tid - 1) * 10 + slot);
            }

            tribesBuilder.Add(new TribeDefinition(
                tribeId: id,
                name: name,
                isPlayable: playable,
                merchantCapacity: merchantCap,
                merchantSpeed: merchantSpeed,
                crannyMultiplier: crannyMultiplier,
                wallBuildingTypeId: wallGid,
                wallFactor: wallFactor,
                hasDualBuildQueue: dualBuild,
                canBuildResidenceOrPalace: canBuildResOrPalace,
                commandBuildingTypeIds: commandBuildingIds,
                hospitalBuildingTypeId: hospitalGid,
                exclusiveBuildingTypeIds: exclusiveGids,
                unitIds: unitIdsBuilder.MoveToImmutable()));
        }

        // 3. Parse academyResearchByUnitId & smithyUpgradesByUnitId
        var researchEl = root.GetProperty("academyResearchByUnitId");
        var smithyEl = root.GetProperty("smithyUpgradesByUnitId");

        // 4. Parse unitsById (1..90, 99)
        var unitsEl = root.GetProperty("unitsById");
        var unitsBuilder = ImmutableArray.CreateBuilder<UnitDefinition>(91);
        foreach (var prop in unitsEl.EnumerateObject())
        {
            var uEl = prop.Value;
            int uid = uEl.GetProperty("id").GetInt32();
            int tribeId = uEl.GetProperty("tribeId").GetInt32();
            int slot = uEl.TryGetProperty("slot", out var slotEl)
                ? slotEl.GetInt32()
                : (uid == 99 ? 0 : ((uid - 1) % 10) + 1);
            string name = uEl.GetProperty("name").GetString()!;

            // Determine category & tactical role flags
            UnitCategory category;
            UnitTacticalRole roles = UnitTacticalRole.None;
            if (uid == 99)
            {
                category = UnitCategory.Trap;
                roles = UnitTacticalRole.Trap;
            }
            else
            {
                if (infantrySet.Contains(uid)) roles |= UnitTacticalRole.Infantry;
                if (cavalrySet.Contains(uid)) roles |= UnitTacticalRole.Cavalry;
                if (scoutSet.Contains(uid)) roles |= UnitTacticalRole.Scout;
                if (siegeSet.Contains(uid)) roles |= UnitTacticalRole.Siege;
                if (ramSet.Contains(uid)) roles |= UnitTacticalRole.Ram;
                if (catapultSet.Contains(uid)) roles |= UnitTacticalRole.Catapult;
                if (expansionSet.Contains(uid)) roles |= UnitTacticalRole.Expansion;
                if (chiefSet.Contains(uid)) roles |= UnitTacticalRole.Chief;
                if (expansionSet.Contains(uid) && !chiefSet.Contains(uid) && slot == 10)
                    roles |= UnitTacticalRole.Settler;

                if (infantrySet.Contains(uid)) category = UnitCategory.Infantry;
                else if (cavalrySet.Contains(uid)) category = UnitCategory.Cavalry;
                else if (siegeSet.Contains(uid)) category = UnitCategory.Siege;
                else category = UnitCategory.Expansion;
            }

            var allowedTrainingBuildings = uEl.TryGetProperty("trainingBuildingGids", out var tbgEl)
                ? ReadIntImmutableArray(tbgEl)
                : ImmutableArray<int>.Empty;
            int primaryTrainingBuildingId = allowedTrainingBuildings.Length > 0 ? allowedTrainingBuildings[0] : 0;
            int greatTrainingBuildingId = uEl.TryGetProperty("greatTrainingBuildingGid", out var gtEl) && gtEl.ValueKind == JsonValueKind.Number
                ? gtEl.GetInt32()
                : 0;

            var cost = new ResourceCost(
                uEl.GetProperty("wood").GetInt32(),
                uEl.GetProperty("clay").GetInt32(),
                uEl.GetProperty("iron").GetInt32(),
                uEl.GetProperty("crop").GetInt32());

            int? hdtLevel = uEl.TryGetProperty("drinking", out var drEl) && drEl.ValueKind == JsonValueKind.Number
                ? drEl.GetInt32()
                : null;

            var researchPrereqs = uEl.TryGetProperty("researchPrerequisites", out var rpEl)
                ? ReadBuildingRequirements(rpEl)
                : ImmutableArray<BuildingLevelRequirement>.Empty;

            ResourceCost? researchCost = null;
            int baseResearchSeconds = 0;
            if (researchEl.TryGetProperty(uid.ToString(), out var rItem))
            {
                researchCost = new ResourceCost(
                    rItem.GetProperty("wood").GetInt32(),
                    rItem.GetProperty("clay").GetInt32(),
                    rItem.GetProperty("iron").GetInt32(),
                    rItem.GetProperty("crop").GetInt32());
                baseResearchSeconds = rItem.GetProperty("time").GetInt32();
            }

            var smithyList = ImmutableArray<SmithyUpgradeLevelStats>.Empty;
            if (smithyEl.TryGetProperty(uid.ToString(), out var sItem))
            {
                var sb = ImmutableArray.CreateBuilder<SmithyUpgradeLevelStats>(20);
                for (int lvl = 1; lvl <= 20; lvl++)
                {
                    var sRow = sItem.GetProperty(lvl.ToString());
                    sb.Add(new SmithyUpgradeLevelStats(
                        lvl,
                        new ResourceCost(
                            sRow.GetProperty("wood").GetInt32(),
                            sRow.GetProperty("clay").GetInt32(),
                            sRow.GetProperty("iron").GetInt32(),
                            sRow.GetProperty("crop").GetInt32()),
                        sRow.GetProperty("time").GetInt32()));
                }
                smithyList = sb.MoveToImmutable();
            }

            unitsBuilder.Add(new UnitDefinition(
                unitTypeId: uid,
                tribeId: tribeId,
                tribeUnitSlot: slot,
                name: name,
                category: category,
                tacticalRole: roles,
                trainingBuildingTypeId: primaryTrainingBuildingId,
                allowedTrainingBuildingTypeIds: allowedTrainingBuildings,
                alternateGreatTrainingBuildingTypeId: greatTrainingBuildingId,
                offense: uEl.GetProperty("atk").GetInt32(),
                defenseInfantry: uEl.GetProperty("di").GetInt32(),
                defenseCavalry: uEl.GetProperty("dc").GetInt32(),
                cost: cost,
                upkeepCropPerHour: uEl.GetProperty("pop").GetInt32(),
                speedTilesPerHour: uEl.GetProperty("speed").GetInt32(),
                baseTrainingSeconds: uEl.GetProperty("time").GetInt32(),
                carryCapacity: uEl.GetProperty("cap").GetInt32(),
                horseDrinkingTroughThresholdLevel: hdtLevel,
                researchPrerequisites: researchPrereqs,
                researchCost: researchCost,
                baseResearchSeconds: baseResearchSeconds,
                smithyUpgrades: smithyList));
        }

        // 5. Parse heroExperienceThresholds & heroScalingTemplatesByUnitId + heroReviveTablesByUnitId
        var heroXpEl = root.GetProperty("heroExperienceThresholds");
        var heroXpThresholds = ReadIntImmutableArray(heroXpEl);

        var heroTemplatesEl = root.GetProperty("heroScalingTemplatesByUnitId");
        var heroReviveEl = root.GetProperty("heroReviveTablesByUnitId");
        var heroBuilder = ImmutableArray.CreateBuilder<HeroUnitTemplate>(35);
        foreach (var prop in heroTemplatesEl.EnumerateObject())
        {
            int uid = int.Parse(prop.Name);
            var hEl = prop.Value;
            var reviveBuilder = ImmutableArray.CreateBuilder<HeroReviveLevelStats>();
            if (heroReviveEl.TryGetProperty(prop.Name, out var revArr) && revArr.ValueKind == JsonValueKind.Array)
            {
                int lvl = 0;
                foreach (var revRow in revArr.EnumerateArray())
                {
                    reviveBuilder.Add(new HeroReviveLevelStats(
                        lvl++,
                        new ResourceCost(
                            revRow.TryGetProperty("wood", out var rw) ? rw.GetInt32() : 0,
                            revRow.TryGetProperty("clay", out var rcl) ? rcl.GetInt32() : 0,
                            revRow.TryGetProperty("iron", out var ri) ? ri.GetInt32() : 0,
                            revRow.TryGetProperty("crop", out var rcr) ? rcr.GetInt32() : 0),
                        revRow.GetProperty("time").GetInt32()));
                }
            }

            heroBuilder.Add(new HeroUnitTemplate(
                unitTypeId: uid,
                baseAttack: hEl.GetProperty("atk").GetDouble(),
                attackPerPoint: hEl.GetProperty("atkp").GetDouble(),
                baseDefenseInfantry: hEl.GetProperty("di").GetDouble(),
                defenseInfantryPerPoint: hEl.GetProperty("dip").GetDouble(),
                baseDefenseCavalry: hEl.GetProperty("dc").GetDouble(),
                defenseCavalryPerPoint: hEl.GetProperty("dcp").GetDouble(),
                reviveTable: reviveBuilder.ToImmutable()));
        }

        // 6. Parse buildingsByGid (1..50)
        var buildingsEl = root.GetProperty("buildingsByGid");
        var buildingsBuilder = ImmutableArray.CreateBuilder<BuildingDefinition>(50);
        for (int gid = 1; gid <= 50; gid++)
        {
            var bEl = buildingsEl.GetProperty(gid.ToString());
            string bName = bEl.GetProperty("name").GetString()!;
            var category = Enum.Parse<BuildingCategory>(bEl.GetProperty("category").GetString()!, ignoreCase: true);
            var slotPlacement = Enum.Parse<SlotPlacementRule>(bEl.GetProperty("slotPlacement").GetString()!, ignoreCase: true);
            int maxLevel = bEl.GetProperty("maxLevel").GetInt32();
            int nonCapMaxLevel = bEl.GetProperty("nonCapitalMaxLevel").GetInt32();
            bool canFinishWithGold = bEl.GetProperty("canFinishInstantlyWithGold").GetBoolean();

            var reqEl = bEl.GetProperty("requirements");
            var minLevels = ReadBuildingRequirements(reqEl.GetProperty("minLevels"));
            var exactLevels = ReadBuildingRequirements(reqEl.GetProperty("exactLevels"));
            var anyOfMinLevels = ReadBuildingRequirements(reqEl.GetProperty("anyOfMinLevels"));
            var allowedTribes = ReadIntImmutableArray(reqEl.GetProperty("allowedTribes"));
            var deniedTribes = ReadIntImmutableArray(reqEl.GetProperty("deniedTribes"));
            bool capitalOnly = reqEl.GetProperty("capitalOnly").GetBoolean();
            bool notCapital = reqEl.GetProperty("notCapital").GetBoolean();
            bool unique = reqEl.GetProperty("unique").GetBoolean();
            int? rebuildAtLevel = reqEl.GetProperty("rebuildAtLevel").ValueKind == JsonValueKind.Number
                ? reqEl.GetProperty("rebuildAtLevel").GetInt32()
                : null;
            bool accountWideUnique = reqEl.GetProperty("accountWideUnique").GetBoolean();
            bool reqGreatStorage = reqEl.GetProperty("requiresGreatStorageQualification").GetBoolean();
            bool reqWwPlan = reqEl.GetProperty("requiresWorldWonderPlan").GetBoolean();
            bool reqGreatWorkshopConfig = reqEl.GetProperty("requiresRoundConfigGreatWorkshop").GetBoolean();

            var capitalConstraint = capitalOnly
                ? CapitalConstraint.CapitalOnly
                : (notCapital ? CapitalConstraint.NonCapitalOnly : CapitalConstraint.Any);

            var levelsEl = bEl.GetProperty("levels");
            var levelsBuilder = ImmutableArray.CreateBuilder<BuildingLevelStats>();
            if (levelsEl.ValueKind == JsonValueKind.Array)
            {
                // gid 1..4: 0..20
                int idx = 0;
                foreach (var lRow in levelsEl.EnumerateArray())
                {
                    if (idx == 0)
                    {
                        double prod0 = lRow.GetProperty("prod").GetDouble();
                        levelsBuilder.Add(new BuildingLevelStats(0, ResourceCost.Zero, 0, 0, 0, prod0));
                    }
                    else
                    {
                        levelsBuilder.Add(ParseBuildingLevelRow(idx, lRow));
                    }
                    idx++;
                }
            }
            else
            {
                for (int lvl = 1; lvl <= maxLevel; lvl++)
                {
                    var lRow = levelsEl.GetProperty(lvl.ToString());
                    levelsBuilder.Add(ParseBuildingLevelRow(lvl, lRow));
                }
            }

            buildingsBuilder.Add(new BuildingDefinition(
                buildingTypeId: gid,
                name: bName,
                category: category,
                slotPlacement: slotPlacement,
                maxLevel: maxLevel,
                nonCapitalMaxLevel: nonCapMaxLevel,
                capitalConstraint: capitalConstraint,
                allowedTribeIds: allowedTribes,
                deniedTribeIds: deniedTribes,
                minLevelRequirements: minLevels,
                exactLevelRequirements: exactLevels,
                anyOfMinLevelRequirements: anyOfMinLevels,
                isUniquePerVillage: unique,
                rebuildAtLevel: rebuildAtLevel,
                isAccountWideUnique: accountWideUnique,
                requiresGreatStorageQualification: reqGreatStorage,
                requiresWorldWonderPlan: reqWwPlan,
                requiresRoundConfigGreatWorkshop: reqGreatWorkshopConfig,
                canFinishInstantlyWithGold: canFinishWithGold,
                levels: levelsBuilder.ToImmutable()));
        }

        return new LoadedCatalogData(
            SchemaVersion: schemaVersion,
            ContentHash: contentHash,
            Tribes: tribesBuilder.MoveToImmutable(),
            Units: unitsBuilder.ToImmutable(),
            HeroTemplates: heroBuilder.ToImmutable(),
            HeroExperienceThresholds: heroXpThresholds,
            Buildings: buildingsBuilder.MoveToImmutable());
    }

    private static BuildingLevelStats ParseBuildingLevelRow(int level, JsonElement row)
    {
        var cost = new ResourceCost(
            row.GetProperty("wood").GetInt32(),
            row.GetProperty("clay").GetInt32(),
            row.GetProperty("iron").GetInt32(),
            row.GetProperty("crop").GetInt32());
        int pop = row.GetProperty("pop").GetInt32();
        int cp = row.GetProperty("cp").GetInt32();
        int time = row.GetProperty("time").GetInt32();
        double attr = row.TryGetProperty("prod", out var prodEl)
            ? prodEl.GetDouble()
            : (row.TryGetProperty("attri", out var attriEl) ? attriEl.GetDouble() : 0.0);
        double? trn = row.TryGetProperty("trn", out var trnEl) && trnEl.ValueKind == JsonValueKind.Number
            ? trnEl.GetDouble()
            : null;
        return new BuildingLevelStats(level, cost, pop, cp, time, attr, trn);
    }

    private static HashSet<int> ReadIntSet(JsonElement parent, string propName)
    {
        var set = new HashSet<int>();
        if (parent.TryGetProperty(propName, out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
                set.Add(item.GetInt32());
        }
        return set;
    }

    private static ImmutableArray<int> ReadIntImmutableArray(JsonElement arr)
    {
        if (arr.ValueKind != JsonValueKind.Array)
            return ImmutableArray<int>.Empty;
        var builder = ImmutableArray.CreateBuilder<int>(arr.GetArrayLength());
        foreach (var item in arr.EnumerateArray())
            builder.Add(item.GetInt32());
        return builder.MoveToImmutable();
    }

    private static ImmutableArray<BuildingLevelRequirement> ReadBuildingRequirements(JsonElement arr)
    {
        if (arr.ValueKind != JsonValueKind.Array)
            return ImmutableArray<BuildingLevelRequirement>.Empty;
        var builder = ImmutableArray.CreateBuilder<BuildingLevelRequirement>(arr.GetArrayLength());
        foreach (var item in arr.EnumerateArray())
        {
            builder.Add(new BuildingLevelRequirement(
                item.GetProperty("gid").GetInt32(),
                item.GetProperty("level").GetInt32()));
        }
        return builder.MoveToImmutable();
    }
}
