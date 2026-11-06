namespace Terravia.Domain.Catalog;

/// <summary>
/// Immutable four-resource cost vector (Wood, Clay, Iron, Crop).
/// </summary>
public readonly record struct ResourceCost
{
    public int Wood { get; }
    public int Clay { get; }
    public int Iron { get; }
    public int Crop { get; }
    public int Total => Wood + Clay + Iron + Crop;

    public ResourceCost(int wood, int clay, int iron, int crop)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(wood);
        ArgumentOutOfRangeException.ThrowIfNegative(clay);
        ArgumentOutOfRangeException.ThrowIfNegative(iron);
        ArgumentOutOfRangeException.ThrowIfNegative(crop);

        Wood = wood;
        Clay = clay;
        Iron = iron;
        Crop = crop;
    }

    public static readonly ResourceCost Zero = new(0, 0, 0, 0);

    public ResourceCost Multiply(int factor) => new(Wood * factor, Clay * factor, Iron * factor, Crop * factor);
}
