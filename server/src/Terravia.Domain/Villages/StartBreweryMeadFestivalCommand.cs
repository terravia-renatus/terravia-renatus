namespace Terravia.Domain.Villages;

/// <summary>
/// Command to start a 72-hour Mead Festival in a Teuton Capital village's Brewery (<c>gid 35</c>).
/// </summary>
public sealed record StartBreweryMeadFestivalCommand(
    int ActorPlayerId,
    int OwnerPlayerId,
    int VillageId);
