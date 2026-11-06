namespace Terravia.Domain;

/// <summary>
/// Sitter permission bitmask flags per Constitutional Principle V.
/// </summary>
[Flags]
public enum SitterPermissions
{
    None = 0,
    Raid = 1 << 0,
    Reinforce = 1 << 1,
    SendResources = 1 << 2,
    BuyAndSpendGold = 1 << 3,
    ReadAndSendMessages = 1 << 4,
    DeleteReports = 1 << 5,
    BuildAndDemolish = 1 << 6
}
