namespace Terravia.Domain.Villages;

/// <summary>
/// Outcome of an administrator persuasion attack and village capture evaluation (<c>FR-017</c>).
/// </summary>
public sealed record ConquestResolutionResult(
    bool CanAttemptPersuasion,
    string? RejectionReasonCode,
    double TotalLoyaltyReduction,
    double ResultingLoyalty,
    bool WasVillageConquered,
    int AdministratorsConsumed,
    bool ApplyBiggerPlayerMinusOneBuildingLevelPenalty);
