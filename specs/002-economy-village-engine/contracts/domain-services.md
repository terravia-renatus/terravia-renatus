# Domain Service & Command Contracts (`Terravia.Domain`)

**Feature**: `002-economy-village-engine`  
**Date**: 2026-11-06  
**Spec**: [spec.md](../spec.md) | **Data Model**: [data-model.md](../data-model.md)

## 1. Pure Domain Calculators, Catalogs & Managers (`server/src/Terravia.Domain`)

Per **Constitutional Principle II** and **Naming & Structural Conventions**:
- Pure mathematical evaluators and static lookup tables are suffixed with **`Calculator`** or **`Catalog`**.
- Stateful or multi-aggregate domain orchestration services are suffixed with **`Manager`** and injected as concrete classes.
- Every public type resides in its own `.cs` file inside its bounded feature folder (`Terravia.Domain.Villages`, `Terravia.Domain.Oases`, `Terravia.Domain.Players`).

```csharp
namespace Terravia.Domain.Villages;

public interface IResourceProductionCalculator
{
    /// <summary>
    /// Computes the exact 6-step hourly Gross Production for Wood, Clay, Iron, and Crop
    /// plus EffectivePopulationUpkeep, TotalTroopUpkeepPerHour, and NetCropPerHour.
    /// Used identically by VillageActor lazy ticks and StarvationCalculator.
    /// </summary>
    ResourceProductionSnapshot CalculateProduction(in VillageProductionContext context);

    /// <summary>
    /// Aggregates total WarehouseCap (gid 10 + gid 38) and GranaryCap (gid 11 + gid 39)
    /// multiplied by StorageMultiplier, enforcing the BaseStorageCapacity floor (800 * StorageMultiplier).
    /// </summary>
    (int WarehouseCap, int GranaryCap) CalculateStorageCapacities(
        ReadOnlySpan<int> slotBuildingTypes,
        ReadOnlySpan<int> slotLevels,
        int storageMultiplier);

    /// <summary>
    /// Lazily advances stored Wood, Clay, Iron, Crop, and Village Loyalty over elapsed
    /// milliseconds (nowMs - lastUpdatedMs), clamping strictly to [0, WarehouseCap/GranaryCap]
    /// and [0, 100] loyalty.
    /// </summary>
    LazyAccrualResult AccrueVillageResourcesAndLoyalty(
        in VillageAccrualInput input,
        long nowMs);
}

public interface IStarvationCalculator
{
    /// <summary>
    /// Evaluates deterministic 5-tier starvation (1: OasisReinforcements -> 2: VillageReinforcements
    /// [excluding captured Nature OwnerVillageId==0] -> 3: TrappedPrisoners -> 4: OwnStationed -> 5: OwnMoving).
    /// Eliminates 1 unit from the strictly largest stack (ties broken by lowest UnitTypeId, Hero last in stack),
    /// flags empty stacks/movements for immediate deletion, and returns the credited base CropCost.
    /// </summary>
    bool TryResolveNextStarvationStep(
        in StarvationEvaluationContext context,
        out StarvationStepResult stepResult);
}
```

```csharp
namespace Terravia.Domain.Oases;

public sealed class OasisAnnexationManager
{
    /// <summary>
    /// Validates toroidal 7x7 neighborhood distance (|dx| <= 3 && |dy| <= 3 with wraparound).
    /// </summary>
    public bool IsWithinToroidalAnnexationRadius(
        short villageX,
        short villageY,
        short oasisX,
        short oasisY,
        int mapRadius);

    /// <summary>
    /// Computes unlocked oasis slots from Hero's Mansion (gid 37): floor((level - 5) / 5) -> 0, 1, 2, or 3.
    /// </summary>
    public int CalculateUnlockedOasisSlots(int heroMansionLevel);

    /// <summary>
    /// Pre-combat Hero Cage (ItemTypeId = 114) capture against an unoccupied wild oasis (AnnexedByVillageId == null).
    /// Captures 1 animal per Cage sequentially from u31..u40 before battle resolution.
    /// </summary>
    public HeroCageCaptureResult ResolveHeroCageCapture(
        int availableCages,
        Span<int> wildOasisFaunaCounts);

    /// <summary>
    /// Evaluates oasis annexation (100% immediate on cleared wild oasis) or loyalty reduction
    /// against a player-annexed oasis (floor(100 / min(3, 4 - defenderOwnedOases)) -> 33%, 50%, or 100%).
    /// </summary>
    public OasisConquestEvaluation EvaluateHeroOasisStrike(in OasisStrikeContext context);
}
```

