using System.Data;
using Terravia.Domain.Players;

namespace Terravia.Infrastructure.Players;

/// <summary>
/// Dapper repository for hydrating and flushing <see cref="PlayerCultureLedger"/> and <c>CapitalVillageId</c>.
/// </summary>
public sealed class DapperPlayerRepository
{
    private readonly string _connectionString;

    public DapperPlayerRepository(string connectionString = "")
    {
        _connectionString = connectionString ?? string.Empty;
    }

    public string ConnectionString => _connectionString;

    public static DataTable CreatePlayerCultureTvp(IEnumerable<Player> rows)
    {
        var table = new DataTable();
        table.Columns.Add("PlayerId", typeof(int));
        table.Columns.Add("AccumulatedCulturePoints", typeof(double));
        table.Columns.Add("TotalVillageBaseCpPerDay", typeof(int));
        table.Columns.Add("HeroHelmetCpPerDay", typeof(int));
        table.Columns.Add("AlliancePhilosophyMultiplier", typeof(double));
        table.Columns.Add("LastCpUpdatedMs", typeof(long));
        table.Columns.Add("OwnedVillageCount", typeof(int));
        table.Columns.Add("CapitalVillageId", typeof(int));

        foreach (var r in rows)
        {
            table.Rows.Add(
                r.PlayerId,
                r.AccumulatedCulturePoints,
                r.TotalVillageBaseCpPerDay,
                r.HeroHelmetCpPerDay,
                r.AlliancePhilosophyMultiplier,
                r.LastCpUpdatedMs,
                r.OwnedVillageCount,
                r.CapitalVillageId);
        }

        return table;
    }

    public static Player ExtractPersistenceRow(PlayerCultureLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);

        return new Player
        {
            PlayerId = ledger.PlayerId,
            AccumulatedCulturePoints = ledger.AccumulatedCulturePoints,
            TotalVillageBaseCpPerDay = ledger.TotalVillageBaseCpPerDay,
            HeroHelmetCpPerDay = ledger.HeroHelmetCpPerDay,
            AlliancePhilosophyMultiplier = ledger.AlliancePhilosophyMultiplier,
            LastCpUpdatedMs = ledger.LastCpUpdatedMs,
            OwnedVillageCount = ledger.OwnedVillageCount,
            CapitalVillageId = ledger.CapitalVillageId
        };
    }

    public static PlayerCultureLedger HydrateFromRow(Player row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return new PlayerCultureLedger(row.PlayerId, row.CapitalVillageId, row.LastCpUpdatedMs)
        {
            AccumulatedCulturePoints = row.AccumulatedCulturePoints,
            TotalVillageBaseCpPerDay = row.TotalVillageBaseCpPerDay,
            HeroHelmetCpPerDay = row.HeroHelmetCpPerDay,
            AlliancePhilosophyMultiplier = row.AlliancePhilosophyMultiplier,
            OwnedVillageCount = row.OwnedVillageCount
        };
    }
}
