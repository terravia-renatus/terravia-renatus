namespace Terravia.Domain.Villages;

/// <summary>
/// Command to start a timed 1-level demolition or execute a 10-Gold instant complete demolition to Level 0.
/// </summary>
public sealed record StartBuildingDemolitionCommand(
    int ActorPlayerId,
    int OwnerPlayerId,
    int VillageId,
    int SlotId,
    bool IsInstantCompleteWithGold = false);
