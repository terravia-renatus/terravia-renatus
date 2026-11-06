namespace Terravia.Domain.Villages;

/// <summary>
/// Hero resource bonus allocation focus mode (Balanced = 3/pt each; SingleResource = 10/pt).
/// </summary>
public enum HeroResourceFocusMode
{
    Balanced = 0,
    Wood = 1,
    Clay = 2,
    Iron = 3,
    Crop = 4
}
