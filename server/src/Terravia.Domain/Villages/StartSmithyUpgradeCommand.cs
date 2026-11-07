namespace Terravia.Domain.Villages;

/// <summary>
/// Command to initiate a Blacksmith (gid 12) offense upgrade or Armoury (gid 13) defense upgrade for a unit slot X1..X8 (FR-009).
/// </summary>
public sealed record StartSmithyUpgradeCommand(
    int VillageId,
    ResearchCategory Category,
    int UnitTypeId);
