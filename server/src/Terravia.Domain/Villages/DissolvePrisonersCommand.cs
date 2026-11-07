namespace Terravia.Domain.Villages;

/// <summary>
/// Command for a trapped troop owner (attacker) to manually dissolve/kill their trapped prisoner stack (100% die, 1/3 free trap repair) (FR-015 Path C).
/// </summary>
public sealed record DissolvePrisonersCommand(
    int CaptorVillageId,
    int OwnerPlayerId,
    long PrisonerStackId);
