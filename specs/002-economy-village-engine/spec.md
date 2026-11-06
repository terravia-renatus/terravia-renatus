# Feature Specification: Core Economy, Village Resource Engine, Oasis Ecology, Starvation Mechanics & Village Expansion Rules

**Feature Branch**: `002-economy-village-engine`  
**Created**: 2026-11-06  
**Status**: Ready for Planning  
**Input**: User description: "Develop the core economy, village resource engine, oasis ecology, starvation mechanics, and village expansion rules for Terravia Renatus."

## Clarifications

### Session 2026-11-06

- Q: How should the Culture Point (CP) threshold progression curve behave from Village 2 through Village 125 when unlocking new village expansion? (FR-014) → A: Option A — Enforce a strictly monotonic CP curve where every village $N+1$ requires strictly more CP than village $N$.
- Q: How and when should Town Hall Small and Great Celebrations award Culture Points (CP) to the player? (FR-013) → A: Option A — Award flat `500` CP (Small) and `2,000` CP (Great) upon celebration completion.
- Q: Should wild Nature animals (`u31..u40`) captured from oases via Hero Cages consume village crop upkeep and be subject to starvation when stationed as village defense? (FR-008) → A: Option A — Captured Nature animals (`u31..u40`) consume `0` crop upkeep and are exempt from starvation.
- Q: Which core domain invariants govern Capital transfers, expansion unit training caps, oasis abandonment, loyalty regeneration, founding defaults, conquest persuasion, Crop Finder precomputation, and starvation cleanup? → A: Enforce all 8 domain invariants (Capital Designation & Transfer Side-Effects, Expansion Unit Training Slot Cap with `Command Center gid 44` `10/15/20` parity, Manual Oasis Abandonment & Reinforcement Return, Village Loyalty Regeneration, Newly Founded Village Initial State, Conquest Persuasion Math & Capture State Reset, `BestOasisCropBonus` Crop Finder Precomputation across `OasisTypeId 3, 6, 9, 10, 11, 12`, and Starvation Empty-Stack Cleanup / Unified `GrossCrop` Formula / Archon World Wonder CP Exclusion).
- Q: When a village is conquered (`Loyalty <= 0%`), how should tribe-specific buildings, active queues, and the bigger-player `-1` building level penalty be handled? (FR-017) → A: Option A — Evaluate `AttackerTotalPop > DefenderTotalPop` using pre-conquest account populations for the `-1` level penalty, destroy tribe-exclusive buildings (`gid 35, 36, 41, 44, 45, 48`) when the conquering player's tribe cannot use them (or during cross-player conquest), and cancel all active village queues (`Construction`, `Demolition`, `Training`, `Research`, `Celebrations`) upon capture.
- Q: When a village is conquered (`Loyalty <= 0%`), what should happen to any oases currently annexed by that village? (FR-009) → A: Option A — Release all oases annexed by the conquered village back to wild `Nature` state (`AnnexedByVillageId = null`, `OwnerPlayerId = Nature`, `Loyalty = 100%`) and return any reinforcements stationed at those oases to their home villages.
- Q: Which exact Culture Point (CP) threshold values and formula should Section 5.5 use for the `Standard` and `Fast` expansion curves across Villages `1..125`? (FR-014) → A: Option A — Use the canonical $\text{round}(1600 \times (N - 1)^{2.3}, -3)$ (`Standard`, with `N=2` at `2,000`) and $\text{round}(\frac{1}{3} \times 1600 \times (N - 1)^{2.3}, -2)$ (`Fast`, with `N=2` at `500` and `N=33` fixed to `1,544,700`) strictly monotonic curves across `Villages 1..125`.
- Q: When a starvation priority tier (`Tier 1: Oasis Reinforcements`, `Tier 2: Village Reinforcements`, `Tier 3: Trapped Prisoners`, or `Tier 5: Own Moving Units`) contains multiple non-empty troop stacks, how should `StarvationCalculator` choose which stack to starve a unit from? (FR-012) → A: Option A — Select the first non-empty stack in the active tier ordered by `StackId` (or `MovementId`) ascending (strict FIFO order), then starve `1` unit from the largest unit count (`u1..u90`, lowest unit ID tie-breaker, Hero last) within that stack.
- Q: Which exact buildings are exempt from the low free-crop construction lockout ($\text{GrossCrop} - \text{EffectivePop} - \text{QueuedPop} - \text{CandidateBuildingPop} \le 1$) when queueing construction in `VillageConstructionManager`? (FR-011) → A: Option A — Allow `Cropland` (`gid 4`), any upgrade with `CandidateBuildingPop == 0`, `Grain Mill` (`gid 8`) / `Bakery` (`gid 9`) whose gross crop increase exceeds `CandidateBuildingPop`, and `Warehouse` (`gid 10`) / `Granary` (`gid 11`) when the cheapest `Cropland` upgrade exceeds `WarehouseCap` or `GranaryCap`; reject all other upgrades if $\text{GrossCrop} - \text{EffectivePop} - \text{QueuedPop} - \text{CandidateBuildingPop} \le 1$.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Village Field Topologies, Continuous Resource Accrual, Storage Enforcement & Crop Finder Bonus (Priority: P1)

As a village ruler, I want my village's 18 outer resource fields to match one of the 12 canonical field distributions and continuously produce Wood, Clay, Iron, and Crop based on field levels, annexed oases, bonus buildings, Hero resource allocation, and Plus bonuses—while strictly capping stored resources at my active Warehouse and Granary capacities and accurately precomputing crop oasis potential (`BestOasisCropBonus`) on `9-cropper` and `15-cropper` map tiles—so that my economy progresses deterministically in real time without manual tick polling.

**Why this priority**: Resource production, storage bounds, and crop-tile potential form the foundational economic loop of every village; construction, troop training, celebrations, and upkeep all depend on accurate, continuous resource accrual.

**Independent Test**: Found or inspect villages across all 12 `FieldTopologyId` (`1..12`) distributions, verify `BestOasisCropBonus` (`0%..150%`) on `9-cropper` and `15-cropper` tiles, upgrade resource fields (`0..20`), construct Tier 1/2 resource bonus buildings (`gid 5..9`), annex oases (with and without Egyptian `Waterworks gid 45`), toggle Hero resource modes and Plus bonuses, and verify that hourly production rates and time-elapsed storage accrual match the canonical formulas and clamp strictly at `WarehouseCap` / `GranaryCap`.

**Acceptance Scenarios**:

1. **Given** a newly founded or conquered village on a map tile with a specific `FieldTopologyId` (`1..12`), **When** the village's outer resource slots (`Slot 1..18`) are initialized, **Then** each slot's `BuildingTypeId` (`1`=Woodcutter, `2`=Clay Pit, `3`=Iron Mine, `4`=Cropland) matches the exact 18-slot topology for that `FieldTopologyId` (including `3-3-3-9` 9-cropper `Type 1`, standard `4-4-4-6` `Type 3`, and `1-1-1-15` 15-cropper `Type 6`) at Level `0` (`2`/hr base output per field at `1x` `Speed`).
2. **Given** a village with resource fields at various levels (`0..20`), annexed oases, resource bonus buildings (`Sawmill gid 5`, `Brickyard gid 6`, `Iron Foundry gid 7`, `Grain Mill gid 8`, `Bakery gid 9`), a living stationed Hero with resource points, and an active `+25%` Plus resource bonus, **When** gross hourly production for resource $r$ is evaluated by `ResourceProductionCalculator`, **Then** the engine computes production using the exact 6-step stacking order:
   - Sum base field output $B_r$ across all village fields of resource $r$;
   - Add annexed oasis bonus amplified by Egyptian `Waterworks` (`gid 45` at level $L_{\text{WW}} \in [0..20]$): $A_1 = B_r + B_r \times 0.25 \times (1 + 0.05 \times L_{\text{WW}}) \times O_r$, where $O_r$ is the count of `+25%` oasis bonus units for resource $r$;
   - Apply resource bonus building percentage $P_{\text{bldg}, r}$ (`0%..25%` for Wood/Clay/Iron; `0%..50%` summed across Grain Mill and Bakery for Crop): $A_2 = A_1 \times (1 + P_{\text{bldg}, r} / 100)$;
   - Add stationed living Hero resource contribution $H_r$ (`3` per point in balanced mode `ResourceMode = Balanced`, or `10` per point in single-resource focus `ResourceMode = r`): $A_3 = A_2 + H_r$;
   - Apply active Plus `+25%` bonus for resource $r$: $A_4 = A_3 \times 1.25$ (if active, else $A_3$);
   - Multiply by server `Speed` and round to the nearest integer: $\text{GrossProd}_r = \text{round}(A_4 \times \text{Speed})$.
3. **Given** elapsed time $\Delta t$ (in seconds) since `LastUpdatedMs`, **When** `VillageActor` resources are evaluated or mutated, **Then** `Wood`, `Clay`, and `Iron` increase by $(\text{GrossProd}_r / 3600) \times \Delta t$ clamped to $[0, \text{WarehouseCap}]$, and `Crop` changes by $(\text{NetCrop} / 3600) \times \Delta t$ clamped to $[0, \text{GranaryCap}]$, with any production exceeding storage capacity permanently lost.
4. **Given** a village with zero or more `Warehouse` (`gid 10`), `Granary` (`gid 11`), `Great Warehouse` (`gid 38`), and `Great Granary` (`gid 39`) buildings, **When** storage capacity is calculated, **Then** total capacity equals the sum of capacities of all standing storage buildings of that category (`gid 10 + gid 38` for `WarehouseCap`, `gid 11 + gid 39` for `GranaryCap`) multiplied by `StorageMultiplier`, or `BaseStorageCapacity` (`800 * StorageMultiplier`) when no storage building of that category exists at Level `>= 1`.
5. **Given** any `9-cropper` (`FieldTopologyId = 1`) or `15-cropper` (`FieldTopologyId = 6`) tile on the world map, **When** `MapTiles.BestOasisCropBonus` is computed for the Crop Finder, **Then** the engine scans all oases within the `7x7` toroidal neighborhood ($|\Delta x| \le 3 \land |\Delta y| \le 3$), assigns `+50%` to `OasisTypeId = 12` and `+25%` to **all five** `+25%` crop-bearing oasis types (`OasisTypeId = 3, 6, 9, 10, 11`), and sums the top `3` highest crop oasis bonuses (`0%` to `150%`).

---

### User Story 2 - Oasis Ecology, Raiding, Animal Capture, Annexation & Manual Abandonment (Priority: P1)

As an expanding commander, I want to interact with wild and player-held oases within a `7x7` radius around my village—raiding them for resources, capturing wild `Nature` fauna using Hero Cages, annexing/conquering them via Hero attacks and `Hero's Mansion` slots, or manually abandoning an annexed oasis to free a slot—so that I can boost my village's resource production, defend my territory, and contest rival economies.

**Why this priority**: Oases provide up to `+150%` (or `+300%` with level 20 Egyptian `Waterworks`) resource production scaling, early-game animal defense via Cages, and a core mid-game territorial PvP loop.

