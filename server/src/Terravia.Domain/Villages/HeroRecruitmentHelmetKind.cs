namespace Terravia.Domain.Villages;

/// <summary>
/// Stationed Hero helmet modifiers (ItemId 10..15) reducing infantry or cavalry training durations (FR-006).
/// </summary>
public enum HeroRecruitmentHelmetKind
{
    None = 0,
    Horseman10 = 10,
    Cavalry15 = 11,
    HeavyCavalry20 = 12,
    Mercenary10 = 13,
    Warrior15 = 14,
    Ruler20 = 15
}
