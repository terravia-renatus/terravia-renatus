namespace Terravia.Domain.Villages;

/// <summary>
/// Command to instantly complete all active Academy, Blacksmith, and Armoury research items in a village for 2 Gold (User Story 3).
/// </summary>
public sealed record FinishAllResearchWithGoldCommand(
    int VillageId);