**Independent Test**: Spawn wild oases of `OasisTypeId 1..12`, trigger periodic `Nature` troop and resource regeneration, send a Hero equipped with Cages to an unoccupied oasis, clear an unoccupied oasis and annex it with a Hero from a village with `Hero's Mansion` (`gid 37`) at levels `10`, `15`, and `20`, build an Egyptian `Waterworks` (`gid 45`), conquer an enemy-annexed oasis by reducing its loyalty to `0%`, and manually abandon an annexed oasis from `Hero's Mansion` verifying that stationed reinforcements return home.

**Acceptance Scenarios**:

1. **Given** an unoccupied wild oasis (`AnnexedByVillageId == null`, owned by `Nature` `Tribe 4`), **When** periodic oasis regeneration runs on `OasisActor`, **Then** its defending `Nature` fauna (`u31..u40`) regenerate toward the oasis type's (`1..12`) and `RichnessTier`'s (`0, 1, 2`) cap scaled by the server progression growth factor $\text{growthFactor} = \min(3.0, \max(0.3, \text{avgPlayerProgress} / 1000))$, and its stored resources accrue at `10 * Speed` per hour (or `20 * Speed` per hour for `+50%` crop oasis `OasisTypeId = 12`) up to a capacity of `800`.
2. **Given** a Hero attacking an unoccupied oasis while carrying unequipped Cages (`ItemTypeId = 114`) in the player's inventory, **When** the attack arrives, **Then** up to `1` defending `Nature` animal per Cage is captured sequentially across `u31..u40` **before** combat begins, the consumed Cages are removed from inventory, the captured animals are immediately stationed as non-recallable `Nature` defensive reinforcements (`StackType = Reinforcement`, `OwnerVillageId = 0`) in the attacking Hero's home village with `0` crop upkeep (and exempt from starvation), and no combat occurs if all defending animals were captured or the Hero was sent alone with Cages.
3. **Given** a village with a `Hero's Mansion` (`gid 37`) at Level `10`, `15`, or `20` (granting `1`, `2`, or `3` oasis slots via $\lfloor (\text{Level} - 5) / 5 \rfloor$) and an unoccupied oasis within a `7x7` square centered on the village ($|\Delta x| \le 3$ and $|\Delta y| \le 3$ with toroidal wraparound), **When** an attack including the village's surviving Hero leaves the oasis with `0` defending troops and the village has an open oasis slot, **Then** the oasis is immediately annexed (`AnnexedByVillageId = VillageId`, `Loyalty = 100%`) and its resource bonus is applied to the village.
4. **Given** a player-annexed oasis (`AnnexedByVillageId != null`) within the `7x7` neighborhood of an attacker's village that has a free `Hero's Mansion` oasis slot, **When** the attacker's surviving Hero strikes the oasis in a `NormalAttack` and all defending troops are eliminated, **Then** each hit reduces the oasis's loyalty by $\lfloor 100 / \min(3, 4 - N_{\text{defOases}}) \rfloor$ percentage points (where $N_{\text{defOases}}$ is the number of oases currently owned by the defending village, yielding `33%` if owning 1, `50%` if owning 2, or `100%` if owning 3), and when loyalty drops to `0%` (or was already below the single-hit threshold), the oasis transfers ownership to the attacking village.
5. **Given** a raid or normal attack that loots a player-annexed oasis, **When** surviving attackers collect bounty, **Then** up to `10%` of each resource currently stored in the owning village (`floor(villageStock / 10)`) is exposed to looting and deducted directly from the owning village's storage.
6. **Given** a village that owns an annexed oasis (`AnnexedByVillageId == VillageId`), **When** the player manually abandons (releases) that oasis via the `Hero's Mansion` (`gid 37`), **Then** all `Reinforcement` troop stacks stationed at that specific oasis are immediately returned to their respective `OwnerVillageId` home villages, the oasis is reset to wild `Nature` state (`AnnexedByVillageId = null`, `OwnerPlayerId = Nature`, `Loyalty = 100%`), and the village's production rates are immediately recalculated.

---

### User Story 3 - Net Crop Upkeep, Starvation Lockout & Deterministic Starvation Resolution (Priority: P1)

As a game server operator and player, I want net crop production to account for village population and all garrisoned or village-fed troops (including Roman `Horse Drinking Trough`, `Archon` World Wonder village, and `Diet Control` artifact modifiers), restrict non-crop construction and troop training during negative crop, and deterministically starve troops (while cleaning up emptied stacks/movements and using the exact same `GrossCrop` formula) when stored crop hits `0` while net crop is negative, so that over-garrisoned armies cannot be sustained indefinitely without food.

**Why this priority**: Crop upkeep and starvation are the primary limiting mechanics on army size and defensive stacking; any non-determinism, orphaned empty stack, or formula divergence in starvation order breaks combat and economic balance.

**Independent Test**: Garrison own troops, foreign village reinforcements, oasis reinforcements, trapped prisoners, and outgoing troops in a village until net crop is negative; verify construction/training restrictions; let stored crop reach `0`; and verify that `StarvationCalculator` eliminates units one at a time in the exact 5-tier priority order, picks the largest stack on ties, leaves heroes last in their group, deletes empty reinforcement/prisoner/movement records when their last unit starves, and credits the starved unit's base crop training cost back to village storage.

**Acceptance Scenarios**:

1. **Given** a village with gross hourly crop production $\text{GrossCrop}$ (computed identically by `ResourceProductionCalculator` in both normal ticks and starvation loops, including `Waterworks gid 45` and stationed `Hero` crop output), village building population $\text{Population}$, and troops fed by the village, **When** net hourly crop production $\text{NetCrop}$ is calculated, **Then**:
   - Effective population consumption is $\lceil \text{Population} / 2 \rceil$ if the village is an `Archon` World Wonder village (`IsArchon == true`), otherwise $\text{Population}$;
   - Total troop upkeep sums the per-unit crop consumption of all own troops stationed in the village (`StackType = Home`), own troops in transit (`Movements`), foreign reinforcements stationed in the village (`StackType = Reinforcement`, excluding captured `Nature` animals `u31..u40` with `OwnerVillageId == 0`), foreign reinforcements stationed in the village's annexed oases, and prisoners trapped in the village's Gaul `Trappers` (`StackType = Trapped`);
   - Roman cavalry (`Equites Scout u4`, `Equites Eques u5`, `Equites Cataphract u6`) fed by a village with a `Horse Drinking Trough` (`gid 41`) have their base crop upkeep reduced by `1` at Trough levels `>= 10` (`u4`), `>= 15` (`u5`), and `>= 20` (`u6`);
   - All troops fed by an `Archon` World Wonder village (`IsArchon == true`) have their effective upkeep halved (`50%`);
   - Active `Diet Control` artifacts multiply total troop upkeep accordingly (`0.5x` or `0.75x`), rounded to the nearest integer;
   - $\text{NetCrop} = \text{GrossCrop} - \text{EffectivePop} - \text{TotalTroopUpkeep}$.
2. **Given** a village where free crop after accounting for active building and demolishing queues would be `<= 1` ($\text{GrossCrop} - \text{EffectivePop} - \text{QueuedPop} - \text{CandidateBuildingPop} \le 1$), **When** a player attempts to queue a building upgrade that is NOT exempt—where exempt upgrades are strictly `Cropland` (`gid 4`), any upgrade level with `CandidateBuildingPop == 0`, `Grain Mill` (`gid 8`) or `Bakery` (`gid 9`) whose gross crop increase exceeds `CandidateBuildingPop`, or `Warehouse` (`gid 10`) / `Granary` (`gid 11`) when the cheapest `Cropland` upgrade cost exceeds current `WarehouseCap` or `GranaryCap`—OR attempts to train troops whose resource cost or crop upkeep cannot be supported, **Then** the action is rejected by `VillageConstructionManager`.
3. **Given** a village with stored `Crop <= 0` and $\text{NetCrop} < 0$ outside of server Peace mode (`IsPeaceMode == false`), **When** `StarvationCalculator` executes, **Then** troops are selected for starvation strictly in the following 5-tier priority order:
   - **Priority 1**: Foreign/allied reinforcements stationed at the village's annexed oases;
   - **Priority 2**: Foreign/allied reinforcements stationed inside the village itself (excluding captured `Nature` rows `OwnerVillageId == 0`);
   - **Priority 3**: Prisoners trapped inside the village's Gaul `Trappers` (`StackType = Trapped`);
   - **Priority 4**: The village's own stationed troops (`StackType = Home`);
   - **Priority 5**: The village's own troops currently away on outgoing/returning `Movements`.
4. **Given** a starvation tier with one or more troop stacks, **When** a single unit is chosen to starve within the active tier, **Then** the engine:
   - Selects the first non-empty stack in that tier ordered by `StackId` (or `MovementId`) ascending (strict FIFO arrival/creation order);
   - Within that stack, selects the unit type (`u1..u90`) with the **strictly largest stack count** (breaking ties by lowest unit index `u1..u90`);
   - Starves a `Hero` **only** if all regular units (`u1..u90`) in that stack have reached `0` (and marks the starved Hero `IsDead = true, Health = 0`);
   - Decrements the chosen unit by `1` (and decrements `1` occupied trap if starving a `Trapped` prisoner);
   - If the active `Reinforcement` stack, `Trapped` prisoner stack, or in-flight `Movement` now has `0` total units (and `0` Hero) remaining, **immediately deletes** that empty `VillageTroopStack` row or cancels the empty `Movement`;
   - Immediately credits the unit's base crop training cost (or `6` crop for a Hero) to the village's stored `Crop` (clamped to `GranaryCap`), repeating unit-by-unit only while stored `Crop <= 0` and $\text{NetCrop} < 0$.

---

### User Story 4 - Culture Points (CP), Celebrations, Mead Festival, Capital Transfer & Village Expansion (Priority: P2)

As an empire builder, I want my villages to passively generate Culture Points (CP) from buildings, Hero helmets, and Alliance Philosophy bonuses (while excluding `Archon` World Wonder villages from passive CP), host `Town Hall` Small/Great Celebrations and Teuton `Brewery` Mead Festivals, manage Capital designation and transfer side-effects, and enforce CP thresholds, expansion building slots (`Residence gid 25`, `Palace gid 26`, `Command Center gid 44`), expansion unit training caps, loyalty regeneration, founding defaults, and conquest state resets, so that empire growth follows strategic pacing and combat trade-offs.

**Why this priority**: Culture Points, expansion slot accounting, capital transfer rules, and conquest state transitions govern multi-village progression, capital management, and late-game conquest pacing.

**Independent Test**: Upgrade buildings across normal and `Archon` WW villages and verify daily CP recount (`CulturePointsPerDay`), equip a Hero CP helmet, run a Small Celebration (`gid 24` Level `>= 1`) and Great Celebration (`gid 24` Level `>= 10`), trigger a Teuton Mead Festival (`gid 35` in Capital), transfer Capital status between two villages via `Palace gid 26` or `Command Center gid 44` and verify field/building downgrades and destructions, verify Settler/Chief training slot caps, and execute both new-tile settlement (`3` Settlers + `750` of each resource) and administrator conquest above and below the required CP threshold and expansion slot limits.

