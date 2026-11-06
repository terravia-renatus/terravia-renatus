namespace Terravia.Domain.Villages;

/// <summary>
/// Timer-triggered command scheduled by <c>VillageActor</c> whenever <c>NetCrop &lt; 0</c> and <c>IsPeaceMode == false</c>.
/// </summary>
public sealed record TriggerVillageStarvationCommand(
    int VillageId,
    long ExpectedTimestampMs);
