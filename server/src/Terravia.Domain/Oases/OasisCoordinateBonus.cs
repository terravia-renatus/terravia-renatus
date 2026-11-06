namespace Terravia.Domain.Oases;

/// <summary>
/// Lightweight coordinate and oasis type descriptor used to precompute <c>BestOasisCropBonus</c> within a 7x7 toroidal neighborhood.
/// </summary>
public readonly record struct OasisCoordinateBonus(
    short X,
    short Y,
    byte OasisTypeId);
