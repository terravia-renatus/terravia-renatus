namespace Terravia.Domain.Villages;

/// <summary>
/// Command to cancel an active timed building demolition in a village.
/// </summary>
public sealed record CancelBuildingDemolitionCommand(
    int ActorPlayerId,
    int OwnerPlayerId,
    int VillageId);
