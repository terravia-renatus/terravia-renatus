using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Terravia.Infrastructure.Oases;

/// <summary>
/// High-throughput Dapper repository for hydrating and flushing <see cref="OasisState"/> and <see cref="MapTile"/> records.
/// </summary>
public sealed class DapperOasisRepository
{
    private readonly string _connectionString;

    public DapperOasisRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public static DataTable CreateOasisStateTvp(IEnumerable<OasisState> states)
    {
        var table = new DataTable();
        table.Columns.Add("TileId", typeof(int));
        table.Columns.Add("OasisTypeId", typeof(byte));
        table.Columns.Add("RichnessTier", typeof(byte));
        table.Columns.Add("AnnexedByVillageId", typeof(int));
        table.Columns.Add("OwnerPlayerId", typeof(int));
        table.Columns.Add("Loyalty", typeof(double));
        table.Columns.Add("Wood", typeof(double));
        table.Columns.Add("Clay", typeof(double));
        table.Columns.Add("Iron", typeof(double));
        table.Columns.Add("Crop", typeof(double));
        table.Columns.Add("LastUpdatedMs", typeof(long));
        table.Columns.Add("NextNatureRegenAtMs", typeof(long));

        foreach (var s in states)
        {
            table.Rows.Add(
                s.TileId,
                s.OasisTypeId,
                s.RichnessTier,
                s.AnnexedByVillageId.HasValue ? s.AnnexedByVillageId.Value : DBNull.Value,
                s.OwnerPlayerId,
                s.Loyalty,
                s.Wood,
                s.Clay,
                s.Iron,
                s.Crop,
                s.LastUpdatedMs,
                s.NextNatureRegenAtMs);
        }

        return table;
    }

    public async Task<OasisState?> GetOasisStateByTileIdAsync(
        int tileId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = """
            SELECT TileId, OasisTypeId, RichnessTier, AnnexedByVillageId, OwnerPlayerId,
                   Loyalty, Wood, Clay, Iron, Crop, LastUpdatedMs, NextNatureRegenAtMs
            FROM dbo.OasisState
            WHERE TileId = @TileId;
            """;

        return await conn.QuerySingleOrDefaultAsync<OasisState>(
            new CommandDefinition(sql, new { TileId = tileId }, cancellationToken: cancellationToken));
    }
}
