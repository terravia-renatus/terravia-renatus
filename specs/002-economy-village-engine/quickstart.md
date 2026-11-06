# Quickstart & Validation Guide: Core Economy, Village Resource Engine, Oasis Ecology, Starvation Mechanics & Village Expansion Rules

**Feature**: `002-economy-village-engine`  
**Date**: 2026-11-06  
**Spec**: [spec.md](./spec.md) | **Data Model**: [data-model.md](./data-model.md) | **Contracts**: [domain-services.md](./contracts/domain-services.md), [economy-expansion-api.md](./contracts/economy-expansion-api.md)

---

## 1. Prerequisites

- **.NET SDK**: `.NET 10.0` (`dotnet --version` >= `10.0.100`)
- **Node.js & Package Manager**: Node `>= 22` / `pnpm` (for `client/packages/game-contracts` interpolation verification)
- **Feature `001-core-game-catalog`**: Catalog definitions (`IGameCatalog`, `IBuildingRuleCatalog`, `IConstructionCalculator`, and `catalog-data.json`) available in `Terravia.Domain.Catalog`.

---

## 2. Automated Golden-Vector & Virtual-Time Verification (TDD Gate)

Per **Constitutional Principle VI (Test-First & Canonical Mathematical Parity)**, all economy formulas, oasis interactions, starvation tie-breakers, and expansion state machines are verified via deterministic xUnit and `Akka.TestKit` virtual-time suites.

### 2.1 Run Pure Domain Economy, Oasis, Starvation & Expansion Unit Tests
```powershell
dotnet test server/tests/Terravia.Domain.Tests/Terravia.Domain.Tests.csproj --filter "FullyQualifiedName~Villages|FullyQualifiedName~Oases|FullyQualifiedName~Players"
```
**Expected Outcomes**:
- **12 Topologies, Crop Finder & 6-Step Production (`SC-001`, `SC-002`)**:
  - Verifies exact slot distributions (`Slot 1..18`) for `FieldTopologyId 1..12`.
  - Verifies `BestOasisCropBonus` (`0%..150%`) on `9-cropper` (`Type 1`) and `15-cropper` (`Type 6`) tiles across `OasisTypeId 3, 6, 9, 10, 11, 12`.
  - Verifies 6-step `ResourceProductionCalculator` parity (Base Fields + Oasis/Waterworks `gid 45` + Sawmill/Brickyard/Iron Foundry/Grain Mill/Bakery `gid 5..9` + Hero `Balanced` vs `SingleResource` + Plus `+25%` + `Speed`) and strict storage clamping at `[0, WarehouseCap]` / `[0, GranaryCap]`.
- **Oasis Ecology, Hero Cage Capture, Annexation & Manual Abandonment (`SC-003`)**:
  - Verifies wild `Nature` fauna (`u31..u40`) regeneration caps across `RichnessTier 0..2` and $\text{growthFactor} \in [0.3, 3.0]$.
  - Verifies pre-combat Hero Cage (`ItemTypeId = 114`) sequential capture (`u31..u40`), `0` crop upkeep, and starvation exemption.
  - Verifies toroidal `7x7` distance validation, `Hero's Mansion` (`gid 37`) slot unlocking (`1/2/3` at Levels `10/15/20`), loyalty reduction (`33%/50%/100%` per hit), `10%` owning-village raid theft, and manual oasis abandonment returning stationed reinforcements.
- **Deterministic 5-Tier Starvation & Empty-Stack Cleanup (`SC-004`)**:
  - Verifies identical `GrossCrop` evaluation (`FR-003` + `Waterworks` + `Hero`) during starvation, Roman `Horse Drinking Trough` (`gid 41` at `10/15/20` for `u4, u5, u6`), `Archon` World Wonder `50%` upkeep reduction, and `Diet Control` artifact multipliers.
  - Verifies 5-tier priority ordering (`OasisReinforcements -> VillageReinforcements -> TrappedPrisoners -> OwnStationed -> OwnMoving`), strictly largest stack selection (lowest `UnitTypeId` tie-breaker, Hero last in stack), immediate deletion of emptied `Reinforcement`/`Trapped`/`Movement` records, and base `CropCost` credit.
- **Culture Points, Celebrations, Capital Transfer & Expansion (`SC-005`)**:
  - Verifies strictly monotonic `Fast` and `Standard` CP progression curves (`1..125` villages), `0` passive CP from `Archon` WW villages (`IsArchon == true`), `Town Hall` (`gid 24`) Small (`500` CP) and Great (`2,000` CP, `+5%/-5%` loyalty) celebrations, and Teuton `Brewery` (`gid 35`) Mead Festival (`+1%..+10%` attack, random catapults, `50%` Chief persuasion).
  - Verifies expansion unit training slot caps (`Residence 25` `10/20`, `Palace 26` & `Command Center 44` `10/15/20`), Capital transfer field/building downgrades and destructions, newly founded village defaults (`gid 15 Lvl 1` on `Slot 26`, `Pop = 2`, `CP = 1`, `750` resources), and administrator conquest state resets (pre-conquest pop comparison, 8-tribe incompatible building cleanup, active queue cancellation, and annexed oasis release).

### 2.2 Run Actor Virtual-Time Scheduling & Concurrency Tests
```powershell
dotnet test server/tests/Terravia.Actors.Tests/Terravia.Actors.Tests.csproj --filter "FullyQualifiedName~VillageEconomy|FullyQualifiedName~Oasis|FullyQualifiedName~Starvation|FullyQualifiedName~Expansion"
```
**Expected Outcomes**:
- Confirms `VillageActor` schedules exact one-shot `TriggerVillageStarvationCommand` timers when `NetCrop < 0`, chains starvation steps as crop credits deplete, and suppresses starvation when `IsPeaceMode == true`.
- Confirms simultaneous Settler race to the same empty `TileId` founds the village for the first arriving party and safely turns the second party around with all `3` Settlers and `750` of each resource refunded upon return.

### 2.3 Run Client-Side 60fps Resource Interpolation Contract Tests
```powershell
pnpm --filter @terravia/game-contracts test
```
**Expected Outcome**:
- Verifies `interpolateVillageEconomy` matches server-side `ResourceProductionCalculator.AccrueVillageResourcesAndLoyalty` across positive accrual, storage clamping, negative-crop depletion, and loyalty regeneration.
