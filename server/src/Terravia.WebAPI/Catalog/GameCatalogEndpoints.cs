using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using Terravia.Domain.Catalog;

namespace Terravia.WebAPI.Catalog;

public static class GameCatalogEndpoints
{
    public static IEndpointRouteBuilder MapGameCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/game/catalog", HandleGetCatalog)
           .WithName("GetGameCatalog");

        return app;
    }

    public static IResult HandleGetCatalog(HttpContext httpContext, IGameCatalog catalog)
    {
        string etag = $"\"{catalog.ContentHash}\"";

        httpContext.Response.Headers[HeaderNames.ETag] = etag;
        httpContext.Response.Headers[HeaderNames.CacheControl] = "public, max-age=3600, must-revalidate";

        if (httpContext.Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var ifNoneMatch))
        {
            foreach (var candidate in ifNoneMatch)
            {
                if (string.Equals(candidate?.Trim(), etag, StringComparison.Ordinal) ||
                    string.Equals(candidate?.Trim(), catalog.ContentHash, StringComparison.Ordinal))
                {
                    return Results.StatusCode(StatusCodes.Status304NotModified);
                }
            }
        }

        var tribes = new List<object>(9);
        foreach (var t in catalog.GetAllTribes())
        {
            tribes.Add(new
            {
                tribeId = t.TribeId,
                name = t.Name,
                isPlayable = t.IsPlayable,
                merchantCapacity = t.MerchantCapacity,
                merchantSpeed = t.MerchantSpeed,
                crannyMultiplier = t.CrannyMultiplier,
                wallBuildingTypeId = t.WallBuildingTypeId,
                wallFactor = t.WallFactor,
                hasDualBuildQueue = t.HasDualBuildQueue,
                canBuildResidenceOrPalace = t.CanBuildResidenceOrPalace,
                commandBuildingTypeIds = t.CommandBuildingTypeIds,
                hospitalBuildingTypeId = t.HospitalBuildingTypeId,
                unitIds = t.UnitIds
            });
        }

        var units = new List<object>(91);
        foreach (var u in catalog.GetAllUnits())
        {
            units.Add(new
            {
                unitTypeId = u.UnitTypeId,
                tribeId = u.TribeId,
                tribeUnitSlot = u.TribeUnitSlot,
                name = u.Name,
                category = u.Category.ToString(),
                tacticalRoles = ExtractTacticalRoles(u.TacticalRole),
                trainingBuildingTypeId = u.TrainingBuildingTypeId,
                alternateGreatTrainingBuildingTypeId = u.AlternateGreatTrainingBuildingTypeId,
                offense = u.Offense,
                defenseInfantry = u.DefenseInfantry,
                defenseCavalry = u.DefenseCavalry,
                cost = new
                {
                    wood = u.Cost.Wood,
                    clay = u.Cost.Clay,
                    iron = u.Cost.Iron,
                    crop = u.Cost.Crop
                },
                upkeepCropPerHour = u.UpkeepCropPerHour,
                speedTilesPerHour = u.SpeedTilesPerHour,
                baseTrainingSeconds = u.BaseTrainingSeconds,
                carryCapacity = u.CarryCapacity,
                horseDrinkingTroughThresholdLevel = u.HorseDrinkingTroughThresholdLevel,
                researchPrerequisites = u.ResearchPrerequisites.Select(r => new
                {
                    buildingTypeId = r.BuildingTypeId,
                    level = r.Level
                })
            });
        }

        var heroTemplates = new List<object>(35);
        foreach (var h in catalog.GetAllHeroTemplates())
        {
            heroTemplates.Add(new
            {
                unitTypeId = h.UnitTypeId,
                baseAttack = h.BaseAttack,
                attackPerPoint = h.AttackPerPoint,
                baseDefenseInfantry = h.BaseDefenseInfantry,
                defenseInfantryPerPoint = h.DefenseInfantryPerPoint,
                baseDefenseCavalry = h.BaseDefenseCavalry,
                defenseCavalryPerPoint = h.DefenseCavalryPerPoint
            });
        }

        var buildings = new List<object>(50);
        foreach (var b in catalog.GetAllBuildings())
        {
            var levelDtos = new List<object>(b.Levels.Length);
            foreach (var l in b.Levels)
            {
                if (l.Level == 0)
                    continue;
                levelDtos.Add(new
                {
                    level = l.Level,
                    cost = new
                    {
                        wood = l.Cost.Wood,
                        clay = l.Cost.Clay,
                        iron = l.Cost.Iron,
                        crop = l.Cost.Crop
                    },
                    population = l.Population,
                    culturePoints = l.CulturePoints,
                    baseTimeSeconds = l.BaseTimeSeconds,
                    attributeValue = l.AttributeValue
                });
            }

            buildings.Add(new
            {
                buildingTypeId = b.BuildingTypeId,
                name = b.Name,
                category = b.Category.ToString(),
                slotPlacement = b.SlotPlacement.ToString(),
                maxLevel = b.MaxLevel,
                nonCapitalMaxLevel = b.NonCapitalMaxLevel,
                capitalConstraint = b.CapitalConstraint.ToString(),
                allowedTribeIds = b.AllowedTribeIds,
                deniedTribeIds = b.DeniedTribeIds,
                minLevelRequirements = b.MinLevelRequirements.Select(r => new
                {
                    buildingTypeId = r.BuildingTypeId,
                    level = r.Level
                }),
                exactLevelRequirements = b.ExactLevelRequirements.Select(r => new
                {
                    buildingTypeId = r.BuildingTypeId,
                    level = r.Level
                }),
                isUniquePerVillage = b.IsUniquePerVillage,
                rebuildAtLevel = b.RebuildAtLevel,
                isAccountWideUnique = b.IsAccountWideUnique,
                requiresGreatStorageQualification = b.RequiresGreatStorageQualification,
                canFinishInstantlyWithGold = b.CanFinishInstantlyWithGold,
                levels = levelDtos
            });
        }

        var payload = new
        {
            version = catalog.SchemaVersion,
            contentHash = catalog.ContentHash,
            serverSpeed = 1.0,
            storageMultiplier = 1.0,
            allowAllTribeRomanQueue = false,
            greatWorkshopEnabled = true,
            maxMasterBuilderSlots = 3,
            tribes,
            units,
            heroTemplates,
            buildings
        };

        return Results.Ok(payload);
    }

    private static IReadOnlyList<string> ExtractTacticalRoles(UnitTacticalRole roles)
    {
        var list = new List<string>(3);
        foreach (var flag in Enum.GetValues<UnitTacticalRole>())
        {
            if (flag != UnitTacticalRole.None && roles.HasFlag(flag))
            {
                list.Add(flag.ToString());
            }
        }
        return list;
    }
}
