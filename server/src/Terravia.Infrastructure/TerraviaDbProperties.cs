namespace Terravia.Infrastructure;

/// <summary>
/// Centralized database table and stored procedure naming constants for SQL Server persistence.
/// </summary>
public static class TerraviaDbProperties
{
    public const string DbTablePrefix = "";
    public const string DbSchema = "dbo";

    public const string VillagesTableName = "Villages";
    public const string VillageBuildingsTableName = "VillageBuildings";
    public const string VillageQueueItemsTableName = "VillageQueueItems";
    public const string VillageTrainingQueuesTableName = "VillageTrainingQueues";
    public const string VillageResearchQueuesTableName = "VillageResearchQueues";
    public const string VillageTechnologiesTableName = "VillageTechnologies";
    public const string VillageTroopStacksTableName = "VillageTroopStacks";
    public const string MapTilesTableName = "MapTiles";
    public const string OasisStateTableName = "OasisState";
    public const string PlayersTableName = "Players";

    public const string VillageBuildingSlotTvpName = "dbo.VillageBuildingSlotTvp";
    public const string VillageQueueItemTvpName = "dbo.VillageQueueItemTvp";
    public const string VillageTrainingQueueItemTvpName = "dbo.VillageTrainingQueueItemTvp";
    public const string VillageResearchQueueItemTvpName = "dbo.VillageResearchQueueItemTvp";
    public const string VillageTechnologyBatchTvpName = "dbo.VillageTechnologyBatchTvp";
    public const string VillageEconomyBatchTvpName = "dbo.VillageEconomyBatchTvp";
    public const string OasisStateBatchTvpName = "dbo.OasisStateBatchTvp";
    public const string PlayerCultureBatchTvpName = "dbo.PlayerCultureBatchTvp";

    public const string FlushVillageBuildingAndQueueBatchProc = "dbo.usp_FlushVillageBuildingAndQueueBatch";
}
