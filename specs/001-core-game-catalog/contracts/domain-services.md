# Domain Service & Command Contracts (`Terravia.Domain`)

**Feature**: `001-core-game-catalog`
**Date**: 2026-11-06

## 1. Pure Domain Catalog & Rule Interfaces (`server/src/Terravia.Domain`)

Per **Constitutional Principle II**, all catalog lookups, prerequisite checks, and construction time calculations are exposed through stateless, zero-allocation interfaces in `Terravia.Domain` with zero dependencies on Akka.NET, EF Core, or ASP.NET Core.

```csharp
namespace Terravia.Domain.Catalog;

public interface IGameCatalog
{
    string ContentHash { get; }
    ReadOnlySpan<TribeDefinition> GetAllTribes();
    TribeDefinition GetTribe(int tribeId);

    ReadOnlySpan<UnitDefinition> GetAllUnits();
    UnitDefinition GetUnit(int unitTypeId);
    ReadOnlySpan<UnitDefinition> GetUnitsForTribe(int tribeId);
    bool TryGetHeroTemplate(int unitTypeId, out HeroUnitTemplate template);

    ReadOnlySpan<BuildingDefinition> GetAllBuildings();
    BuildingDefinition GetBuilding(int buildingTypeId);
    BuildingLevelStats GetBuildingLevelStats(int buildingTypeId, int level);
}

public interface IBuildingRuleCatalog
{
    /// <summary>
    /// Evaluates whether a building type (gid 1..50) satisfies all tribe, capital,
    /// prerequisite building levels, uniqueness/rebuildAtLevel, artifact, and WW plan rules.
    /// </summary>
    bool MeetsPrerequisites(int buildingTypeId, in VillagePrerequisiteContext context);

    /// <summary>
    /// Evaluates whether a specific slot can be constructed or upgraded right now,
    /// returning the canonical BuildSlotEvaluation status indicator (Active, WaitingLoop,
    /// MasterBuilder, MaxLevel, WarehouseCap, GranaryCap, CropDeficit, BeingDemolished, etc.).
    /// </summary>
    BuildSlotEvaluation EvaluateSlotUpgrade(
        int slotId,
        int buildingTypeId,
        in VillageConstructionContext context);

    /// <summary>
    /// Evaluates whether a unit type (u1..u90) can be researched in the Academy (gid 22)
    /// given the village's current building levels.
    /// </summary>
    bool MeetsUnitResearchPrerequisites(int unitTypeId, ReadOnlySpan<int> slotBuildingTypes, ReadOnlySpan<int> slotLevels);

    /// <summary>
    /// Evaluates whether an inner building slot (19..40, 99) can be demolished (timed or 10-Gold instant).
    /// Enforces Main Building >= 10, no active/loop build on slot, and Alliance Founder Embassy (gid 18) safeguard.
    /// </summary>
    DemolitionEligibilityResult EvaluateDemolition(
        int slotId,
        bool isInstantGold,
        in VillageDemolitionContext context);
}

public interface IConstructionCalculator
{
    /// <summary>
    /// Calculates exact construction time in seconds for targetLevel, scaled by Main Building (gid 15)
    /// attribute percentage table (or 5x penalty when Main Building == 0) and server Speed multiplier.
    /// </summary>
    int CalculateBuildTimeSeconds(
        int buildingTypeId,
        int targetLevel,
        int mainBuildingLevel,
        double serverSpeed);

    /// <summary>
    /// Calculates timed 1-level demolition duration: floor(CalculateBuildTimeSeconds(gid, currentLevel) / 2.0).
    /// </summary>
    int CalculateDemolitionTimeSeconds(
        int buildingTypeId,
        int currentLevel,
        int mainBuildingLevel,
        double serverSpeed);

    /// <summary>
    /// Recounts total base Culture Points per day across slots 1..40 before Alliance Philosophy bonus.
    /// </summary>
    int CalculateVillageCulturePoints(ReadOnlySpan<int> slotBuildingTypes, ReadOnlySpan<int> slotLevels);

    /// <summary>
    /// Calculates effective hourly crop upkeep for a unit stack, applying Roman Horse Drinking Trough
    /// (gid 41 at levels 10, 15, 20 for u4, u5, u6) and Diet Artifact modifiers.
    /// </summary>
    double CalculateUnitUpkeepPerHour(
        int unitTypeId,
        int count,
        int horseDrinkingTroughLevel,
        double artifactDietMultiplier = 1.0);
}
```

---

## 2. Construction & Demolition Command Contracts (`IGameCommandGateway`)

Per **Constitutional Principle I & V**, players, Sitters, and Utility AI bots dispatch construction and demolition actions exclusively through `IGameCommandGateway` routed to `VillageActor`:

| Command Record | Parameters | Validation & State Effect |
| :--- | :--- | :--- |
| **`ConstructOrUpgradeBuildingCommand`** | `(int ActorPlayerId, int OwnerPlayerId, int VillageId, int SlotId, int BuildingTypeId, bool UseMasterBuilder)` | Validates prerequisites, slot placement (`39`->`16`, `40`->Tribe Wall), Roman vs. Standard concurrency, Plus waiting loop (`IsWaitingLoop = true`), or Master Builder reservation (`IsMasterBuilder = true`). Deducts resources immediately for active/loop jobs; defers resource + `1` Gold deduction for Master Builder until slot activation. |
| **`CancelConstructionJobCommand`** | `(int ActorPlayerId, int OwnerPlayerId, int VillageId, long QueueItemId)` | Removes queued job; refunds `100%` of `ResourceCost` (capped at current storage) if `IsMasterBuilder == false`; refunds `0` if `IsMasterBuilder == true`; promotes remaining `WaitingLoop` job (`IsWaitingLoop = false`) and recalculates queued timestamps/levels on the same slot; resets `SlotBuildingType = 0` if a Level 1 new construction is cancelled. |
| **`FinishAllConstructionsWithGoldCommand`** | `(int ActorPlayerId, int OwnerPlayerId, int VillageId)` | Requires `SITTER_PERM_GOLD` if Sitter. Blocked completely if village has World Wonder (`Slot 99 == 40`). Skips `gid 25, 26, 40`. Activates & finishes eligible Master Builder jobs (deducting resources + `1` Gold) and finishes active/loop builds, active demolition, and active research (`2` Gold, or `3` Gold combined with Master Builder). |
| **`StartBuildingDemolitionCommand`** | `(int ActorPlayerId, int OwnerPlayerId, int VillageId, int SlotId, bool IsInstantCompleteWithGold)` | Requires Main Building (`gid 15`) `>= 10` and `SlotId in 19..40, 99`. Enforces Embassy (`gid 18`) Alliance Founder safeguard. If `IsInstantCompleteWithGold == false`, schedules 1-level demolition timer (`floor(buildTime / 2)`). If `IsInstantCompleteWithGold == true`, charges `10` Gold, razes building directly to `Level 0` (`gid = 0`), subtracts cumulative `pop(1..L)`, and recalculates storage & CP immediately. |
| **`CancelBuildingDemolitionCommand`** | `(int ActorPlayerId, int OwnerPlayerId, int VillageId)` | Cancels the active timed demolition job in the village. |
