namespace Terravia.Domain.Oases;

/// <summary>
/// Evaluation result of a Hero strike against a wild or player-annexed oasis.
/// </summary>
public readonly record struct OasisConquestEvaluation(
    bool WasAnnexedOrConquered,
    int LoyaltyReductionPercent,
    double ResultingLoyalty);