```csharp
namespace Terravia.Domain.Villages;

public sealed class CelebrationManager
{
    /// <summary>
    /// Validates and computes Town Hall (gid 24) Small Celebration (Lvl >= 1, 500 CP)
    /// and Great Celebration (Lvl >= 10, 2,000 CP, +5%/-5% loyalty modifier) costs and durations.
    /// </summary>
    public CelebrationStartResult EvaluateStartCelebration(
        CelebrationType celebrationType,
        int townHallLevel,
        bool hasActiveCelebration,
        in ResourceCost availableResources,
        double serverSpeed);

    /// <summary>
    /// Validates and computes Teuton Brewery (gid 35, Capital only, Lvl 1..10) Mead Festival
    /// (72h / Speed duration, +1%..+10% attack bonus, random catapults, 50% Chief persuasion).
    /// </summary>
    public MeadFestivalStartResult EvaluateStartMeadFestival(
        int ownerTribeId,
        bool isCapital,
        int breweryLevel,
        bool hasActiveFestival,
        in ResourceCost availableResources,
        double serverSpeed);
}

public sealed class VillageExpansionManager
{
    /// <summary>
    /// Returns the strictly monotonic Culture Point threshold required to found or conquer village N (1..125)
    /// for CulturePointCurveMode (Fast vs Standard).
    /// </summary>
    public int GetRequiredCulturePointsForVillageNumber(int targetVillageNumber, CulturePointCurveMode curveMode);

    /// <summary>
    /// Computes unlocked expansion slots (Residence gid 25 -> 10/20; Palace gid 26 & Command Center gid 44 -> 10/15/20)
    /// and remaining trainable Settler-equivalents (UnlockedSlots * 3 - 3*OccupiedSlots - 3*Admins - 1*Settlers).
    /// </summary>
    public ExpansionTrainingQuota EvaluateExpansionTrainingQuota(in ExpansionQuotaContext context);

    /// <summary>
    /// Computes atomic Capital transfer mutations between oldCapital and newCapital
    /// (rejects if newCapital.IsArchon; downgrades oldCapital Slot 1..18 > 10 to 10; destroys oldCapital gid 34, 35;
    /// destroys newCapital gid 29, 30, 38, 39, 49; cancels invalidated build queue jobs).
    /// </summary>
    public CapitalTransferPlan BuildCapitalTransferPlan(
        in VillageCapitalState oldCapital,
        in VillageCapitalState newCapital);
}

public sealed class SiegeAndConquestCalculator
{
    /// <summary>
    /// Evaluates administrator loyalty reduction per surviving unit:
    /// ((BaseRand + AttackerGreatCel(5) - DefenderGreatCel(5)) / MoraleBonus) * (0.5 if Teuton Mead Festival else 1.0),
    /// pre-conquest AttackerTotalPop > DefenderTotalPop -1 building level penalty, tribe-exclusive building cleanup
    /// (gid 35, 36, 41, 44, 45, 48), Wall destruction, queue cancellations, and oasis release directives.
    /// </summary>
    public ConquestResolutionResult EvaluateAdministratorConquest(in ConquestEvaluationContext context);
}
```

---

## 2. Economy, Oasis & Expansion Command Contracts (`IGameCommandGateway`)

Per **Constitutional Principle I & V**, all state-mutating operations are dispatched as immutable `*Command` records through `IGameCommandGateway`:

