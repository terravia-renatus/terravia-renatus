namespace Terravia.Domain.Oases;

/// <summary>
/// Input context for evaluating an oasis annexation or loyalty reduction strike by a surviving Hero.
/// </summary>
public readonly record struct OasisStrikeContext(
    short AttackerVillageX,
    short AttackerVillageY,
    short OasisX,
    short OasisY,
    int MapRadius,
    int AttackerHeroMansionLevel,
    int AttackerCurrentlyOwnedOases,
    bool AttackerHeroSurvived,
    bool IsNormalAttack,
    int RemainingOasisDefenders,
    int? CurrentAnnexedByVillageId,
    int DefenderCurrentlyOwnedOases,
    double CurrentOasisLoyalty);
