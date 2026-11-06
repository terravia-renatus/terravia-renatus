namespace Terravia.Domain;

/// <summary>
/// Hierarchical domain error codes formatted as &lt;Namespace&gt;:&lt;Feature&gt;:&lt;Code&gt; for deterministic testing and client localization.
/// </summary>
public static class TerraviaDomainErrorCodes
{
    public static class Villages
    {
        public const string QueueItemNotFound = "Terravia:Villages:QueueItemNotFound";
        public const string WorldWonderVillageFinishAllForbidden = "Terravia:Villages:WorldWonderVillageFinishAllForbidden";
        public const string NothingEligibleToFinish = "Terravia:Villages:NothingEligibleToFinish";
        public const string InsufficientGold = "Terravia:Villages:InsufficientGold";
        public const string InsufficientResourcesForMasterBuilderFinish = "Terravia:Villages:InsufficientResourcesForMasterBuilderFinish";
        public const string NoActiveDemolition = "Terravia:Villages:NoActiveDemolition";
    }

    public static class Sitters
    {
        public const string PermissionDenied = "Terravia:Sitters:PermissionDenied";
        public const string GoldPermissionDenied = "Terravia:Sitters:GoldPermissionDenied";
    }
}
