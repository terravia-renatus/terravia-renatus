using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Terravia.Domain.Catalog;
using Terravia.Domain.Villages;

namespace Terravia.Infrastructure.Villages;

/// <summary>
/// High-throughput Dapper repository using Table-Valued Parameters (TVPs) for VillageActor hydration
/// and micro-batched / immediate write-behind flushes (Constitutional Principle IV).
/// </summary>
public sealed class DapperVillageRepository
{
    private readonly string _connectionString;
    private readonly IGameCatalog _catalog;

    public DapperVillageRepository(string connectionString, IGameCatalog catalog)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public static DataTable CreateBuildingSlotsTvp(IEnumerable<VillageBuilding> rows)
    {
        var table = new DataTable();
        table.Columns.Add("VillageId", typeof(int));
        table.Columns.Add("SlotId", typeof(int));
        table.Columns.Add("BuildingTypeId", typeof(int));
        table.Columns.Add("Level", typeof(int));

        foreach (var row in rows)
        {
            table.Rows.Add(row.VillageId, row.SlotId, row.BuildingTypeId, row.Level);
        }

        return table;
    }

    public static DataTable CreateQueueItemsTvp(IEnumerable<VillageQueueItem> rows)
    {
        var table = new DataTable();
        table.Columns.Add("QueueItemId", typeof(long));
        table.Columns.Add("VillageId", typeof(int));
        table.Columns.Add("QueueCategory", typeof(int));
        table.Columns.Add("SlotId", typeof(int));
        table.Columns.Add("BuildingTypeId", typeof(int));
        table.Columns.Add("TargetLevel", typeof(int));
        table.Columns.Add("IsWaitingLoop", typeof(bool));
        table.Columns.Add("IsMasterBuilder", typeof(bool));
        table.Columns.Add("StartedAtMs", typeof(long));
        table.Columns.Add("FinishesAtMs", typeof(long));

        foreach (var row in rows)
        {
            table.Rows.Add(
                row.QueueItemId,
                row.VillageId,
                (int)row.QueueCategory,
                row.SlotId,
                row.BuildingTypeId,
                row.TargetLevel,
                row.IsWaitingLoop,
                row.IsMasterBuilder,
                row.StartedAtMs,
                row.FinishesAtMs);
        }

        return table;
    }

    public static (IReadOnlyList<VillageBuilding> Buildings, IReadOnlyList<VillageQueueItem> QueueItems)
        ExtractPersistenceBatch(VillageState state)
    {
        var buildings = new List<VillageBuilding>(41);
        for (int slot = VillageConsts.MinResourceFieldSlotId; slot <= VillageConsts.MaxStandardSlotId; slot++)
        {
            if (state.SlotBuildingTypes[slot] != 0 || state.SlotLevels[slot] != 0)
            {
                buildings.Add(new VillageBuilding
                {
                    VillageId = state.VillageId,
                    SlotId = slot,
                    BuildingTypeId = state.SlotBuildingTypes[slot],
                    Level = state.SlotLevels[slot]
                });
            }
        }

        if (state.WorldWonderBuildingType == 40)
        {
            buildings.Add(new VillageBuilding
            {
                VillageId = state.VillageId,
                SlotId = VillageConsts.WorldWonderSlotId,
                BuildingTypeId = 40,
                Level = state.WorldWonderLevel
            });
        }

        var queueItems = new List<VillageQueueItem>(state.ConstructionQueue.Count + 1);
        foreach (var q in state.ConstructionQueue)
        {
            queueItems.Add(new VillageQueueItem
            {
                QueueItemId = q.QueueItemId,
                VillageId = state.VillageId,
                QueueCategory = VillageQueueCategory.Building,
                SlotId = q.SlotId,
                BuildingTypeId = q.BuildingTypeId,
                TargetLevel = q.TargetLevel,
                IsWaitingLoop = q.IsWaitingLoop,
                IsMasterBuilder = q.IsMasterBuilder,
                StartedAtMs = q.StartedAtMs,
                FinishesAtMs = q.FinishesAtMs
            });
        }

        if (state.ActiveDemolition is { } demo)
        {
            queueItems.Add(new VillageQueueItem
            {
                QueueItemId = -demo.VillageId,
                VillageId = demo.VillageId,
                QueueCategory = VillageQueueCategory.Demolition,
                SlotId = demo.SlotId,
                BuildingTypeId = demo.BuildingTypeId,
                TargetLevel = demo.TargetLevel,
                IsWaitingLoop = false,
                IsMasterBuilder = false,
                StartedAtMs = 0,
                FinishesAtMs = demo.FinishesAtMs
            });
        }

        return (buildings, queueItems);
    }

