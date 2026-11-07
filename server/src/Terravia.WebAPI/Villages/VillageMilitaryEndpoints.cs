using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Terravia.Domain;
using Terravia.Domain.Villages;

namespace Terravia.WebAPI.Villages;

/// <summary>
/// Minimal API and SignalR command gateway bindings for military recruitment, Hospital healing,
/// Academy unit discovery, Blacksmith/Armoury upgrades, 2-Gold instant research completion,
/// and Gaul Trapper prisoner release/dissolution with Sitter permission enforcement.
/// </summary>
public static class VillageMilitaryEndpoints
{
    public sealed record EnqueueTrainingInput(
        TrainingFacilityCategory FacilityCategory,
        int UnitTypeId,
        int RequestedAmount,
        int? SecondExpansionUnitTypeId = null,
        int SecondExpansionRequestedAmount = 0,
        SitterPermissions? SitterPermissions = null);

    public sealed record EnqueueHealingInput(
        int UnitTypeId,
        int RequestedAmount,
        SitterPermissions? SitterPermissions = null);

    public sealed record StartAcademyInput(
        int UnitTypeId,
        SitterPermissions? SitterPermissions = null);

    public sealed record StartSmithyInput(
        ResearchCategory Category,
        int UnitTypeId,
        SitterPermissions? SitterPermissions = null);

    public sealed record SitterGateInput(
        int? OwnerPlayerId = null,
        SitterPermissions? SitterPermissions = null);

    public static IEndpointRouteBuilder MapVillageMilitaryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/villages/{villageId:int}");

        group.MapPost("/training", (int villageId, EnqueueTrainingInput input) =>
        {
            if (input.SitterPermissions.HasValue &&
                !input.SitterPermissions.Value.HasFlag(SitterPermissions.BuildAndDemolish))
            {
                return Results.BadRequest(CommandResult.Fail(
                    TerraviaDomainErrorCodes.Sitters.PermissionDenied,
                    "Sitter lacks BuildAndDemolish permission to train military units or traps."));
            }

            var cmd = new EnqueueMilitaryTrainingCommand(
                VillageId: villageId,
                FacilityCategory: input.FacilityCategory,
                UnitTypeId: input.UnitTypeId,
                RequestedAmount: input.RequestedAmount,
                SecondExpansionUnitTypeId: input.SecondExpansionUnitTypeId,
                SecondExpansionRequestedAmount: input.SecondExpansionRequestedAmount);

            return Results.Ok(cmd);
        }).WithName("EnqueueMilitaryTraining");

        group.MapPost("/hospital/heal", (int villageId, EnqueueHealingInput input) =>
        {
            if (input.SitterPermissions.HasValue &&
                !input.SitterPermissions.Value.HasFlag(SitterPermissions.BuildAndDemolish))
            {
                return Results.BadRequest(CommandResult.Fail(
                    TerraviaDomainErrorCodes.Sitters.PermissionDenied,
                    "Sitter lacks BuildAndDemolish permission to heal wounded units."));
            }

            var cmd = new EnqueueHospitalHealingCommand(
                VillageId: villageId,
                UnitTypeId: input.UnitTypeId,
                RequestedAmount: input.RequestedAmount);

            return Results.Ok(cmd);
        }).WithName("EnqueueHospitalHealing");

        group.MapPost("/academy/research", (int villageId, StartAcademyInput input) =>
        {
            if (input.SitterPermissions.HasValue &&
                !input.SitterPermissions.Value.HasFlag(SitterPermissions.BuildAndDemolish))
            {
                return Results.BadRequest(CommandResult.Fail(
                    TerraviaDomainErrorCodes.Sitters.PermissionDenied,
                    "Sitter lacks BuildAndDemolish permission to research Academy technologies."));
            }

            var cmd = new StartAcademyResearchCommand(
                VillageId: villageId,
                UnitTypeId: input.UnitTypeId);

            return Results.Ok(cmd);
        }).WithName("StartAcademyResearch");

        group.MapPost("/smithy/upgrade", (int villageId, StartSmithyInput input) =>
        {
            if (input.SitterPermissions.HasValue &&
                !input.SitterPermissions.Value.HasFlag(SitterPermissions.BuildAndDemolish))
            {
                return Results.BadRequest(CommandResult.Fail(
                    TerraviaDomainErrorCodes.Sitters.PermissionDenied,
                    "Sitter lacks BuildAndDemolish permission to start Smithy upgrades."));
            }

            var cmd = new StartSmithyUpgradeCommand(
                VillageId: villageId,
                Category: input.Category,
                UnitTypeId: input.UnitTypeId);

            return Results.Ok(cmd);
        }).WithName("StartSmithyUpgrade");

        group.MapPost("/research/finish-all-gold", (int villageId, SitterGateInput? input) =>
        {
            if (input?.SitterPermissions.HasValue == true &&
                !input.SitterPermissions.Value.HasFlag(SitterPermissions.BuyAndSpendGold))
            {
                return Results.BadRequest(CommandResult.Fail(
                    TerraviaDomainErrorCodes.Sitters.GoldPermissionDenied,
                    "Sitter lacks BuyAndSpendGold permission to finish research with Gold."));
            }

            var cmd = new FinishAllResearchWithGoldCommand(VillageId: villageId);
            return Results.Ok(cmd);
        }).WithName("FinishAllResearchWithGold");

        group.MapPost("/trapper/prisoners/{stackId:long}/release", (int villageId, long stackId, SitterGateInput? input) =>
        {
            if (input?.SitterPermissions.HasValue == true &&
                !input.SitterPermissions.Value.HasFlag(SitterPermissions.Reinforce))
            {
                return Results.BadRequest(CommandResult.Fail(
                    TerraviaDomainErrorCodes.Sitters.PermissionDenied,
                    "Sitter lacks Reinforce permission to release trapped prisoners."));
            }

            var cmd = new ReleasePrisonersCommand(CaptorVillageId: villageId, PrisonerStackId: stackId);
            return Results.Ok(cmd);
        }).WithName("ReleaseTrappedPrisoners");

        group.MapPost("/trapper/prisoners/{stackId:long}/dissolve", (int villageId, long stackId, SitterGateInput? input) =>
        {
            if (input?.SitterPermissions.HasValue == true &&
                !input.SitterPermissions.Value.HasFlag(SitterPermissions.Reinforce))
            {
                return Results.BadRequest(CommandResult.Fail(
                    TerraviaDomainErrorCodes.Sitters.PermissionDenied,
                    "Sitter lacks Reinforce permission to dissolve trapped prisoners."));
            }

            var cmd = new DissolvePrisonersCommand(
                CaptorVillageId: villageId,
                OwnerPlayerId: input?.OwnerPlayerId ?? 1,
                PrisonerStackId: stackId);
            return Results.Ok(cmd);
        }).WithName("DissolveTrappedPrisoners");

        return app;
    }
}
