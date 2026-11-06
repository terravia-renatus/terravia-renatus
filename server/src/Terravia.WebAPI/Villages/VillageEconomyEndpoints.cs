using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Terravia.Domain;
using Terravia.Domain.Oases;
using Terravia.Domain.Villages;

namespace Terravia.WebAPI.Villages;

/// <summary>
/// Minimal API and SignalR command gateway bindings for village economy, celebrations, Mead Festivals,
/// Capital village transfer, and oasis abandonment with Sitter permission enforcement.
/// </summary>
public static class VillageEconomyEndpoints
{
    public sealed record StartCelebrationInput(
        int VillageId,
        string CelebrationType,
        SitterPermissions? SitterPermissions = null);

    public sealed record StartMeadFestivalInput(
        int VillageId,
        SitterPermissions? SitterPermissions = null);

    public sealed record ChangeCapitalInput(
        int NewCapitalVillageId,
        SitterPermissions? SitterPermissions = null);

    public sealed record AbandonOasisInput(
        int VillageId,
        int OasisTileId,
        SitterPermissions? SitterPermissions = null);

    public static IEndpointRouteBuilder MapVillageEconomyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/game/economy");

        group.MapGet("/topologies", () => Results.Ok(VillageFieldTopologyCatalog.Default.GetAllTopologies()))
             .WithName("GetVillageFieldTopologies");

        group.MapGet("/oases/types", () => Results.Ok(OasisEcologyCatalog.Default.GetAllOasisTypes()))
             .WithName("GetOasisTypes");

        group.MapPost("/celebrations/start", (StartCelebrationInput input) =>
        {
            if (input.SitterPermissions.HasValue &&
                !input.SitterPermissions.Value.HasFlag(SitterPermissions.BuildAndDemolish))
            {
                return Results.BadRequest(CommandResult.Fail(
                    TerraviaDomainErrorCodes.Sitters.PermissionDenied,
                    "Sitter lacks BuildAndDemolish permission to start celebrations."));
            }

            if (!Enum.TryParse<CelebrationType>(input.CelebrationType, ignoreCase: true, out var celType) ||
                celType == CelebrationType.None)
            {
                return Results.BadRequest(CommandResult.Fail(
                    TerraviaDomainErrorCodes.Villages.InsufficientTownHallLevel,
                    "CelebrationType must be 'Small' or 'Great'."));
            }

            var cmd = new StartTownHallCelebrationCommand(
                ActorPlayerId: 1,
                OwnerPlayerId: 1,
                VillageId: input.VillageId,
                CelebrationType: celType);

            return Results.Ok(cmd);
        }).WithName("StartCelebration");

        group.MapPost("/mead-festival/start", (StartMeadFestivalInput input) =>
        {
            if (input.SitterPermissions.HasValue &&
                !input.SitterPermissions.Value.HasFlag(SitterPermissions.BuildAndDemolish))
            {
                return Results.BadRequest(CommandResult.Fail(
                    TerraviaDomainErrorCodes.Sitters.PermissionDenied,
                    "Sitter lacks BuildAndDemolish permission to start Mead Festivals."));
            }

            var cmd = new StartBreweryMeadFestivalCommand(
                ActorPlayerId: 1,
                OwnerPlayerId: 1,
                VillageId: input.VillageId);

            return Results.Ok(cmd);
        }).WithName("StartMeadFestival");

        group.MapPost("/capital/change", (ChangeCapitalInput input) =>
        {
            if (input.SitterPermissions.HasValue)
            {
                return Results.BadRequest(CommandResult.Fail(
                    TerraviaDomainErrorCodes.Sitters.PermissionDenied,
                    "Sitters are never permitted to transfer a player's Capital village."));
            }

            var cmd = new ChangeCapitalVillageCommand(
                ActorPlayerId: 1,
                OwnerPlayerId: 1,
                NewCapitalVillageId: input.NewCapitalVillageId);

            return Results.Ok(cmd);
        }).WithName("ChangeCapitalVillage");

        group.MapPost("/oases/abandon", (AbandonOasisInput input) =>
        {
            if (input.SitterPermissions.HasValue)
            {
                return Results.BadRequest(CommandResult.Fail(
                    TerraviaDomainErrorCodes.Sitters.PermissionDenied,
                    "Sitters are not permitted to abandon annexed oases."));
            }

            var cmd = new AbandonAnnexedOasisCommand(
                ActorPlayerId: 1,
                OwnerPlayerId: 1,
                VillageId: input.VillageId,
                OasisTileId: input.OasisTileId);

            return Results.Ok(cmd);
        }).WithName("AbandonOasis");

        return app;
    }
}