    /// <summary>
    /// Hydrates a <see cref="VillageState"/> in-memory aggregate from Dapper persistence rows.
    /// </summary>
    public void ApplyHydratedRows(
        VillageState state,
        IEnumerable<VillageBuilding> buildingRows,
        IEnumerable<VillageQueueItem> queueRows)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(buildingRows);
        ArgumentNullException.ThrowIfNull(queueRows);

        foreach (var b in buildingRows)
        {
            if (b.VillageId != state.VillageId)
                continue;
            state.SetSlot(b.SlotId, b.BuildingTypeId, b.Level);
        }

        state.ConstructionQueue.Clear();
        state.ActiveDemolition = null;

        foreach (var q in queueRows.OrderBy(r => r.QueueItemId))
        {
            if (q.VillageId != state.VillageId)
                continue;

            if (q.QueueCategory == VillageQueueCategory.Building)
            {
                var stats = _catalog.GetBuildingLevelStats(q.BuildingTypeId, q.TargetLevel);
                int durationSec = q.StartedAtMs > 0 && q.FinishesAtMs >= q.StartedAtMs
                    ? (int)((q.FinishesAtMs - q.StartedAtMs) / 1000L)
                    : (int)(q.FinishesAtMs / 1000L);

                state.ConstructionQueue.Add(new ConstructionQueueItem(
                    QueueItemId: q.QueueItemId,
                    VillageId: q.VillageId,
                    SlotId: q.SlotId,
                    BuildingTypeId: q.BuildingTypeId,
                    TargetLevel: q.TargetLevel,
                    IsWaitingLoop: q.IsWaitingLoop,
                    IsMasterBuilder: q.IsMasterBuilder,
                    StartedAtMs: q.StartedAtMs,
                    FinishesAtMs: q.FinishesAtMs,
                    DeductedOrRequiredCost: stats.Cost,
                    BuildDurationSeconds: durationSec));
            }
            else if (q.QueueCategory == VillageQueueCategory.Demolition)
            {
                state.ActiveDemolition = new DemolitionQueueItem(
                    VillageId: q.VillageId,
                    SlotId: q.SlotId,
                    BuildingTypeId: q.BuildingTypeId,
                    TargetLevel: q.TargetLevel,
                    FinishesAtMs: q.FinishesAtMs);
            }
        }
    }

    /// <summary>
    /// Queries SQL Server via Dapper multi-mapping (`QueryMultipleAsync`) to hydrate a village's building slots and active queues.
    /// </summary>
    public async Task<VillageState> HydrateVillageStateAsync(
        int villageId,
        int ownerPlayerId,
        int tribeId,
        bool isCapital,
        CancellationToken cancellationToken = default)
    {
        var state = new VillageState(villageId, ownerPlayerId, tribeId, isCapital);

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = """
            SELECT VillageId, SlotId, BuildingTypeId, Level
            FROM dbo.VillageBuildings
            WHERE VillageId = @VillageId;

            SELECT QueueItemId, VillageId, QueueCategory, SlotId, BuildingTypeId, TargetLevel, IsWaitingLoop, IsMasterBuilder, StartedAtMs, FinishesAtMs
            FROM dbo.VillageQueueItems
            WHERE VillageId = @VillageId
            ORDER BY QueueItemId ASC;
            """;

        using var multi = await conn.QueryMultipleAsync(
            new CommandDefinition(sql, new { VillageId = villageId }, cancellationToken: cancellationToken));

        var buildings = await multi.ReadAsync<VillageBuilding>();
        var queueItems = await multi.ReadAsync<VillageQueueItem>();

        ApplyHydratedRows(state, buildings, queueItems);
        return state;
    }

    public async Task FlushVillageStateBatchAsync(
        IEnumerable<VillageState> states,
        CancellationToken cancellationToken = default)
    {
        var allBuildings = new List<VillageBuilding>();
        var allQueues = new List<VillageQueueItem>();

        foreach (var state in states)
        {
            var (b, q) = ExtractPersistenceBatch(state);
            allBuildings.AddRange(b);
            allQueues.AddRange(q);
        }

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        using var buildingsTvp = CreateBuildingSlotsTvp(allBuildings);
        using var queuesTvp = CreateQueueItemsTvp(allQueues);

        var parameters = new DynamicParameters();
        parameters.Add("@BuildingSlots", buildingsTvp.AsTableValuedParameter(TerraviaDbProperties.VillageBuildingSlotTvpName));
        parameters.Add("@QueueItems", queuesTvp.AsTableValuedParameter(TerraviaDbProperties.VillageQueueItemTvpName));

        await conn.ExecuteAsync(
            new CommandDefinition(
                TerraviaDbProperties.FlushVillageBuildingAndQueueBatchProc,
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
    }
}
