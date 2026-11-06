using Terravia.Domain.Villages;

namespace Terravia.Actors.Villages;

/// <summary>
/// Outbound read projection DTO summarizing a village's economy, storage, oases, celebrations, and expansion slots.
/// </summary>
public sealed record VillageEconomySnapshotDto(
    int VillageId,
    int TileId,
    string Name,
    int FieldTopologyId,
    bool IsCapital,
    bool IsArchon,
    int Population,
    int CulturePointsPerDay,
    double Loyalty,
    double LoyaltyRegenPerHour,
    long LastUpdatedServerMs,
    double Wood,
    double Clay,
    double Iron,
    double Crop,
    int WarehouseCap,
    int GranaryCap,
    ResourceProductionSnapshot Production,
    CelebrationType ActiveCelebrationType,
    long CelebrationEndsAtMs,
    long FestivalEndsAtMs,
    int UnlockedExpansionSlots,
    int? ExpansionSlot1VillageId,
    int? ExpansionSlot2VillageId,
    int? ExpansionSlot3VillageId,
    int RemainingTrainableSettlerEquivalents);
