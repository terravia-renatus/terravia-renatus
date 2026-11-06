namespace Terravia.Actors.Villages;

/// <summary>
/// Command message accruing Wood, Clay, Iron, and Crop in a village and evaluating Master Builder promotions.
/// </summary>
public sealed record AccrueVillageResourcesCommand(
    int AddWood,
    int AddClay,
    int AddIron,
    int AddCrop,
    long NowMs);
