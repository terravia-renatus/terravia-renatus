namespace Terravia.Domain.Villages;

/// <summary>
/// Command to instantly complete eligible constructions, active demolition, and researches in a village with Gold.
/// </summary>
public sealed record FinishAllConstructionsWithGoldCommand(
    int ActorPlayerId,
    int OwnerPlayerId,
    int VillageId);
