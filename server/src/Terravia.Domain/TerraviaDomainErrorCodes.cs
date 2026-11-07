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

        public const string NegativeCropConstructionRestricted = "Terravia:Villages:NegativeCropConstructionRestricted";
        public const string CelebrationAlreadyActive = "Terravia:Villages:CelebrationAlreadyActive";
        public const string InsufficientTownHallLevel = "Terravia:Villages:InsufficientTownHallLevel";
        public const string InsufficientResources = "Terravia:Villages:InsufficientResources";
        public const string MeadFestivalRequiresTeutonCapitalBrewery = "Terravia:Villages:MeadFestivalRequiresTeutonCapitalBrewery";
        public const string FestivalAlreadyActive = "Terravia:Villages:FestivalAlreadyActive";
        public const string CannotMakeWorldWonderVillageCapital = "Terravia:Villages:CannotMakeWorldWonderVillageCapital";
        public const string CapitalRequiresPalaceOrCommandCenter = "Terravia:Villages:CapitalRequiresPalaceOrCommandCenter";
        public const string ExpansionUnitQuotaExceeded = "Terravia:Villages:ExpansionUnitQuotaExceeded";
        public const string InsufficientCulturePointsForExpansion = "Terravia:Villages:InsufficientCulturePointsForExpansion";
        public const string NoFreeExpansionSlot = "Terravia:Villages:NoFreeExpansionSlot";
        public const string TargetTileAlreadyOccupied = "Terravia:Villages:TargetTileAlreadyOccupied";
        public const string CannotConquerCapitalOrLastVillage = "Terravia:Villages:CannotConquerCapitalOrLastVillage";
        public const string TargetExpansionBuildingStillStanding = "Terravia:Villages:TargetExpansionBuildingStillStanding";

        public const string InvalidUnitForFacility = "Terravia:Villages:InvalidUnitForFacility";
        public const string UnitNotResearched = "Terravia:Villages:UnitNotResearched";
        public const string TrainingFacilityNotBuilt = "Terravia:Villages:TrainingFacilityNotBuilt";
        public const string GreatMilitaryForbiddenInCapital = "Terravia:Villages:GreatMilitaryForbiddenInCapital";
        public const string SimultaneousChiefAndSettlerForbidden = "Terravia:Villages:SimultaneousChiefAndSettlerForbidden";
        public const string NegativeCropTrainingRestricted = "Terravia:Villages:NegativeCropTrainingRestricted";
        public const string AcademyResearchAlreadyActive = "Terravia:Villages:AcademyResearchAlreadyActive";
        public const string UnitAlreadyResearched = "Terravia:Villages:UnitAlreadyResearched";
        public const string ResearchPrerequisitesNotMet = "Terravia:Villages:ResearchPrerequisitesNotMet";
        public const string SmithyQueueFull = "Terravia:Villages:SmithyQueueFull";
        public const string SmithyLevelCapReached = "Terravia:Villages:SmithyLevelCapReached";
        public const string InsufficientWoundedUnits = "Terravia:Villages:InsufficientWoundedUnits";
        public const string TrapperCapacityExceeded = "Terravia:Villages:TrapperCapacityExceeded";
        public const string PrisonerStackNotFound = "Terravia:Villages:PrisonerStackNotFound";
    }

    public static class Oases
    {
        public const string OasisOutOfSevenBySevenRange = "Terravia:Oases:OasisOutOfSevenBySevenRange";
        public const string NoFreeHeroMansionOasisSlot = "Terravia:Oases:NoFreeHeroMansionOasisSlot";
        public const string OasisNotOwnedByVillage = "Terravia:Oases:OasisNotOwnedByVillage";
    }

    public static class Sitters
    {
        public const string PermissionDenied = "Terravia:Sitters:PermissionDenied";
        public const string GoldPermissionDenied = "Terravia:Sitters:GoldPermissionDenied";
    }
}
