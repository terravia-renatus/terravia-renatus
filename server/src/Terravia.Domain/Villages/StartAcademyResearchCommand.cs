namespace Terravia.Domain.Villages;

/// <summary>
/// Command to initiate Academy (gid 22) discovery for a unit slot X2..X9 (FR-008).
/// </summary>
public sealed record StartAcademyResearchCommand(
    int VillageId,
    int UnitTypeId);
