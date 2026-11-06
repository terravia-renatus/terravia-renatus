namespace Terravia.Domain.Villages;

/// <summary>
/// Command to construct a new building on an empty slot or upgrade an existing building.
/// </summary>
public sealed record ConstructOrUpgradeBuildingCommand(
    int ActorPlayerId,
    int OwnerPlayerId,
    int VillageId,
    int SlotId,
    int BuildingTypeId,
    bool UseMasterBuilder = false);
