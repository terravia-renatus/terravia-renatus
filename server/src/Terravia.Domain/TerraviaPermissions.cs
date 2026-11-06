namespace Terravia.Domain;

/// <summary>
/// Hierarchical permission definitions and bitmask mappings across Sitter delegation and Administrative roles.
/// </summary>
public static class TerraviaPermissions
{
    public const string GroupName = "Terravia";

    public static class Sitter
    {
        public const string Default = GroupName + ".Sitter";
        public const string Raid = Default + ".Raid";
        public const string NormalAttack = Default + ".NormalAttack";
        public const string Reinforce = Default + ".Reinforce";
        public const string SendResources = Default + ".SendResources";
        public const string SpendGold = Default + ".SpendGold";
        public const string ChatAndMessages = Default + ".ChatAndMessages";
        public const string BuildAndDemolish = Default + ".BuildAndDemolish";

        public const SitterPermissions GoldMask = SitterPermissions.BuyAndSpendGold;
        public const SitterPermissions BuildMask = SitterPermissions.BuildAndDemolish;
    }

    public static class Admin
    {
        public const string Default = GroupName + ".Admin";
        public const string ManageRound = Default + ".ManageRound";
        public const string ManageBans = Default + ".ManageBans";
        public const string OverridePushProtection = Default + ".OverridePushProtection";
    }
}
