using Terravia.Domain.Catalog;

namespace Terravia.Domain.Villages;

/// <summary>
/// Command triggered when 3 Settlers arrive at an unoccupied map tile to found a new village (<c>FR-016</c>).
/// </summary>
public sealed record FoundNewVillageCommand(
    int LaunchingVillageId,
    int OwnerPlayerId,
    int OwnerTribeId,
    int TargetTileId,
    int TargetFieldTopologyId,
    ResourceCost CarriedResources);
