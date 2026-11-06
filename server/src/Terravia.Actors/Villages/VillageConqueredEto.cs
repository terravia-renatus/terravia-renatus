namespace Terravia.Actors.Villages;

/// <summary>
/// Event Transfer Object emitted when a village is conquered via administrator loyalty reduction
/// or razed to zero population.
/// </summary>
public sealed record VillageConqueredEto(
    int VillageId,
    int PreviousOwnerPlayerId,
    int NewOwnerPlayerId,
    int NewOwnerTribeId,
    int ExpansionOriginVillageId,
    bool WasRazedToZeroPopulation,
    IReadOnlyList<int> ReleasedOasisTileIds,
    long TimestampMs);
