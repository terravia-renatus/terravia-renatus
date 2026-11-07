using Terravia.Domain.Villages;

namespace Terravia.Actors.Villages;

/// <summary>
/// Event Transfer Object emitted when trapped prisoners are manually released, dissolved, or liberated in combat.
/// </summary>
public sealed record PrisonersReleasedOrLiberatedEto(
    int CaptorVillageId,
    IReadOnlyList<PrisonerLiberationOutcome> Outcomes,
    int FreeRepairsQueued,
    long TimestampMs);
