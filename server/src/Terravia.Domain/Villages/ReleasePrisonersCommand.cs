namespace Terravia.Domain.Villages;

/// <summary>
/// Command for a Gaul trap owner (defender) to manually release a held prisoner stack (100% survive, 100% free trap repair) (FR-015 Path B).
/// </summary>
public sealed record ReleasePrisonersCommand(
    int CaptorVillageId,
    long PrisonerStackId);