**Acceptance Scenarios**:

1. **Given** a village with buildings in `Slot 1..40`, **When** the village's passive Culture Point rate (`CulturePointsPerDay`) is recounted, **Then**:
   - If the village is a conquered `Archon` World Wonder village (`IsArchon == true`), it contributes `0` passive daily Culture Points to the player's account CP progression;
   - For all non-`Archon` villages (`IsArchon == false`), base daily CP equals the sum of building `cp` values across `Slot 1..40`, which accrues continuously over elapsed time $\Delta t$ scaled by any active Alliance Philosophy bonus (`+2%..+10%`), plus the flat daily CP bonus from an equipped Hero Gladiator/Tribune/Consul Helmet (`100..2,400` CP/day scaled by server `Speed`).
2. **Given** a village with a `Town Hall` (`gid 24`) and no celebration currently active, **When** the player starts a **Small Celebration** (`CelebrationType = Small`, cost `6,400` Wood, `6,650` Clay, `5,940` Iron, `1,340` Crop) or a **Great Celebration** (`CelebrationType = Great`, requires `Town Hall` Level `>= 10`, cost `29,700` Wood, `33,250` Clay, `32,000` Iron, `6,700` Crop), **Then**:
   - Resources are deducted immediately and the celebration runs for `SmallCelebrationDuration[level] / Speed` or `GreatCelebrationDuration[level] / Speed` seconds;
   - While a **Great Celebration** is actively running, every administrator (`Senator u9`, `Chief u19`, `Chieftain u29`, `Archon Hierophant u49`, `Tribal Chief u59`, `Nomarch u69`, `Ephor u79`, `Jarl u89`) attacking **from** that village gains `+5%` loyalty reduction per administrator, and every enemy administrator attacking **into** that village suffers `-5%` loyalty reduction per administrator;
   - Upon completion, the player's account is awarded a flat `500` CP for a Small Celebration or a flat `2,000` CP for a Great Celebration.
3. **Given** a Teuton player (`Tribe 2`) with a `Brewery` (`gid 35`, levels `1..10`) constructed in their Capital village (`IsCapital == true`) and no festival currently active, **When** the player starts a **Mead Festival** (cost `3,870` Wood, `1,680` Clay, `215` Iron, `10,900` Crop), **Then** the festival runs for a fixed `259,200 / Speed` seconds (`72` hours at `1x` speed) and applies three empire-wide effects to all attacks originating from any of that player's villages while active:
   - Total attacking combat points are increased by `+1%` per `Brewery` level (`+1%` to `+10%`);
   - All attacking catapults (both primary and secondary target slots) are forced to target random buildings;
   - Attacking Teuton `Chief` (`u19`) loyalty reduction is halved (`50%`).
4. **Given** a non-Capital, non-`Archon` World Wonder village (`IsCapital == false`, `IsArchon == false`) with a `Palace` (`gid 26`, Level `>= 1`) or Hun `Command Center` (`gid 44`, Level `>= 1`), **When** the player designates that village as their new Capital, **Then**:
   - In the **previous Capital**: `IsCapital` is set to `false`, every outer resource field (`Slot 1..18`) with `Level > 10` is immediately downgraded to `Level 10`, any `Stonemason's Lodge` (`gid 34`) or Teuton `Brewery` (`gid 35`) is completely destroyed (`Level 0`), and any active construction queue items targeting `Slot 1..18` above Level `10`, `gid 34`, or `gid 35` are cancelled;
   - In the **new Capital**: `IsCapital` is set to `true`, any non-capital-only buildings (`Great Barracks gid 29`, `Great Stable gid 30`, `Great Warehouse gid 38`, `Great Granary gid 39`, `Great Workshop gid 49`) are completely destroyed (`Level 0`), and any active construction queue items targeting those buildings are cancelled;
   - Both villages immediately recalculate `Population`, `CulturePointsPerDay`, `WarehouseCap`, and `GranaryCap` (clamping stored resources if storage capacity dropped).
