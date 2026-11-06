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
    public const string VillageBuildingSlotTvpName = "dbo.VillageBuildingSlotTvp";
    public const string VillageQueueItemTvpName = "dbo.VillageQueueItemTvp";
    public const string FlushVillageBuildingAndQueueBatchProc = "dbo.usp_FlushVillageBuildingAndQueueBatch";
}