| Command Record | Target Actor | Validation & State Effect |
| :--- | :--- | :--- |
| **`StartTownHallCelebrationCommand`** | `VillageActor` | `(int ActorPlayerId, int OwnerPlayerId, int VillageId, CelebrationType CelebrationType)` — Validates `Town Hall (gid 24)` level (`>= 1` for `Small`, `>= 10` for `Great`), no active celebration, and sufficient resources. Deducts cost, sets `ActiveCelebrationType` and `CelebrationEndsAtMs`, and schedules `CompleteTownHallCelebrationCommand` timer (which credits flat `500` or `2,000` CP to `PlayerActor`). |
| **`StartBreweryMeadFestivalCommand`** | `VillageActor` | `(int ActorPlayerId, int OwnerPlayerId, int VillageId)` — Validates `TribeId == 2` (Teutons), `IsCapital == true`, `Brewery (gid 35) >= 1`, no active festival, and sufficient resources (`3870/1680/215/10900`). Deducts cost, sets `FestivalEndsAtMs = NowMs + (259200 / Speed) * 1000`, and notifies `PlayerActor` of empire-wide festival window. |
| **`ChangeCapitalVillageCommand`** | `PlayerActor` -> `VillageActor` | `(int ActorPlayerId, int OwnerPlayerId, int NewCapitalVillageId)` — Validates `NewCapitalVillageId` is not already Capital, `IsArchon == false`, and has `Palace (26) >= 1` or `Command Center (44) >= 1`. Applies `CapitalTransferPlan` across old and new Capital `VillageActor`s and triggers an **immediate persistence flush**. |
| **`AbandonAnnexedOasisCommand`** | `VillageActor` -> `OasisActor` | `(int ActorPlayerId, int OwnerPlayerId, int VillageId, int OasisTileId)` — Validates `AnnexedByVillageId == VillageId`. Returns all `Reinforcement` stacks stationed at `OasisTileId` to their `OwnerVillageId`, resets oasis to wild `Nature` (`AnnexedByVillageId = null`, `OwnerPlayerId = Nature`, `Loyalty = 100%`), and recalculates village production. |
| **`FoundNewVillageCommand`** | `MovementSchedulerActor` -> `WorldCoordinatorActor` | `(int LaunchingVillageId, int OwnerPlayerId, int TargetTileId, ResourceCost CarriedResources)` — Triggered when `3` Settlers arrive at `TargetTileId`. If `OccupiedByVillageId != null` or CP/slot check fails, turns Settlers around to return home with their `750` resources. Otherwise consumes `3` Settlers, creates the new village (`gid 15 Lvl 1` on `Slot 26`, `Pop = 2`, `CP = 1`, `Loyalty = 100%`, `750` resources), binds `ExpansionSlot1..3` on `LaunchingVillageId`, and flushes immediately. |
| **`TriggerVillageStarvationCommand`** | `VillageActor` (Timer) | `(int VillageId, long ExpectedTimestampMs)` — Scheduled whenever `NetCrop < 0` and `IsPeaceMode == false`. Lazily accrues crop to `NowMs`; while `Crop <= 0` and `NetCrop < 0`, executes `StarvationCalculator.TryResolveNextStarvationStep`, deletes any emptied `Reinforcement`/`Trapped`/`Movement` records, credits starved unit `CropCost`, and reschedules next starvation timer if `NetCrop` remains `< 0`. |

---

## 3. Domain Error Codes (`TerraviaDomainErrorCodes`)

All domain rejections return deterministic error constants from `TerraviaDomainErrorCodes`:
- `Terravia:Villages:NegativeCropConstructionRestricted`
- `Terravia:Villages:CelebrationAlreadyActive`
- `Terravia:Villages:InsufficientTownHallLevel`
- `Terravia:Villages:MeadFestivalRequiresTeutonCapitalBrewery`
- `Terravia:Villages:CannotMakeWorldWonderVillageCapital`
- `Terravia:Villages:CapitalRequiresPalaceOrCommandCenter`
- `Terravia:Villages:ExpansionUnitQuotaExceeded`
- `Terravia:Villages:InsufficientCulturePointsForExpansion`
- `Terravia:Villages:TargetTileAlreadyOccupied`
- `Terravia:Villages:CannotConquerCapitalOrLastVillage`
- `Terravia:Villages:TargetExpansionBuildingStillStanding`
- `Terravia:Oases:OasisOutOfSevenBySevenRange`
- `Terravia:Oases:NoFreeHeroMansionOasisSlot`
- `Terravia:Oases:OasisNotOwnedByVillage`