5. **Given** a village with an expansion building (`Residence gid 25` unlocking `1` slot at Level `10` and `2` slots at Level `20`; `Palace gid 26` or Hun `Command Center gid 44` unlocking `1` slot at Level `10`, `2` slots at Level `15`, and `3` slots at Level `20`), **When** the player queues `Settler` (`u{tribe}0`) or administrator (`Senator`/`Chief`/`Chieftain`/`Archon Hierophant`/`Tribal Chief`/`Nomarch`/`Ephor`/`Jarl`) training in that village, **Then** each unlocked expansion slot permits at most `3` Settlers OR `1` Administrator (`TotalCapacityInSettlerUnits = UnlockedSlots * 3`, where `1` Administrator = `3` Settler-equivalents), deducting all expansion slots already consumed by `ExpansionSlot1VillageId..ExpansionSlot3VillageId` AND all Settlers/Administrators belonging to that village that are currently stationed at home, queued in training, marching/returning in `Movements`, reinforcing other tiles, or trapped in enemy `Trappers`.
6. **Given** a village with `Loyalty < 100%` and a standing `Residence` (`gid 25`), `Palace` (`gid 26`), or `Command Center` (`gid 44`) at level $L \ge 1$, **When** time $\Delta t$ elapses, **Then** the village's `Loyalty` regenerates continuously toward `100%` at a rate of $L \times \text{Speed}$ percentage points per hour (remaining frozen if $L = 0$).
7. **Given** `3` Settlers arriving at an unoccupied map tile with `750` Wood, `750` Clay, `750` Iron, and `750` Crop when the launching village has a free expansion slot and the player meets the `(currentVillageCount + 1)`-th village CP threshold, **When** the new village is founded, **Then** the `3` Settlers are consumed, the new village is initialized with `Main Building` (`gid 15`) at `Level 1` on `Slot 26` (all other `Slot 1..18` at `Level 0` matching the tile's `FieldTopologyId`), `Population = 2`, `CulturePointsPerDay = 1`, `Loyalty = 100%`, `IsCapital = false`, `750` of each resource (`Wood, Clay, Iron, Crop`), and `WarehouseCap = GranaryCap = BaseStorageCapacity` (`800 * StorageMultiplier`), and the new `VillageId` is recorded in the launching village's first open expansion slot (`ExpansionSlot1VillageId..ExpansionSlot3VillageId`).
8. **Given** surviving administrators (`u9, u19, u29, u49, u59, u69, u79, u89`) in a `NormalAttack` against a conquerable village (not the defender's sole village unless `IsArchon == true`, `IsCapital == false`, not an `Archon` WW Building Plan village, and no standing `Residence gid 25`, `Palace gid 26`, or `Command Center gid 44`) when the attacker's launching village has a free expansion slot and the attacker meets the `(currentVillageCount + 1)`-th village CP threshold, **When** loyalty reduction is evaluated by `SiegeAndConquestCalculator`, **Then**:
   - Each surviving administrator reduces village `Loyalty` by:
     $$\Delta \text{Loyalty}_{\text{perAdmin}} = \frac{\text{BasePersuasion} + \text{AttackerGreatCelBonus} - \text{DefenderGreatCelBonus}}{\text{MoraleBonus}} \times M_{\text{MeadFestival}}$$
     where $\text{BasePersuasion} \in [20, 30]$ for Roman `Senator` (`u9`) and $\in [20, 25]$ for all other tribes' administrators (`u19, u29, u49, u59, u69, u79, u89`), $\text{AttackerGreatCelBonus} \in \{0, 5\}$, $\text{DefenderGreatCelBonus} \in \{0, 5\}$, $\text{MoraleBonus} \ge 1.0$, and $M_{\text{MeadFestival}} = 0.5$ if the attacker has an active Teuton Mead Festival (else `1.0`);
   - When `Loyalty` reaches `0%`, `1` attacking administrator is consumed, **pre-conquest** account populations (`AttackerTotalPop > DefenderTotalPop`) are compared to apply the `-1` building level penalty, any tribe-exclusive buildings (`Brewery gid 35`, `Trapper gid 36`, `Horse Drinking Trough gid 41`, `Command Center gid 44`, `Waterworks gid 45`, `Big Hospital gid 48`) incompatible with the conquering player's tribe are destroyed (`Level 0`), the defensive Wall (`Slot 40`) is destroyed (`Level 0`), all oases annexed by the conquered village are immediately released back to wild `Nature` state (`AnnexedByVillageId = null`, `OwnerPlayerId = Nature`, `Loyalty = 100%`) with their stationed reinforcements returned home, all active village queues (`Construction`, `Demolition`, `Training`, `Research`, `Celebrations`), Blacksmith/Armoury unit upgrades, Academy unit research, `Hospital` wounded & healing queues, active Trade Routes, and the village's own troop stacks (`Home`, `Trapped`, and outgoing reinforcements) are cleared/returned, any previous parent village's `ExpansionSlot` pointing to this village is freed, and this village's `VillageId` is bound to the attacker's launching village's first free expansion slot (`ExpansionSlot1VillageId..ExpansionSlot3VillageId`).

---

### Edge Cases

- **Simultaneous Settler Race to the Same Empty Tile**: If two settler parties are sent to the same unoccupied map tile (`OccupiedByVillageId == null`), the first party to arrive founds the village and claims the tile (`OccupiedByVillageId = newVillageId`); when the second party arrives and detects `OccupiedByVillageId != null`, it does not overwrite the village—instead, it automatically turns around and walks back to its home village, returning the `3` Settlers and refunding the `750` Wood, `750` Clay, `750` Iron, and `750` Crop upon arrival.
- **Storage Downgrade or Destruction via Catapults/Demolition/Capital Transfer**: If a `Warehouse`, `Granary`, `Great Warehouse`, or `Great Granary` is damaged, demolished, or destroyed during a Capital transfer while resources are stored, `WarehouseCap` / `GranaryCap` is immediately recalculated (never dropping below `BaseStorageCapacity = 800 * StorageMultiplier`), and any stored resources exceeding the new cap are clamped down to the new maximum capacity.
- **Oasis Retention When Hero's Mansion Is Damaged vs. Release on Village Conquest/Razing**: If a `Hero's Mansion` (`gid 37`) is destroyed or damaged below level `10/15/20`, already-annexed oases remain attached to the village unless manually abandoned, conquered directly by another village, or the owning village itself is conquered (`Loyalty <= 0%`) or razed (`Population == 0`), though no new oasis can be annexed until the Mansion level exceeds current owned oases again. When the owning village is conquered or razed, all oases annexed by that village are immediately released to wild `Nature` state (`AnnexedByVillageId = null`, `OwnerPlayerId = Nature`, `Loyalty = 100%`) and any reinforcements stationed at those oases are returned to their home villages.
- **Village Razing (`Population == 0`) Cleanup & Capital Reassignment**: If a village is completely razed by catapults (`Population == 0` and not a conquerable `Archon` World Wonder village), all its annexed oases are immediately reset to unoccupied wild `Nature` state (`AnnexedByVillageId = null`, `OwnerPlayerId = Nature`, `Loyalty = 100%`), any parent village's `ExpansionSlot1VillageId..ExpansionSlot3VillageId` referencing the razed village is cleared (`null`), and if the razed village was the player's Capital (`IsCapital == true`), Capital status is automatically reassigned to the player's remaining village with the highest `Population`.
- **Conquest of a Village That Previously Founded or Conquered Daughter Villages**: Every village tracks up to 3 expansion slots (`ExpansionSlot1VillageId`, `ExpansionSlot2VillageId`, `ExpansionSlot3VillageId`). When a village is conquered or razed, the parent village that originally founded or conquered it has its corresponding `ExpansionSlot` cleared (`null`), freeing that slot for the parent village to train new Settlers/Administrators.
- **Bigger-Player Conquest Building Downgrade Penalty (`AttackerTotalPop > DefenderTotalPop`) & Tribe-Exclusive Building Cleanup**: Evaluated strictly using **pre-conquest** total account populations (`AttackerTotalPop` vs. `DefenderTotalPop` before transferring village ownership). When a larger player conquers a non-`Archon` village (`IsArchon == false`) from a smaller player, every standing building and resource field in the conquered village drops by `1` level (except `Main Building gid 15` which cannot drop below `Level 1`, and `Brewery gid 35`, `Trapper gid 36`, `Horse Drinking Trough gid 41`, and `Waterworks gid 45` which are completely removed to `Level 0`). Furthermore, on **any** conquest (regardless of population ratio), any tribe-exclusive building (`gid 35, 36, 41, 44, 45, 48`) that is incompatible with the new owner's tribe as well as the defensive Wall (`Slot 40`) is completely destroyed (`Level 0`).
- **World Wonder Village Capital Prohibition**: An `Archon` World Wonder village (`IsArchon == true` / containing a `World Wonder gid 40` in `Slot 99`) can **never** be designated as a player's Capital (`IsCapital` MUST remain `false` even if a `Palace gid 26` or `Command Center gid 44` is constructed inside it).
- **Starvation With Zero Regular Units Left in a Stack & Empty Stack Deletion**: A `Hero` stationed in a reinforcement stack, trapped prisoner stack, own home stack, or in-flight movement is immune to starvation as long as at least `1` regular unit (`u1..u90`) exists in that same stack; the Hero starves (marked `IsDead = true, Health = 0`, crediting `6` crop) only when all regular units in that stack are `0` and the stack is the highest-priority non-empty starvation candidate. Whenever the last unit (or Hero) in a `Reinforcement` stack, `Trapped` prisoner stack, or `Movement` starves to `0`, the empty record MUST be deleted immediately.
- **Peace Mode Starvation Suppression**: When server-wide Peace mode is active (`IsPeaceMode == true`), starvation execution is suspended so no units die of hunger during ceasefires.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001 (12 Village Field Topologies & Crop Finder Precomputation)**:
  - The system MUST support all 12 canonical village field distributions (`FieldTopologyId 1..12`) across outer slots `Slot 1..18`, mapping each slot to `Woodcutter` (`gid 1`), `Clay Pit` (`gid 2`), `Iron Mine` (`gid 3`), or `Cropland` (`gid 4`) according to Section 1 of the Canonical Domain Reference below.
  - For all `9-cropper` (`FieldTopologyId = 1`) and `15-cropper` (`FieldTopologyId = 6`) map tiles, the system MUST precompute `MapTiles.BestOasisCropBonus` (`0%..150%`) as the sum of the top `3` crop-bearing oases within the `7x7` toroidal radius, counting `OasisTypeId = 12` as `+50%` and **all five** `+25%` crop oasis types (`OasisTypeId = 3, 6, 9, 10, 11`) as `+25%`.
- **FR-002 (Resource Field Level Progression)**: Each outer resource field (`Slot 1..18`) MUST support levels `0..20` (capped at Level `10` in non-capital villages `IsCapital == false` and up to Level `20` in the player's Capital village `IsCapital == true`), producing `2` base resources/hour at Level `0` and scaling up to `2,450` base resources/hour at Level `20` (at `1x` `Speed`) per Section 2.
- **FR-003 (Resource Production Bonus Stacking)**: `ResourceProductionCalculator` MUST compute gross hourly production for each resource (`Wood`, `Clay`, `Iron`, `Crop`) using the deterministic 6-step pipeline defined in Section 2:
  1. Sum base field output $B_r$ across `Slot 1..18`;
  2. Add annexed oasis bonus amplified by Egyptian `Waterworks` (`gid 45`);
  3. Apply resource bonus buildings (`Sawmill gid 5`, `Brickyard gid 6`, `Iron Foundry gid 7`, `Grain Mill gid 8`, `Bakery gid 9`);
  4. Add living stationed Hero resource contribution (`Balanced` `3`/pt each vs `SingleResource` `10`/pt);
  5. Multiply by `1.25` if the player's Plus `+25%` bonus for that resource is active;
  6. Multiply by server `Speed` and round to the nearest integer.
- **FR-004 (Continuous Time-Based Accrual & Storage Clamping)**: Resource stocks MUST accrue continuously as a function of elapsed time $\Delta t$ since `LastUpdatedMs` and MUST be clamped strictly to $[0, \text{WarehouseCap}]$ for `Wood`/`Clay`/`Iron` and $[0, \text{GranaryCap}]$ for `Crop`. Any resources produced beyond storage capacity MUST be discarded.
- **FR-005 (Warehouse & Granary Storage Aggregation)**: Village storage limits (`WarehouseCap` and `GranaryCap`) MUST equal the sum of capacities of all standing `Warehouse` (`gid 10`) and `Great Warehouse` (`gid 38`) buildings for `Wood`/`Clay`/`Iron`, and all standing `Granary` (`gid 11`) and `Great Granary` (`gid 39`) buildings for `Crop`, multiplied by `StorageMultiplier`, with a minimum floor of `BaseStorageCapacity = 800 * StorageMultiplier` when no storage building of that category is present.
- **FR-006 (Oasis Bonus Types & Neighborhood Radius)**: The world map MUST support all 12 oasis types (`OasisTypeId 1..12`), providing single-resource (`+25%` or `+50%` Crop) and dual-resource (`+25%` primary + `+25%` Crop) bonuses. A village MAY only annex oases located within a `7x7` tile square centered on the village ($|\Delta x| \le 3$ and $|\Delta y| \le 3$ accounting for toroidal map wraparound).
- **FR-007 (Wild Oasis Fauna Spawning, Regeneration & Resource Stock)**: Unoccupied oases (`AnnexedByVillageId == null`) MUST be owned by the `Nature` faction (`Tribe 4`), hold base resource stocks up to `800` of each resource, and periodically regenerate defending animal stacks (`u31..u40`) according to `OasisTypeId` (`1..12`), oasis `RichnessTier` (`0, 1, 2`), active player count, and server progression factor $\text{growthFactor} \in [0.3, 3.0]$ as specified in Section 3.
- **FR-008 (Hero Cage Animal Capture)**: When a living Hero attacks an unoccupied oasis while the player holds unequipped Cages (`ItemTypeId = 114`) in their inventory, the engine MUST capture up to `1` animal per Cage sequentially from `u31` through `u40` prior to combat resolution, deduct the used Cages from inventory, and station the captured animals as `Nature` defensive reinforcements (`StackType = Reinforcement`, `OwnerVillageId = 0`) in the Hero's home village. Stationed `Nature` animals (`u31..u40`, `OwnerVillageId = 0`) MUST consume `0` crop/hour upkeep and MUST be exempt from starvation.
- **FR-009 (Oasis Annexation, Loyalty, Egyptian Waterworks & Manual Abandonment)**:
  - A village's `Hero's Mansion` (`gid 37`) MUST unlock `1` oasis slot at Level `10`, `2` slots at Level `15`, and `3` slots at Level `20` ($\lfloor (\text{Level} - 5) / 5 \rfloor$).
  - An unoccupied oasis within the `7x7` radius with `0` remaining defending troops MUST be immediately annexed (`Loyalty = 100%`) when attacked by the village's surviving Hero if the village has an open oasis slot.
  - A player-occupied oasis (`AnnexedByVillageId != null`) attacked in a `NormalAttack` by a surviving Hero from a village within `7x7` that has a free oasis slot and clears all defenders MUST lose $\lfloor 100 / \min(3, 4 - N_{\text{defOases}}) \rfloor$ loyalty per hit (`33%`, `50%`, or `100%`), transferring ownership to the attacker's village when loyalty reaches `0%`.
  - Oasis loyalty MUST regenerate toward `100%` at a rate of $L_{\text{HeroMansion}} \times \text{Speed}$ percent per hour.
  - Egyptian `Waterworks` (`gid 45`, Levels `1..20`) MUST amplify all annexed oasis bonuses in that village by $+5\%$ per level (up to $+100\%$ at Level `20`, doubling each `+25%` oasis bonus unit to `+50%`).
  - A village owner MUST be able to manually abandon an annexed oasis from `Hero's Mansion` (`gid 37`), which immediately returns all `Reinforcement` troop stacks stationed at that oasis to their home villages and resets the oasis to wild `Nature` state (`AnnexedByVillageId = null`, `OwnerPlayerId = Nature`, `Loyalty = 100%`). The same release and reinforcement return MUST occur automatically for all oases annexed by a village whenever that village is conquered (`Loyalty <= 0%`) or razed (`Population == 0`).
- **FR-010 (Net Crop Upkeep & Modifiers)**: Net hourly crop production (`NetCrop`) MUST equal gross hourly crop production (`GrossCrop`, always computed via `FR-003` including `Waterworks gid 45` and stationed `Hero` crop contribution) minus effective village population ($\lceil \text{Population} / 2 \rceil$ in `Archon` World Wonder villages `IsArchon == true`, else $\text{Population}$) minus total troop crop upkeep across all non-`Nature` troops fed by the village (excluding captured `Nature` animals `u31..u40` with `OwnerVillageId == 0`, which consume `0` crop/hour), incorporating Roman `Horse Drinking Trough` (`gid 41` at Levels `10/15/20` for `Equites Scout u4`, `Equites Eques u5`, and `Equites Cataphract u6`), `Archon` World Wonder village `50%` upkeep reduction, and active `Diet Control` artifact multipliers (`0.5x` or `0.75x`).
- **FR-011 (Negative Crop Construction & Training Restrictions)**: `VillageConstructionManager` MUST prevent queueing any building upgrade whose population cost (combined with already-queued building changes) would reduce free crop to `<= 1` ($\text{GrossCrop} - \text{EffectivePop} - \text{QueuedPop} - \text{CandidateBuildingPop} \le 1$), EXCEPT for: (a) `Cropland` (`gid 4`), (b) any building upgrade level with `CandidateBuildingPop == 0`, (c) `Grain Mill` (`gid 8`) or `Bakery` (`gid 9`) when its gross crop increase exceeds `CandidateBuildingPop`, and (d) `Warehouse` (`gid 10`) or `Granary` (`gid 11`) when the cheapest `Cropland` upgrade cost exceeds current `WarehouseCap` or `GranaryCap`. It MUST also cap or block troop training whenever available resources or crop upkeep are insufficient.
- **FR-012 (Deterministic Starvation Resolution & Empty-Stack Cleanup)**: When stored `Crop <= 0` and $\text{NetCrop} < 0$ while `IsPeaceMode == false`, `StarvationCalculator` MUST execute starvation unit-by-unit in the 5-tier priority order (1: Oasis Reinforcements -> 2: Village Reinforcements [excluding captured `Nature` rows `OwnerVillageId == 0`] -> 3: Trapped Prisoners -> 4: Own Stationed Units -> 5: Outgoing/Returning Moving Units). Within the highest-priority non-empty tier, the engine MUST select the first non-empty stack ordered by `StackId` (or `MovementId`) ascending (strict FIFO order), eliminate `1` unit from the largest unit stack (`u1..u90` excluding `u31..u40`, lowest unit ID on ties, `Hero` last only when all regular units in that stack are `0` and marking a starved Hero `IsDead = true, Health = 0`), immediately delete the `VillageTroopStack` (`Reinforcement` or `Trapped`) or cancel the `Movement` if its total unit and Hero count reaches `0`, and credit that unit's base crop training cost (`u{X}.CropCost`, or `6` for a Hero) to village crop storage, repeating until stored `Crop > 0` or $\text{NetCrop} \ge 0$ or no eligible troops remain.
- **FR-013 (Culture Point Generation, Archon WW Exclusion, Celebrations & Mead Festival)**:
  - Each non-`Archon` village (`IsArchon == false`) MUST generate passive daily Culture Points (`CulturePointsPerDay`) equal to the sum of building `cp` values across `Slot 1..40`, modified by active Alliance Philosophy bonuses and supplemented at the account level by equipped Hero CP helmets. Conquered `Archon` World Wonder villages (`IsArchon == true`) MUST contribute `0` passive daily Culture Points to account CP progression.
  - `Town Hall` (`gid 24`) MUST support Small Celebrations (Level `>= 1`, awarding a flat `500` CP on completion) and Great Celebrations (Level `>= 10`, awarding a flat `2,000` CP on completion, `+5%` attacking administrator loyalty reduction from that village, and `-5%` defending loyalty reduction against enemy administrators attacking that village while active).
  - Teuton `Brewery` (`gid 35`, Capital only, Levels `1..10`) MUST support a `72`-hour (`259,200 / Speed` seconds) Mead Festival granting `+1%` combat attack power per `Brewery` level empire-wide, while forcing attacking catapults to target randomly and halving (`50%`) attacking Teuton `Chief` (`u19`) loyalty reduction.
- **FR-014 (Expansion Buildings, Training Slot Caps & Loyalty Regeneration)**:
  - `Residence` (`gid 25`) MUST unlock `1` expansion slot at Level `10` and `2` slots at Level `20`; `Palace` (`gid 26`) and Hun `Command Center` (`gid 44`) MUST unlock `1`, `2`, and `3` expansion slots at Levels `10`, `15`, and `20`.
  - Each unlocked expansion slot (`1..3`) MUST allow training at most `3` Settlers (`u{tribe}0`) OR `1` Administrator (`u9, u19, u29, u49, u59, u69, u79, u89`), deducting all expansion slots already occupied in `ExpansionSlot1VillageId..ExpansionSlot3VillageId` and all Settlers/Administrators belonging to that village that are stationed at home, queued in training, in transit in `Movements`, reinforcing other tiles, or trapped in enemy `Trappers`.
  - Whenever a village has `Loyalty < 100%` and a standing `Residence` (`gid 25`), `Palace` (`gid 26`), or `Command Center` (`gid 44`) at level $L \ge 1$, its `Loyalty` MUST regenerate continuously toward `100%` at $L \times \text{Speed}$ percentage points per hour (and `0%`/hr when $L = 0$).
- **FR-015 (Capital Designation & Transfer Side-Effects)**:
  - A player MAY have at most `1` Capital village (`IsCapital = true`) at any time. An `Archon` World Wonder village (`IsArchon == true`) MUST NEVER be designated as Capital.
  - Designating a new Capital via a `Palace` (`gid 26`, Level `>= 1`) or Hun `Command Center` (`gid 44`, Level `>= 1`) MUST atomically:
    1. Downgrade all outer resource fields (`Slot 1..18`) above Level `10` in the old Capital down to `Level 10`;
    2. Destroy any `Stonemason's Lodge` (`gid 34`) and Teuton `Brewery` (`gid 35`) in the old Capital (`Level 0`);
    3. Destroy any non-capital-only buildings (`Great Barracks gid 29`, `Great Stable gid 30`, `Great Warehouse gid 38`, `Great Granary gid 39`, `Great Workshop gid 49`) in the new Capital (`Level 0`);
    4. Cancel any active construction queue items violating the swapped Capital constraints and recalculate `Population`, `CulturePointsPerDay`, `WarehouseCap`, and `GranaryCap` in both villages.
- **FR-016 (Newly Founded Village Initialization)**: Founding a new village on an unoccupied tile (`OccupiedByVillageId == null`) MUST require an open expansion slot in the launching village, accumulated player CP `>=` the strictly monotonic `(N+1)`-th village threshold, `3` Settlers, and `750` of each resource. Upon arrival, the engine MUST consume the `3` Settlers, initialize the new village with `Main Building` (`gid 15`) at `Level 1` on `Slot 26`, all `Slot 1..18` at `Level 0` matching the tile's `FieldTopologyId`, `Population = 2`, `CulturePointsPerDay = 1`, `Loyalty = 100%`, `IsCapital = false`, `750` of each resource (`Wood, Clay, Iron, Crop`), and `WarehouseCap = GranaryCap = BaseStorageCapacity` (`800 * StorageMultiplier`), and record the new `VillageId` in the launching village's first free `ExpansionSlot`.
- **FR-017 (Village Conquest Persuasion, State Reset & Razing Cleanup)**:
  - Conquering an existing village via surviving administrators (`u9, u19, u29, u49, u59, u69, u79, u89`) in a `NormalAttack` MUST require that the target is not a player's sole village (unless `IsArchon == true`), not a Capital (`IsCapital == false`), not an `Archon` WW Building Plan village, has no standing `Residence`/`Palace`/`Command Center` (`gid 25, 26, 44`), and that the attacker has both an open expansion slot in the launching village and sufficient CP for their `(N+1)`-th village.
  - Each surviving administrator MUST reduce target `Loyalty` by `(BasePersuasion + AttackerGreatCel(5) - DefenderGreatCel(5)) / MoraleBonus * (0.5 if Teuton Mead Festival else 1.0)`, where `BasePersuasion` is `rand(20..30)` for Roman `Senator` (`u9`) and `rand(20..25)` for all other administrators (`u19, u29, u49, u59, u69, u79, u89`).
  - When `Loyalty` drops to `0%`, the engine MUST consume `1` attacking administrator, evaluate `AttackerTotalPop > DefenderTotalPop` using **pre-conquest** account populations to apply the bigger-player `-1` building level penalty (destroying `gid 35, 36, 41, 45` and never dropping `gid 15` below Level `1`), destroy any tribe-exclusive buildings (`gid 35, 36, 41, 44, 45, 48`) incompatible with the conquering player's tribe, transfer village ownership, destroy the defensive Wall (`Slot 40` -> `Level 0`), release all oases annexed by the conquered village back to wild `Nature` state (`AnnexedByVillageId = null`, `OwnerPlayerId = Nature`, `Loyalty = 100%`) and return any reinforcements stationed at those oases to their home villages, cancel all active village queues (`Construction`, `Demolition`, `Training`, `Research`, `Celebrations`), clear all Blacksmith/Armoury upgrades, Academy research, `Hospital` wounded/healing queues, active Trade Routes, and own troop stacks (`Home`, `Trapped`), return/clear outgoing reinforcements, free any prior parent village's `ExpansionSlot`, and bind the conquered `VillageId` to the attacker's launching village's first free `ExpansionSlot`.
  - If a village is completely razed (`Population == 0`), the engine MUST release all its annexed oases to `Nature`, clear its parent village's `ExpansionSlot`, and reassign `IsCapital = true` to the owner's highest-population remaining village if the razed village was a Capital.

---

### Key Entities

- **Village Economy & Expansion State (`Villages` / `VillageActor`)**: Tracks `VillageId`, `TileId`, `OwnerPlayerId`, `FieldTopologyId` (`1..12`), `IsCapital` flag, `IsArchon` (World Wonder village) flag, current resource stocks (`Wood`, `Clay`, `Iron`, `Crop`), storage caps (`WarehouseCap`, `GranaryCap`), `Population`, `CulturePointsPerDay`, `Loyalty` (`0..100`), `LastUpdatedMs` timestamp, active `Town Hall` celebration (`CelebrationEndsAtMs`, `CelebrationType [Small | Great]`), active `Brewery` Mead Festival (`FestivalEndsAtMs`), and 3 expansion slot references (`ExpansionSlot1VillageId`, `ExpansionSlot2VillageId`, `ExpansionSlot3VillageId`).
- **Resource Field & Building Slot (`VillageBuildings`)**: Represents `Slot 1..18` (outer resource fields `gid 1..4`, levels `0..20`), `Slot 19..40` (inner village buildings and defensive wall `Slot 40`, levels `0..20`), and `Slot 99` (`World Wonder gid 40`, levels `0..100`), driving base production, storage capacity, bonus percentages, `Population`, and `CulturePointsPerDay`.
- **Map Tile & Oasis State (`MapTiles` & `OasisState` / `OasisActor`)**: Represents `TileId`, coordinates `(X, Y)`, `FieldTopologyId` (`1..12`), `OasisTypeId` (`1..12`), precomputed `BestOasisCropBonus` (`0%..150%`), `RichnessTier` (`0..2`), ownership state (`AnnexedByVillageId = null` for wild `Nature` `Tribe 4`, or `AnnexedByVillageId = parentVillageId` for annexed), `Loyalty` (`0..100`), resource stocks (`Wood`, `Clay`, `Iron`, `Crop` up to `800`), `LastUpdatedMs` (resource & loyalty tick), `NextNatureRegenAtMs` (next `Nature` fauna regeneration timestamp), and stationed `VillageTroopStacks`.
- **Starvation Resolution Queue (`StarvationCandidateGroup`)**: Ordered 5-tier collection of `VillageTroopStacks` and `Movements` fed by a village (`OasisReinforcements`, `VillageReinforcements`, `TrappedPrisoners`, `OwnStationedUnits`, `OwnMovingUnits`) evaluated deterministically by `StarvationCalculator` whenever `Crop <= 0` and `NetCrop < 0`.
- **Culture & Expansion Ledger (`PlayerCultureLedger`)**: Tracks a player's accumulated Culture Points, daily CP production across all owned non-`Archon` villages plus Hero helmet and Alliance Philosophy bonuses, and required CP thresholds for village `N + 1` under the server's configured `CulturePointCurveMode` (`Fast` or `Standard`).

---

## Canonical Domain Reference (Complete Economic & Expansion Data)

### 1. All 12 Village Field Topologies (`Slot 1..18`) & Crop Finder Bonus

Resource `BuildingTypeId` codes: `1` = Woodcutter (Wood), `2` = Clay Pit (Clay), `3` = Iron Mine (Iron), `4` = Cropland (Crop).

| `FieldTopologyId` | Distribution (`W-C-I-Crop`) | Common Name | Exact Slot Mapping (`Slot 1` through `Slot 18`) |
| :--- | :--- | :--- | :--- |
| **Type 1** | `3-3-3-9` | **9-Cropper** | `4, 4, 1, 4, 4, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2` |
| **Type 2** | `3-4-5-6` | Iron-Heavy (`5 Iron`) | `3, 4, 1, 3, 2, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2` |
| **Type 3** | `4-4-4-6` | **Standard Balanced** | `1, 4, 1, 3, 2, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2` |
| **Type 4** | `4-5-3-6` | Clay-Heavy (`5 Clay`) | `1, 4, 1, 2, 2, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2` |
| **Type 5** | `5-3-4-6` | Wood-Heavy (`5 Wood`) | `1, 4, 1, 3, 1, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2` |
| **Type 6** | `1-1-1-15` | **15-Cropper** | `4, 4, 1, 3, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 2, 4, 4` |
| **Type 7** | `4-4-3-7` | 7-Cropper (Wood/Clay) | `1, 4, 4, 1, 2, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2` |
| **Type 8** | `3-4-4-7` | 7-Cropper (Clay/Iron) | `3, 4, 4, 1, 2, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2` |
| **Type 9** | `4-3-4-7` | 7-Cropper (Wood/Iron) | `3, 4, 4, 1, 1, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2` |
| **Type 10** | `3-5-4-6` | Clay/Iron (`5 Clay`) | `3, 4, 1, 2, 2, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2` |
| **Type 11** | `4-3-5-6` | Wood/Iron (`5 Iron`) | `3, 1, 1, 3, 1, 4, 4, 3, 3, 2, 2, 3, 1, 4, 4, 2, 4, 4` |
| **Type 12** | `5-4-3-6` | Wood/Clay (`5 Wood`) | `1, 4, 1, 1, 2, 2, 3, 4, 4, 3, 3, 4, 4, 1, 4, 2, 1, 2` |

> **Starting Capital Rule**: A player's initial spawn village is always created on a **Type 3 (`4-4-4-6`)** tile.
> **Crop Finder `BestOasisCropBonus` Rule**: For `Type 1` (`9-Cropper`) and `Type 6` (`15-Cropper`) tiles, `MapTiles.BestOasisCropBonus` (`0%..150%`) equals the sum of the top `3` crop bonuses among all oases within $|\Delta x| \le 3 \land |\Delta y| \le 3$ (toroidal), where `OasisTypeId = 12` yields `+50%` and `OasisTypeId` $\in \{3, 6, 9, 10, 11\}$ each yield `+25%`.

---

### 2. Resource Field Production, Storage Capacities & Bonus Stacking

#### 2.1 Base Hourly Production & Storage Capacity Curve (`Levels 0..20` at `1x` `Speed` / `1x` `StorageMultiplier`)

| Level | Resource Field Output (`gid 1..4` / hr) | Warehouse / Granary Capacity (`gid 10, 11`) | Great Warehouse / Great Granary Capacity (`gid 38, 39`) | Resource Field CP / Day (`gid 1..4`) |
| :---: | :---: | :---: | :---: | :---: |
| **0** | `2` | `800` *(base floor)* | `800` *(base floor)* | `0` |
| **1** | `5` | `1,200` | `3,600` | `1` |
| **2** | `9` | `1,700` | `5,100` | `1` |
| **3** | `15` | `2,300` | `6,900` | `2` |
| **4** | `22` | `3,100` | `9,300` | `2` |
| **5** | `33` | `4,000` | `12,000` | `2` |
| **6** | `50` | `5,000` | `15,000` | `3` |
| **7** | `70` | `6,300` | `18,900` | `4` |
| **8** | `100` | `7,800` | `23,400` | `4` |
| **9** | `145` | `9,600` | `28,800` | `5` |
| **10** | `200` | `11,800` | `35,400` | `6` |
| **11** | `280` | `14,400` | `43,200` | `7` |
| **12** | `375` | `17,600` | `52,800` | `9` |
| **13** | `495` | `21,400` | `64,200` | `11` |
| **14** | `635` | `25,900` | `77,700` | `13` |
| **15** | `800` | `31,300` | `93,900` | `15` |
| **16** | `1,000` | `37,900` | `113,700` | `18` |
| **17** | `1,300` | `45,700` | `137,100` | `22` |
| **18** | `1,600` | `55,100` | `165,300` | `27` |
| **19** | `2,000` | `66,400` | `199,200` | `32` |
| **20** | `2,450` | `80,000` | `240,000` | `38` |

#### 2.2 Tier 1 & Tier 2 Resource Bonus Buildings (`gid 5..9`)

| Building (`gid`) | Target Resource | Prerequisite | Max Level | Bonus Curve (`Levels 1..5`) |
| :--- | :--- | :--- | :---: | :--- |
| **Sawmill (`gid 5`)** | Wood | Woodcutter `Lvl 10`, Main Building `Lvl 5` | `5` | `+5%, +10%, +15%, +20%, +25%` |
| **Brickyard (`gid 6`)** | Clay | Clay Pit `Lvl 10`, Main Building `Lvl 5` | `5` | `+5%, +10%, +15%, +20%, +25%` |
| **Iron Foundry (`gid 7`)** | Iron | Iron Mine `Lvl 10`, Main Building `Lvl 5` | `5` | `+5%, +10%, +15%, +20%, +25%` |
| **Grain Mill (`gid 8`)** | Crop (Tier 1) | Cropland `Lvl 5` | `5` | `+5%, +10%, +15%, +20%, +25%` |
| **Bakery (`gid 9`)** | Crop (Tier 2) | Cropland `Lvl 10`, Main Building `Lvl 5`, Grain Mill `Lvl 5` | `5` | `+5%, +10%, +15%, +20%, +25%` *(sums with Grain Mill up to `+50%`)* |

#### 2.3 Exact Production Formula (`ResourceProductionCalculator`)

For any resource $r \in \{1\text{ (Wood)}, 2\text{ (Clay)}, 3\text{ (Iron)}, 4\text{ (Crop)}\}$:
1. **Base Field Output ($B_r$)**:
   $$B_r = \sum_{s \in \text{Slots}(r)} \text{FieldProd}(\text{Level}_s)$$
2. **Oasis Factor & Annexed Oasis Bonus ($A_1$)**:
   $$\text{OasisFactor} = 0.25 \times (1 + 0.05 \times L_{\text{Waterworks}})$$
   $$A_1 = B_r + B_r \times \text{OasisFactor} \times O_r$$
   where $L_{\text{Waterworks}} \in [0..20]$ is the level of the village's Egyptian `Waterworks` (`gid 45`), and $O_r \in [0..6]$ is the sum of `25%` bonus units for resource $r$ across all oases annexed by this village (`+1` per `+25%` bonus, `+2` for a `+50%` crop oasis).
3. **Bonus Buildings ($A_2$)**:
   $$P_{\text{bldg}, r} = \begin{cases} 5 \times L_{\text{Sawmill}} & r = 1 \\ 5 \times L_{\text{Brickyard}} & r = 2 \\ 5 \times L_{\text{IronFoundry}} & r = 3 \\ 5 \times L_{\text{GrainMill}} + 5 \times L_{\text{Bakery}} & r = 4 \end{cases}$$
   $$A_2 = A_1 + \frac{A_1}{100} \times P_{\text{bldg}, r}$$
4. **Hero Resource Contribution ($A_3$)**:
   If the village owner's Hero is alive (`IsDead == false`) and currently stationed in this village (`CurrentVillageId == VillageId`), with $R_{\text{pts}} \in [0..100]$ points allocated to the `ResourceBonusPoints` attribute and resource mode `ResourceFocusMode` $\in \{0, 1, 2, 3, 4\}$:
   $$H_r = \begin{cases} 3 \times R_{\text{pts}} & \text{if } \text{ResourceFocusMode} = 0 \text{ (balanced all-resource mode)} \\ 10 \times R_{\text{pts}} & \text{if } \text{ResourceFocusMode} = r \text{ (single-resource focus)} \\ 0 & \text{otherwise} \end{cases}$$
   $$A_3 = A_2 + H_r$$
5. **Plus Account Resource Bonus ($A_4$)**:
   If the player's Plus `+25%` bonus for resource $r$ is currently active:
   $$A_4 = A_3 + \frac{A_3}{100} \times 25 = 1.25 \times A_3$$
6. **Server Speed Scaling ($\text{GrossProd}_r$)**:
   $$\text{GrossProd}_r = \text{round}(A_4 \times \text{Speed})$$

---

### 3. Oasis Ecology, Fauna Regeneration, Annexation & Manual Abandonment Rules

#### 3.1 Oasis Types (`1..12`), Resource Bonuses & Spawning Fauna (`Tribe 4: Nature`)

`Nature` unit IDs: `u31` Rat, `u32` Spider, `u33` Snake, `u34` Bat, `u35` Wild Boar, `u36` Wolf, `u37` Bear, `u38` Crocodile, `u39` Tiger, `u40` Elephant.

| `OasisTypeId` | Resource Bonus | Oasis Bonus Units ($O_{\text{wood}}, O_{\text{clay}}, O_{\text{iron}}, O_{\text{crop}}$) | Regen Tick Increment Formula (before `growthFactor` & cap) |
| :---: | :--- | :---: | :--- |
| **Type 1, 2** | `+25%` Wood | `(1, 0, 0, 0)` | `u35`: `5 + rand(0..10)`, `u36`: `0 + rand(0..5)`, `u37`: `0 + rand(0..5)` |
| **Type 3** | `+25%` Wood + `+25%` Crop | `(1, 0, 0, 1)` | `u35`: `5 + rand(0..15)`, `u36`: `0 + rand(0..5)`, `u37`: `0 + rand(0..5)`, `u38`: `0 + rand(0..5)`, `u40`: `0 + rand(0..3)` |
| **Type 4, 5** | `+25%` Clay | `(0, 1, 0, 0)` | `u31`: `10 + rand(0..15)`, `u32`: `5 + rand(0..15)`, `u35`: `0 + rand(0..10)` |
| **Type 6** | `+25%` Clay + `+25%` Crop | `(0, 1, 0, 1)` | `u31`: `15 + rand(0..20)`, `u32`: `10 + rand(0..15)`, `u35`: `0 + rand(0..10)`, `u40`: `0 + rand(0..3)` |
| **Type 7, 8** | `+25%` Iron | `(0, 0, 1, 0)` | `u31`: `10 + rand(0..15)`, `u32`: `5 + rand(0..15)`, `u34`: `0 + rand(0..10)` |
| **Type 9** | `+25%` Iron + `+25%` Crop | `(0, 0, 1, 1)` | `u31`: `15 + rand(0..20)`, `u32`: `10 + rand(0..15)`, `u34`: `0 + rand(0..10)`, `u39`: `0 + rand(0..3)` |
| **Type 10, 11** | `+25%` Crop | `(0, 0, 0, 1)` | `u31`: `5 + rand(0..15)`, `u33`: `5 + rand(0..10)`, `u37`: `0 + rand(0..10)`, `u38`: `0 + rand(0..5)`, `u39`: `0 + rand(0..5)` |
| **Type 12** | `+50%` Crop | `(0, 0, 0, 2)` | `u31`: `10 + rand(0..15)`, `u33`: `5 + rand(0..10)`, `u37`: `0 + rand(0..10)`, `u38`: `0 + rand(0..5)`, `u39`: `0 + rand(0..5)`, `u40`: `0 + rand(0..3)` |

#### 3.2 Fauna Scaling & Richness Tier Caps (`RichnessTier = 0, 1, 2`)

- **Server Progression Growth Factor**:
  $$\text{avgPlayerProgress} = \text{AVG}(\text{Population} + \text{CulturePointsPerDay}) \text{ across all real player villages}$$
  $$\text{growthFactor} = \min(3.0, \max(0.3, \text{avgPlayerProgress} / 1000))$$
- **Per-Unit-Type Stack Caps by Oasis `RichnessTier`**:
  - `RichnessTier = 0`: $\text{minUnits}_0 = \max(5, \lfloor 5 \times \text{growthFactor} \rfloor)$, $\text{maxUnits}_0 = \min(30, \lfloor \text{minUnits}_0 + 5 + 1.5 \times \text{playerCount} \times \text{growthFactor} \rfloor)$
  - `RichnessTier = 1`: $\text{minUnits}_1 = \max(10, \lfloor 10 \times \text{growthFactor} \rfloor)$, $\text{maxUnits}_1 = \min(60, \lfloor \text{minUnits}_1 + 10 + 2.0 \times \text{playerCount} \times \text{growthFactor} \rfloor)$
  - `RichnessTier = 2`: $\text{minUnits}_2 = \max(20, \lfloor 20 \times \text{growthFactor} \rfloor)$, $\text{maxUnits}_2 = \min(90, \lfloor \text{minUnits}_2 + 15 + 3.0 \times \text{playerCount} \times \text{growthFactor} \rfloor)$
- **Regeneration Schedule**: Whenever an unoccupied oasis (`AnnexedByVillageId == null`) reaches `now >= NextNatureRegenAtMs`, its fauna counts are incremented by the table above and `NextNatureRegenAtMs` is advanced by `NatureRegenIntervalSeconds`.

#### 3.3 Oasis Annexation, Loyalty, Theft & Manual Abandonment Summary

| Rule Parameter | Unoccupied Wild Oasis (`AnnexedByVillageId == null`) | Player-Annexed Oasis (`AnnexedByVillageId != null`) |
| :--- | :--- | :--- |
| **Distance Requirement** | Within `7x7` square ($|\Delta x| \le 3 \land |\Delta y| \le 3$) of attacking village | Within `7x7` square ($|\Delta x| \le 3 \land |\Delta y| \le 3$) of attacking village |
| **Hero's Mansion (`gid 37`) Requirement** | `floor((MansionLevel - 5) / 5) > AttackerOwnedOases` (`Lvl 10`->1, `Lvl 15`->2, `Lvl 20`->3) | `floor((MansionLevel - 5) / 5) > AttackerOwnedOases` |
| **Attack Type Required to Annex** | `NormalAttack` or `Raid` with surviving Hero & `0` defenders left | **`NormalAttack` only** with surviving Hero & `0` defenders left |
| **Loyalty Reduction per Hero Hit** | Immediate annexation (`AnnexedByVillageId = attackerVillageId`, `Loyalty = 100%`) | Reduces loyalty by $\lfloor 100 / \min(3, 4 - N_{\text{defOases}}) \rfloor$ (`33%` if defender has 1 oasis, `50%` if 2, `100%` if 3); annexes when `Loyalty` reaches `0%` |
| **Loyalty Regeneration** | Fixed at `100%` | $+1\% \times L_{\text{HeroMansion}} \times \text{Speed}$ per hour up to `100%` |
| **Raidable Resources** | Up to `800` per resource stored in the oasis itself | Up to `10%` of each resource stored in the owning village (`floor(villageStock / 10)`) |
| **Manual Abandonment (`gid 37`)** | N/A | Returns all stationed `Reinforcement` stacks to their `OwnerVillageId`, sets `AnnexedByVillageId = null`, `OwnerPlayerId = Nature`, `Loyalty = 100%`, and recalculates village production |

---

### 4. Crop Upkeep, Starvation Lockout & Starvation Resolution Order

#### 4.1 Troop Crop Upkeep Modifiers
1. **Base Unit Upkeep**: Defined in Feature `001-core-game-catalog` (`u1..u90` crop upkeep, `Hero = 6` crop/hr), **except** captured wild `Nature` animals (`u31..u40` stationed in a village with `OwnerVillageId == 0`), which consume **`0` crop/hr** and are exempt from starvation.
2. **Roman `Horse Drinking Trough` (`gid 41`)**: If the feeding village is Roman (`Tribe 1`) and has a `Horse Drinking Trough` (`gid 41`):
   - `Equites Scout` (`u4`): `-1` crop/hr (from `2` to `1`) at Trough Level `>= 10`;
   - `Equites Eques` (`u5`): `-1` crop/hr (from `3` to `2`) at Trough Level `>= 15`;
   - `Equites Cataphract` (`u6`): `-1` crop/hr (from `4` to `3`) at Trough Level `>= 20`.
3. **`Archon` World Wonder Village Upkeep Halving**: If the feeding village is an `Archon` World Wonder village (`IsArchon == true`), both its building population upkeep ($\lceil \text{Population} / 2 \rceil$) and all troop upkeep inside it are halved (`* 0.5`).
4. **`Diet Control` Artifacts**: Multiply total troop upkeep by the active artifact factor (`0.5` for Great/Unique Diet Control, `0.75` for Small Diet Control) and round to the nearest integer.

#### 4.2 Deterministic 5-Tier Starvation Priority, Tie-Breaking & Empty-Stack Cleanup
When `Crop <= 0`, `NetCrop < 0` (evaluated using the unified `FR-003` `GrossCrop` formula), and `IsPeaceMode == false`:
1. **Tier 1 — Oasis Reinforcements**: Foreign/allied `Reinforcement` stacks stationed at oases annexed by this village.
2. **Tier 2 — Village Reinforcements**: Foreign/allied `Reinforcement` stacks stationed directly in this village (excluding captured `Nature` rows where `OwnerVillageId == 0`).
3. **Tier 3 — Trapped Prisoners**: Prisoner stacks (`StackType = Trapped`) held in this village's Gaul `Trappers`. When a prisoner unit starves, both total traps (`u99`) and occupied traps in the village are decremented by `1`.
4. **Tier 4 — Own Stationed Troops**: The village's own `Home` troop stack (`u1..u90`, `Hero`).
5. **Tier 5 — Own Moving Troops**: Outgoing or returning troop `Movements` belonging to this village.

**Within-Tier FIFO Stack Selection, Within-Row Unit Selection, Empty-Stack Deletion & Crop Credit**:
- Select the first non-empty stack in the highest-priority non-empty tier ordered by `StackId` (or `MovementId`) ascending (strict FIFO order), then scan `u1..u90` in that stack and pick the unit type $u_k$ with the **strictly maximum count** (ties broken by the lowest unit index $k$).
- A `Hero` in that stack is selected **only** if all `u1..u90` in that stack have count `0` (and when starved, the Hero is marked `IsDead = true, Health = 0`).
- Decrement the selected unit by `1`, delete the `Reinforcement`/`Trapped` stack or cancel the `Movement` if its remaining unit + Hero count is `0`, add the starved unit's base training crop cost (`u{k}.CropCost`, or `6` for a Hero) to the village's `Crop` storage (clamped to `GranaryCap`), recompute `NetCrop`, and repeat until `Crop > 0` or `NetCrop >= 0` or all 5 tiers are empty.

---

### 5. Culture Points (CP), Celebrations, Mead Festival, Capital Transfer & Expansion Rules

#### 5.1 Town Hall (`gid 24`) Celebrations (`CelebrationManager`)

| Celebration Type | Required Town Hall Level | Resource Cost (`Wood / Clay / Iron / Crop`) | CP Awarded on Completion | Active Combat / Conquest Modifiers (while running) | Base Duration (`Lvl 1` -> `Lvl 10` -> `Lvl 20` at `1x` `Speed`) |
| :--- | :---: | :---: | :---: | :--- | :--- |
| **Small Celebration (`Small`)** | `Lvl 1` | `6,400 / 6,650 / 5,940 / 1,340` | Flat `500` CP | None | `86,400s` (`24h`) -> `62,117s` (`17h 15m 17s`) -> `43,050s` (`11h 57m 30s`) |
| **Great Celebration (`Great`)** | `Lvl 10` | `29,700 / 33,250 / 32,000 / 6,700` | Flat `2,000` CP | **+5%** loyalty reduction for own Administrators (`u9, u19, u29, u49, u59, u69, u79, u89`) sent from this village; **-5%** loyalty reduction for enemy Administrators attacking this village | N/A below Lvl 10; `155,291s` (`43h 08m 11s` at Lvl 10) -> `107,626s` (`29h 53m 46s` at Lvl 20) |

**Exact Town Hall Celebration Base Durations (Seconds at `1x` `Speed`, divided by `Speed`)**:
- **Small Celebration (`Levels 1..20`)**:
  `1: 86400`, `2: 83290`, `3: 80291`, `4: 77401`, `5: 74614`, `6: 71928`, `7: 69338`, `8: 66843`, `9: 64436`, `10: 62117`, `11: 59880`, `12: 57725`, `13: 55647`, `14: 53643`, `15: 51712`, `16: 49850`, `17: 48056`, `18: 46326`, `19: 44658`, `20: 43050`.
- **Great Celebration (`Levels 10..20`)**:
  `10: 155291`, `11: 149701`, `12: 144312`, `13: 139116`, `14: 134108`, `15: 129280`, `16: 124626`, `17: 120140`, `18: 115815`, `19: 111645`, `20: 107626`.

#### 5.2 Teuton Brewery (`gid 35`) Mead Festival

| Parameter | Canonical Value & Rule |
| :--- | :--- |
| **Tribe & Village Constraint** | **Teutons (`Tribe 2`) only**, constructible and activatable **only in the Capital village (`IsCapital == true`)** (`Levels 1..10`) |
| **Resource Cost** | `3,870` Wood, `1,680` Clay, `215` Iron, `10,900` Crop |
| **Duration** | Fixed `259,200 / Speed` seconds (`72` hours at `1x` speed) regardless of `Brewery` level |
| **Empire-Wide Offensive Bonus** | `+1%` total attack power per `Brewery` level (`+1%` at Lvl 1 up to `+10%` at Lvl 10) for all attacks from any of the player's villages while active |
| **Catapult Trade-Off** | Forces both catapult targeting slots (primary and secondary) to shoot **random targets** |
| **Chief Persuasion Trade-Off** | Halves (`50%`) the loyalty reduction of attacking Teuton `Chiefs` (`u19`) after celebration and morale adjustments |

#### 5.3 Expansion Buildings, Training Slot Caps & Loyalty Regeneration

| Building (`gid`) | Allowed Villages | Max Level | Expansion Slots Unlocked (`S`) | Max Trainable Expansion Units (`3*S` Settler-Equivalents) | Village Loyalty Regeneration Rate |
| :--- | :--- | :---: | :--- | :--- | :--- |
| **Residence (`gid 25`)** | Any village without `Palace` / `Command Center` | `20` | `1` slot at **Level 10**, `2` slots at **Level 20** | `1` slot = `3` Settlers or `1` Admin; `2` slots = `6` Settlers or `2` Admins (or `3` Settlers + `1` Admin) | $+1.0 \times \text{Level} \times \text{Speed}$ % / hr |
| **Palace (`gid 26`)** | Max `1` per account (required to designate Capital; not Huns) | `20` | `1` slot at **Level 10**, `2` slots at **Level 15**, `3` slots at **Level 20** | Up to `9` Settler-equivalents (`3` per unlocked slot) | $+1.0 \times \text{Level} \times \text{Speed}$ % / hr |
| **Command Center (`gid 44`)** | Huns (`Tribe 6`) only, replaces `Palace` & `Residence` | `20` | `1` slot at **Level 10**, `2` slots at **Level 15**, `3` slots at **Level 20** | Up to `9` Settler-equivalents (`3` per unlocked slot) | $+1.0 \times \text{Level} \times \text{Speed}$ % / hr |

#### 5.4 Capital Transfer, Settlement Defaults & Conquest Persuasion Summary

| Operation | Canonical Domain Rules |
| :--- | :--- |
| **Capital Transfer (`gid 26` / `gid 44`)** | Prohibited in `Archon` World Wonder villages (`IsArchon == true`). Old Capital: `IsCapital = false`, outer fields `Slot 1..18` above `Lvl 10` downgraded to `Lvl 10`, `Stonemason's Lodge (gid 34)` and `Brewery (gid 35)` destroyed (`Lvl 0`). New Capital: `IsCapital = true`, non-capital buildings (`Great Barracks gid 29`, `Great Stable gid 30`, `Great Warehouse gid 38`, `Great Granary gid 39`, `Great Workshop gid 49`) destroyed (`Lvl 0`). Recounts `Population`, `CulturePointsPerDay`, `WarehouseCap`, and `GranaryCap` in both villages. |
| **New Village Settlement (`3` Settlers)** | Requires `1` free `ExpansionSlot`, player CP `>=` target village threshold, `3` Settlers, and `750` of each resource. Creates village with `Main Building (gid 15)` `Lvl 1` in `Slot 26`, `Slot 1..18` `Lvl 0`, `Population = 2`, `CulturePointsPerDay = 1`, `Loyalty = 100%`, `IsCapital = false`, `750` of each resource, `WarehouseCap = GranaryCap = BaseStorageCapacity`, and binds `ExpansionSlot` on parent village. |
| **Administrator Persuasion per Surviving Unit** | Roman `Senator (u9)`: `rand(20..30)%`; Teuton `Chief (u19)`, Gaul `Chieftain (u29)`, Archon `Archon Hierophant (u49)`, Hun `Tribal Chief (u59)`, Egyptian `Nomarch (u69)`, Spartan `Ephor (u79)`, Viking `Jarl (u89)`: `rand(20..25)%`. Formula: `(BaseRand + AttackerGreatCel(5) - DefenderGreatCel(5)) / MoraleBonus * (0.5 if Teuton Mead Festival else 1.0)`. |
| **Conquest State Reset (`Loyalty <= 0%`)** | Consumes `1` attacking administrator; compares **pre-conquest** `AttackerTotalPop > DefenderTotalPop` to apply `-1` building level penalty (destroying `gid 35, 36, 41, 45`, keeping `gid 15 >= 1`); destroys tribe-incompatible buildings (`gid 35, 36, 41, 44, 45, 48`) and Wall (`Slot 40` -> `Lvl 0`); releases all annexed oases to `Nature` (`Loyalty = 100%`) and returns oasis reinforcements; cancels all active queues (`Construction`, `Demolition`, `Training`, `Research`, `Celebrations`); clears Blacksmith/Armoury upgrades, Academy research, `Hospital` wounded/queues, Trade Routes, and own troop stacks (`Home`, `Trapped`); returns outgoing reinforcements; frees prior parent's `ExpansionSlot` and binds attacker's launching village `ExpansionSlot`. |

#### 5.5 Culture Point (CP) Progression Curves (`Villages 1..25` + Monotonic Curve for `1..125`)

The engine enforces a **strictly monotonic** CP progression curve (`CulturePointCurveMode = Fast` and `CulturePointCurveMode = Standard`, scaled up to `125` villages where $\text{CP}(N+1) > \text{CP}(N)$ for every $N \in [1..124]$):

| Target Village # (`N`) | Fast / Speed Curve (`Fast[N]`) | Standard Curve (`Standard[N]`) | Target Village # (`N`) | Fast / Speed Curve (`Fast[N]`) | Standard Curve (`Standard[N]`) |
| :---: | :---: | :---: | :---: | :---: | :---: |
| **1** | `0` | `0` | **14** | `194,600` | `584,000` |
| **2** | `500` | `2,000` | **15** | `230,700` | `692,000` |
| **3** | `2,600` | `8,000` | **16** | `270,400` | `811,000` |
| **4** | `6,700` | `20,000` | **17** | `313,700` | `941,000` |
| **5** | `12,900` | `39,000` | **18** | `360,600` | `1,082,000` |
| **6** | `21,600` | `65,000` | **19** | `411,300` | `1,234,000` |
| **7** | `32,900` | `99,000` | **20** | `465,700` | `1,397,000` |
| **8** | `46,000` | `141,000` | **25** | `797,000` | `2,391,000` |
| **9** | `63,000` | `191,000` | **33** | `1,544,700` | `4,634,000` |
| **10** | `83,500` | `251,000` | **50** | `4,115,800` | `12,347,000` |
| **11** | `106,400` | `319,000` | **75** | `10,622,600` | `31,868,000` |
| **12** | `132,500` | `397,000` | **100** | `20,747,200` | `62,242,000` |
| **13** | `161,900` | `486,000` | **125** | `34,823,200` | `104,470,000` |

*(Note: For $N \ge 3$, the curves follow $\text{CP}_{\text{std}}(N) = \text{round}(1600 \times (N - 1)^{2.3}, -3)$ (with $N=2$ at `2,000`) and $\text{CP}_{\text{fast}}(N) = \text{round}(\frac{1}{3} \times 1600 \times (N - 1)^{2.3}, -2)$ (with $N=2$ at `500` and $N=33$ set to `1,544,700`) so that every village $N \in [1..125]$ is strictly increasing without any duplicate or backward threshold.)*

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001 (100% Village Topology, Crop Finder & Resource Formula Parity)**: Automated verification tests confirm that all 12 village field distributions (`FieldTopologyId 1..12`), `BestOasisCropBonus` (`0%..150%` across `OasisTypeId 3, 6, 9, 10, 11, 12`), all 21 resource field levels (`0..20`), all 4 storage buildings (`gid 10, 11, 38, 39`), and the 6-step bonus stacking pipeline (Base Fields + Oasis/Waterworks + Sawmill/Brickyard/Iron Foundry/Grain Mill/Bakery + Hero Resource Mode + Plus `+25%` + `Speed`) match the canonical output values with `0` rounding discrepancies.
- **SC-002 (Storage Cap Enforcement)**: In 100% of resource accrual, merchant delivery, raid return, Capital transfer storage destruction, and starvation crop-credit scenarios, village stored resources never exceed `WarehouseCap` / `GranaryCap` or drop below `0`.
- **SC-003 (Oasis Lifecycle, Annexation & Abandonment Accuracy)**: Automated tests verify all 12 oasis bonus mappings, `7x7` toroidal distance validation, `Hero's Mansion` slot limits (`1/2/3` at Levels `10/15/20`), pre-combat Hero Cage animal capture (`0` crop upkeep & starvation exemption), loyalty reduction (`33%/50%/100%` per hit), Egyptian `Waterworks` amplification (`+5%..+100%`), `10%` owning-village raid theft, and manual oasis abandonment returning stationed reinforcements.
- **SC-004 (Deterministic Starvation & Empty-Stack Cleanup Invariant)**: Given any combination of negative `NetCrop` (evaluated using the unified `FR-003` `GrossCrop` formula) and `0` stored crop across all 5 troop priority tiers, `StarvationCalculator` deterministically eliminates the exact expected unit type (largest stack first, lowest unit ID tie-breaker, Hero last in stack), deletes any `Reinforcement`/`Trapped` stack or cancels any `Movement` whose unit count hits `0`, and credits its exact base crop training cost without race conditions or negative troop counts.
- **SC-005 (Culture Points, Celebrations, Capital Transfer & Expansion Gate Accuracy)**: Verification tests confirm daily CP recount across all 40 building slots (with `0` passive CP from `Archon` World Wonder villages `IsArchon == true`), Small/Great Celebration CP awards and `+5% / -5%` loyalty modifiers, Teuton `Brewery` Mead Festival (`+1%..+10%` attack, random catapults, `50%` Chief persuasion), Capital transfer field/building downgrades and destructions, expansion unit training slot caps (including `Command Center gid 44` `10/15/20` parity), village loyalty regeneration, newly founded village initialization defaults, and administrator conquest persuasion and state resets.

---

## Assumptions

- **Consistency with Feature `001-core-game-catalog`**: All building IDs (`gid 1..50`), unit IDs (`u1..u90`, `u99`), Hero item IDs (`ItemTypeId = 114` Cages, CP helmets), and base unit training costs/upkeep values reference the canonical catalog established in Feature `001-core-game-catalog`.
- **Configurable World Speed & Multipliers**: All production, regeneration, celebration duration, and storage formulas accept `ServerConfig` parameters (`Speed`, `StorageMultiplier`, `BaseStorageCapacity`, `NatureRegenIntervalSeconds`, `IsPeaceMode`, and `CulturePointCurveMode`), defaulting to `1x` standard speed values in baseline tests.
- **Hero Oasis Capture Transport**: Wild `Nature` animals captured with Hero Cages are stationed directly in the attacking Hero's home village as non-recallable `Nature` reinforcements (`StackType = Reinforcement`, `OwnerVillageId = 0`) at the moment of capture.
