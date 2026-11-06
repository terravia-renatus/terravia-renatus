# Feature Specification: Core Game Catalog (Tribes, Units, Buildings & Construction Rules)

**Feature Branch**: `[001-core-game-catalog]`

**Created**: 2026-11-06

**Status**: Draft

**Input**: User description: "Develop the core game catalog for Terravia Renatus comprising all 9 tribes (Romans, Teutons, Gauls, Huns, Egyptians, Spartans, Vikings, Nature, Archons), their 90 distinct unit types, and all 50 building types with their prerequisite trees and level progressions.

The specification must establish:
1. Tribe profiles and cultural bonuses:
   - Romans: Dual-building capability (simultaneous resource field + village center building), high base combat stats, expensive training.
   - Teutons: Aggressive raiders with reduced construction costs and unique Brewery building.
   - Gauls: Defensive masters with expanded Cranny protection and exclusive Trapper building.
   - Huns: Mobile cavalry specialists with the 3-expansion Command Center.
   - Egyptians: Resource-rich builders with powerful stone walls and oasis-amplifying Waterworks.
   - Spartans & Vikings: Specialized military factions with exclusive defensive barriers and Big Hospital access.
   - Nature & Archons: NPC factions for oasis wildlife and endgame World Wonder / Artifact custodianship.

2. Unit Catalog & Attributes:
   - 10 designated units per tribe covering light/heavy infantry, cavalry, scouts, siege engines (rams and catapults), settlers, and cultural conquerors (chiefs/senators).
   - Core baseline attributes for each unit: Wood/Clay/Iron/Crop costs, base training duration, movement speed (tiles/hr), resource carry capacity, combat values (offense, infantry defense, cavalry defense), and hourly crop consumption.

3. Building Catalog & Prerequisite Hierarchy:
   - All 50 building types organized into Resources, Production Boosters, Storage, Military, Walls, and Civic/Expansion.
   - Level 1-20 scaling rules (and Level 1-100 for World Wonder) covering upgrade resource costs, construction times, population yield, and culture point generation.
   - Complete dependency validation rules: building A cannot be constructed unless prerequisite buildings B and C meet required levels, with support for tribe-exclusive building restrictions.

4. Construction & Queue Rules:
   - Standard build queue behavior, Roman dual-construction queue logic, and Master Builder queue reserve validation.
   - Level-by-level demolition rules and prerequisites (Main Building Level >= 10)."

## Clarifications

### Session 2026-11-06

- Q: Should Blacksmith (`gid 12`) and Armoury (`gid 13`) remain two separate buildings for offense and defense upgrades respectively, or be merged into a single unified Smithy? → A: Keep Blacksmith (`gid 12`, weapon/offense upgrades) and Armoury (`gid 13`, armor/defense upgrades) as two separate buildings.
- Q: Should Main Building (`gid 15`) demolition support both the 10-Gold instant complete demolition (razing a building directly to Level 0) and the Embassy demolition safeguard for Alliance founders, or only timed level-by-level demolition? → A: Support timed 1-level demolition, 2-Gold "Finish All" completion of an active demolition, 10-Gold instant complete demolition to Level 0, and the Alliance founder Embassy (`gid 18`) demolition safeguard.
- Q: Should unit Academy research prerequisites and training building assignments follow the canonical faction rules (including training Hun `Scout Rider u52`, Spartan `Perioikoi Scout u74`, and Viking `Viking Scout u82` in the Barracks)? → A: Enforce the exact canonical Academy research prerequisites and training building assignments (`Barracks`, `Stable`, `Workshop`, `Residence/Palace/Command Center`).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Browse & Validate Tribe Profiles and Unit Catalogs (Priority: P1)

Players and game systems need an authoritative catalog of all 9 tribes (7 playable factions and 2 non-playable factions) and their 90 unit types (plus auxiliary defensive traps and hero unit scaling baselines) so that faction selection, military recruitment, movement planning, and combat evaluation operate on consistent, verifiable rules.

**Why this priority**: Every military, economic, and world-generation interaction depends on tribe identities, cultural traits, and baseline unit statistics. Without the tribe and unit catalog, neither players nor automated systems can recruit troops, calculate upkeep, or simulate battles.

**Independent Test**: Can be fully tested by querying the catalog for each of the 9 tribes (`Tribe 1..9`) and verifying that each tribe returns its cultural traits, merchant capacity/speed, exclusive buildings, and exactly 10 designated units (`u1..u90`) with exact Wood, Clay, Iron, Crop costs, base training duration, speed, carry capacity, offense, infantry defense, cavalry defense, and hourly crop consumption matching the canonical Terravia Renatus catalog baseline.

**Acceptance Scenarios**:

1. **Given** a new player selecting a playable tribe during registration, **When** the player inspects the 7 playable tribes (Romans, Teutons, Gauls, Huns, Egyptians, Spartans, Vikings), **Then** the system displays each tribe's cultural bonuses, exclusive buildings, defensive wall type, merchant speed/capacity, and 10 tribe-specific unit profiles, while excluding NPC tribes (Nature and Archons) from player registration.
2. **Given** any tribe identifier $T \in \{1 \dots 9\}$, **When** the system resolves the unit roster for tribe $T$, **Then** it maps unit slots $1 \dots 10$ to global unit identifiers $(T-1) \times 10 + 1$ through $(T-1) \times 10 + 10$ with complete combat, resource, speed, carry, and upkeep attributes.
3. **Given** a Roman village with a Horse Drinking Trough at Level 10, 15, or 20, **When** hourly crop upkeep is calculated for Roman cavalry units (`Equites Scout`, `Equites Eques`, `Equites Cataphract`), **Then** each corresponding cavalry tier receives a 1-crop-per-hour upkeep reduction at its designated building level threshold (`Level 10`, `Level 15`, `Level 20`).

---

### User Story 2 - Evaluate Building Catalog, Level Scaling & Prerequisite Trees (Priority: P1)

Players inspecting empty building slots or upgrading existing structures need accurate level-by-level progression tables (`Level 1..20` for standard buildings, `Level 1..5` for Sawmill/Brickyard/Iron Foundry/Grain Mill/Bakery production booster buildings, `Level 1..10` for Cranny and Brewery, and `Level 1..100` for World Wonder) and strict prerequisite enforcement so only legal buildings for their tribe, village status (Capital vs. Non-Capital), and current infrastructure can be constructed.

**Why this priority**: Village progression is the core economic loop of the game. Enforcing exact building prerequisites, capital restrictions, tribe-exclusive structures, and multi-instance rebuild rules prevents illegal village states and ensures 100% parity with baseline game rules.

**Independent Test**: Can be tested independently by evaluating building eligibility across simulated village states (varying tribe, capital flag, existing building levels, and artifact ownership) against all 50 building types (`gid 1..50`) and verifying upgrade costs, construction times, population, culture points, and functional effects at each level.

**Acceptance Scenarios**:

1. **Given** an empty inner village slot (`Slot 19..38`) in a Gaul village with Main Building Level 3 and Rally Point Level 1, **When** the player views available buildings to construct, **Then** the Trapper (`gid 36`) and Barracks (`gid 19`) are listed as available, whereas Roman-only (`gid 41`), Teuton-only (`gid 35`), Hun-only (`gid 44`), Egyptian-only (`gid 45`), and Spartan/Viking-only (`gid 48`) buildings are prohibited.
2. **Given** a Hun village attempting to construct a Residence (`gid 25`) or Palace (`gid 26`), **When** building prerequisites are evaluated, **Then** both Residence and Palace are rejected, and the Hun village is instead permitted to construct the Command Center (`gid 44`) once Main Building reaches Level 5 (and can also construct the Stonemason's Lodge `gid 34` in its capital once Command Center reaches Level 3).
3. **Given** a village that already owns a Warehouse (`gid 10`), Granary (`gid 11`), Cranny (`gid 23`), or Gaul Trapper (`gid 36`), **When** the player attempts to construct an additional instance of that building in another empty slot, **Then** construction is blocked until at least one existing instance of that building in the village reaches its maximum prerequisite threshold (`Level 20` for Warehouse/Granary/Trapper/Great Storage, `Level 10` for Cranny).
4. **Given** a non-capital village vs. a capital village, **When** evaluating maximum resource field levels (`gid 1..4`) and Great Barracks (`gid 29`) / Great Stable (`gid 30`) / Great Workshop (`gid 49`) / Brewery (`gid 35`) / Stonemason's Lodge (`gid 34`), **Then** resource fields in non-capitals are capped at Level 10 (uncapped up to catalog max Level 20 in capitals), Great military buildings are restricted exclusively to non-capitals, and Brewery and Stonemason's Lodge are restricted exclusively to capitals.

---

### User Story 3 - Execute Construction Queues, Roman Dual-Build & Master Builder Reservations (Priority: P2)

Players queuing resource field and village center upgrades need deterministic build queue management that honors standard single-slot execution, Plus Subscription waiting-loop slots, Roman simultaneous resource-field + inner-building construction, and Master Builder reserved slots that defer resource deduction until activation.

**Why this priority**: Build queue rules directly govern how fast villages develop and how cultural bonuses (Romans) and premium convenience features (Plus queue, Master Builder, Gold instant completion) behave under normal and edge conditions.

**Independent Test**: Can be tested by submitting sequences of build and upgrade commands for Roman and non-Roman players (with and without Plus and Master Builder slots), verifying start/completion timestamps, resource deductions, cancellation refunds, and instant-completion restrictions.

**Acceptance Scenarios**:

1. **Given** a Roman player without a Plus Subscription, **When** one resource field (`Slot 1..18`) is actively upgrading and the player orders an inner village building (`Slot 19..40`) upgrade, **Then** both constructions execute concurrently with independent completion timers; if a second resource field is queued with Plus active, its start time chains strictly after the active resource field (plus the `ceil(60 / Speed)` loop delay).
2. **Given** a non-Roman player without a Plus Subscription, **When** one resource field (`Slot 1..18`) is actively upgrading and the player orders an inner village building (`Slot 19..40`) upgrade, **Then** the second order is rejected unless Plus is active (which places it in the waiting loop) or a Master Builder slot is used.
3. **Given** a player whose active/Plus build queues are full (or who lacks current resources), **When** the player reserves a building upgrade via the Master Builder slot, **Then** neither resources nor Gold are deducted at reservation time; when a construction slot becomes free and village resources reach the required cost, the system automatically converts the Master Builder job into an active build, deducts the building resources, and charges the Master Builder Gold fee.
4. **Given** a village containing a World Wonder (`Slot 99` / `gid 40`) or an active upgrade of a Residence (`gid 25`), Palace (`gid 26`), or World Wonder (`gid 40`), **When** the player invokes "Finish All Constructions with Gold", **Then** any village housing a World Wonder skips instant Gold completion for all buildings in that village, and Residence, Palace, and World Wonder jobs are never instant-completed with Gold in any village.

---

### User Story 4 - Demolish Village Buildings Level-by-Level (Priority: P3)

Players restructuring captured or maturing villages need to demolish inner village buildings one level at a time (or instantly with Gold) once their Main Building reaches Level 10 or higher.

**Why this priority**: Demolition allows players to repurpose slots after completing academy research or smithy upgrades, or after conquering a poorly laid-out village, completing the building lifecycle.

**Independent Test**: Can be tested by attempting demolition in villages with Main Building Level 9 (rejected) and Level 10+ (accepted), verifying single-active-demolition constraints, level decrement on completion, and building type clearance when Level 1 is demolished to Level 0.

**Acceptance Scenarios**:

1. **Given** a village with Main Building (`gid 15`) at Level 9, **When** the player attempts to demolish an inner building (`Slot 19..40`), **Then** the system rejects the request because Main Building Level 10 is required.
2. **Given** a village with Main Building at Level 10 or higher and no active demolition in progress, **When** the player initiates timed demolition on an inner building at Level $L \ge 1$, **Then** a demolition timer is scheduled; upon completion (or 2-Gold "Finish All"), the building level decreases to $L - 1$ (and if $L - 1 = 0$, the slot's building type is cleared to empty and any pending queued upgrades for that slot are cancelled).
3. **Given** a village with Main Building at Level 10 or higher and a player with at least 10 Gold, **When** the player selects "Instant Complete Demolition" on an inner building at Level $L \ge 1$, **Then** 10 Gold is deducted, any active demolition in the village is cleared, the building is immediately razed from Level $L$ directly to Level 0 (clearing its slot type), and the cumulative population across all levels $1 \dots L$, storage capacity (if Warehouse/Granary/Great Storage), and daily Culture Points are recalculated immediately.
4. **Given** a player who is the founder/owner of an Alliance with more than 1 member, **When** the player attempts to demolish (timed or instant) their Embassy (`gid 18`), **Then** the system blocks the demolition until leadership is transferred or other members leave.

---

### Edge Cases

- **Queued Multi-Level Upgrades on the Same Slot**: When a building at Level $L$ already has $K$ upgrades queued in the waiting loop or Master Builder slots, queueing another upgrade on that same slot must evaluate costs, construction time, and maximum level caps against target level $L + K + 1$, not $L + 1$.
- **World Wonder Building Plan Bypass via Queued Slots**: When a player queues multiple World Wonder (`gid 40`) upgrades simultaneously (e.g., at Level 48 queueing Levels 49, 50, and 51 via Plus/Master Builder), prerequisite validation must inspect the highest queued level across the entire build queue (`51 > 49`) and require the second alliance World Wonder Building Plan before allowing Level 50 or 51 to be queued.
- **Cancelling Active vs. Master Builder Queued Jobs**: Cancelling a normal or Plus-loop build job refunds the deducted construction resources (capped by current Warehouse/Granary capacity), whereas cancelling an unstarted Master Builder reservation refunds zero resources and zero Gold because neither was deducted upon reservation.
- **Palace Account-Wide Uniqueness Check**: Checking whether a player already has a Palace (`gid 26`) across their empire must strictly inspect inner building slot type identifiers (`Slot 19..40` building types) rather than matching arbitrary numeric values (such as a village ID of 26 or a World Wonder at Level 26).
- **Main Building Demolished Below Level 10**: If the Main Building itself is demolished from Level 10 to Level 9, no further demolitions can be initiated until the Main Building is upgraded back to at least Level 10.
- **Building Cost Exceeding Storage Capacity**: If the next level of a building requires more Wood, Clay, or Iron than the village's current Warehouse capacity—or more Crop than current Granary capacity—the system must return a distinct "Upgrade Warehouse / Granary first" status rather than a generic "Not enough resources" status.

## Requirements *(mandatory)*

### Functional Requirements

#### 1. Tribe Profiles & Cultural Bonuses (`Tribe 1..9`)

- **FR-001**: The system MUST define exactly 9 tribes identified by integer IDs `1` through `9`:
  - **Playable Factions (`7`)**: `1` Romans, `2` Teutons, `3` Gauls, `6` Huns, `7` Egyptians, `8` Spartans, `9` Vikings.
  - **Non-Playable Factions (`2`)**: `4` Nature (oasis wildlife garrison faction) and `5` Archons (imperial endgame faction guarding Artifacts and World Wonder villages).
- **FR-002**: The system MUST enforce tribe-specific cultural bonuses and systemic rules across economy, construction, trade, and combat:

| Tribe ID & Name | Playable | Merchant Carry & Speed (`1x`) | Exclusive Wall (`Slot 40`) | Exclusive Buildings | Cultural & Faction Traits |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`1` Romans** | Yes | `500` res @ `16` tiles/hr | City Wall (`gid 31`: `+3%`/lvl def bonus) | Horse Drinking Trough (`gid 41`) | Simultaneous construction of 1 Resource Field (`Slot 1..18`) + 1 Inner Building (`Slot 19..40`); Horse Drinking Trough speeds up cavalry training by `1%`/lvl and reduces crop upkeep by `1`/hr for `u4` at Lvl 10, `u5` at Lvl 15, `u6` at Lvl 20. |
| **`2` Teutons** | Yes | `1000` res @ `12` tiles/hr | Earth Wall (`gid 32`: `+2%`/lvl def bonus, high durability) | Brewery (`gid 35`, Capital only) | Cranny raid bonus (enemy Crannies hide less when raided by Teutons / Hero modifier); Brewery enables 72h Mead Festival (`+1%` attack power per level, halves chief persuasion, forces random catapult targeting). |
| **`3` Gauls** | Yes | `750` res @ `24` tiles/hr | Palisade (`gid 33`: `+2.5%`/lvl def bonus) | Trapper (`gid 36`) | Cranny (`gid 23`) holds `2x` resources compared to other tribes; fastest cavalry and merchants; Trapper builds up to `400` traps (`u99`) per Level 20 building to capture attacking troops before combat. |
| **`4` Nature** | No (NPC) | N/A | None | None | Populates unoccupied Oases with wildlife units (`u31..u40`); animals can be captured by player Heroes equipped with Cages to defend the player's village with `0` crop upkeep. |
| **`5` Archons** | No (NPC) | `750` res @ `16` tiles/hr | City/Stone Wall | World Wonder (`gid 40`), Great Warehouse (`gid 38`), Great Granary (`gid 39`) | Custodians of Artifacts, World Wonder Building Plans, and WW villages (`u41..u50`); Archon villages can construct Great Warehouses and Great Granaries without requiring a Storage Blueprint artifact. |
| **`6` Huns** | Yes | `500` res @ `20` tiles/hr | Makeshift Wall (`gid 43`: `+1.5%`/lvl def bonus, fragile) | Command Center (`gid 44`) | Cannot build Residence (`gid 25`) or Palace (`gid 26`); instead builds Command Center (`gid 44`), which unlocks expansion slots (Settlers/Tribal Chiefs) at **3 levels** (`Levels 10, 15, and 20`) and satisfies the capital requirement for Stonemason's Lodge (`gid 34`) at Level 3. |
| **`7` Egyptians** | Yes | `750` res @ `16` tiles/hr | Stone Wall (`gid 42`: `+3%`/lvl def bonus, high durability) | Waterworks (`gid 45`) | Waterworks (`gid 45`, requires Hero's Mansion Lvl 10) increases Hero oasis production bonuses by `5%` per level (up to `+100%` at Level 20); extremely cheap, fast-training basic infantry (`Armed Slave u61`). |
| **`8` Spartans** | Yes | `500` res @ `14` tiles/hr | Defensive Wall (`gid 47`: `+2.8%`/lvl def bonus) | Big Hospital (`gid 48`) | Elite heavy infantry faction (`Spartan Hoplite`, `Agoge Warrior`, `Homoioi`, `Hippeis`); replaces standard Hospital (`gid 46`) with Big Hospital (`gid 48`) for enhanced casualty recovery. |
| **`9` Vikings** | Yes | `750` res @ `18` tiles/hr | Barricade (`gid 50`: `+2.2%`/lvl def bonus) | Big Hospital (`gid 48`) | Aggressive northern raiders (`Viking Raider`, `Norse Axeman`, `Berserker`, `Viking Scout`, `Viking Rider`, `Huscarl`, `Jarl`); replaces standard Hospital (`gid 46`) with Big Hospital (`gid 48`). |

#### 2. Unit Catalog & Baseline Attributes (`u1..u90` + `u99` Traps)

- **FR-003**: The system MUST define 90 distinct unit types (`u1..u90`, 10 per tribe) plus `u99` (Gaul Traps), categorized into explicit tactical roles:
  - **Infantry**: `u1, u2, u3` (Romans), `u11, u12, u13, u14` (Teutons), `u21, u22` (Gauls), `u31, u32, u33, u34` (Nature), `u41, u42, u43, u44` (Archons), `u51, u52` (Huns), `u61, u62, u63` (Egyptians), `u71, u72, u73, u74` (Spartans), `u81, u82, u83, u84` (Vikings).
  - **Cavalry**: `u4, u5, u6` (Romans), `u15, u16` (Teutons), `u23, u24, u25, u26` (Gauls), `u35, u36` (Nature), `u45, u46` (Archons), `u53, u54, u55, u56` (Huns), `u64, u65, u66` (Egyptians), `u75, u76` (Spartans), `u85, u86` (Vikings).
  - **Scouts / Reconnaissance**: `u4` (Equites Scout), `u14` (Scout), `u23` (Pathfinder), `u44` (Shadow Falcon), `u52` (Scout Rider), `u64` (Horse Scout), `u74` (Perioikoi Scout), `u82` (Viking Scout).
  - **Siege Rams**: `u7, u17, u27, u47, u57, u67, u77, u87` (plus Nature `u37` Bear in siege slot).
  - **Siege Catapults**: `u8, u18, u28, u48, u58, u68, u78, u88` (plus Nature `u38` Crocodile in siege slot).
  - **Administrators / Cultural Conquerors (Chiefs)**: `u9` (Senator), `u19` (Chief), `u29` (Chieftain), `u49` (Archon Hierophant), `u59` (Tribal Chief), `u69` (Nomarch), `u79` (Ephor), `u89` (Jarl).
  - **Settlers**: `u10, u20, u30, u50, u60, u70, u80, u90`.
- **FR-004**: Every unit in the catalog MUST expose exact baseline attributes at `1x` server speed matching the authoritative reference table below (`Atk` = Offense, `Def-I` = Defense vs. Infantry, `Def-C` = Defense vs. Cavalry, `Pop` = Hourly Crop Upkeep, `Time` = Base Training Seconds at Level 1 training building on `1x` speed, `Cap` = Resource Carry Capacity, `Speed` = Tiles per hour on `1x` speed):

##### Playable & NPC Unit Catalog (`u1..u90` and `u99`)

| ID | Tribe | Unit Name | Role | Atk | Def-I | Def-C | Wood | Clay | Iron | Crop | Upkeep (`Pop`) | Speed | Base Time (s) | Carry (`Cap`) | Academy Research Prerequisites |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **`u1`** | `1` Romans | Legionnaire | Inf | 40 | 35 | 50 | 120 | 100 | 150 | 30 | 1 | 6 | 1600 | 50 | None (Default, Barracks) |
| **`u2`** | `1` Romans | Praetorian | Inf | 30 | 65 | 35 | 100 | 130 | 160 | 70 | 1 | 5 | 1760 | 20 | Academy 1, Armoury 1 (Barracks) |
| **`u3`** | `1` Romans | Triarius | Inf | 70 | 40 | 25 | 150 | 160 | 210 | 80 | 1 | 7 | 1920 | 50 | Academy 5, Blacksmith 1 (Barracks) |
| **`u4`** | `1` Romans | Equites Scout | Cav / Scout | 0 | 20 | 10 | 140 | 160 | 20 | 40 | 2 (`-1` @ HDT 10) | 16 | 1360 | 0 | Academy 5, Stable 1 (Stable) |
| **`u5`** | `1` Romans | Equites Eques | Cav | 120 | 65 | 50 | 550 | 440 | 320 | 100 | 3 (`-1` @ HDT 15) | 14 | 2640 | 100 | Academy 5, Stable 5 (Stable) |
| **`u6`** | `1` Romans | Equites Cataphract | Cav | 180 | 80 | 105 | 550 | 640 | 800 | 180 | 4 (`-1` @ HDT 20) | 10 | 3520 | 70 | Academy 15, Stable 10 (Stable) |
| **`u7`** | `1` Romans | Battering Ram | Ram | 60 | 30 | 75 | 900 | 360 | 500 | 70 | 3 | 4 | 4600 | 0 | Academy 10, Workshop 1 (Workshop) |
| **`u8`** | `1` Romans | Fire Catapult | Catapult | 75 | 60 | 10 | 950 | 1350 | 600 | 90 | 6 | 3 | 9000 | 0 | Academy 15, Workshop 10 (Workshop) |
| **`u9`** | `1` Romans | Senator | Chief | 50 | 40 | 30 | 30750 | 27200 | 45000 | 37500 | 5 | 5 | 90700 | 0 | Academy 20, Rally Point 10 (Res/Palace) |
| **`u10`** | `1` Romans | Settler | Settler | 0 | 80 | 80 | 5800 | 5300 | 7200 | 5500 | 1 | 5 | 26900 | 3000 | Residence / Palace (Slot Level) |
| **`u11`** | `2` Teutons | Clubswinger | Inf | 40 | 20 | 5 | 95 | 75 | 40 | 40 | 1 | 7 | 720 | 60 | None (Default, Barracks) |
| **`u12`** | `2` Teutons | Spearman | Inf | 10 | 35 | 60 | 145 | 70 | 85 | 40 | 1 | 7 | 1120 | 40 | Academy 1, Barracks 3 (Barracks) |
| **`u13`** | `2` Teutons | Axeman | Inf | 60 | 30 | 30 | 130 | 120 | 170 | 70 | 1 | 6 | 1200 | 50 | Academy 3, Blacksmith 1 (Barracks) |
| **`u14`** | `2` Teutons | Scout | Inf / Scout | 0 | 10 | 5 | 160 | 100 | 50 | 50 | 1 | 9 | 1120 | 0 | Academy 1, Main Building 5 (Barracks) |
| **`u15`** | `2` Teutons | Paladin | Cav | 55 | 100 | 40 | 370 | 270 | 290 | 75 | 2 | 10 | 2400 | 110 | Academy 1, Stable 3 (Stable) |
| **`u16`** | `2` Teutons | Teutonic Knight | Cav | 150 | 50 | 75 | 450 | 515 | 480 | 80 | 3 | 9 | 2960 | 80 | Academy 15, Stable 10 (Stable) |
| **`u17`** | `2` Teutons | Ram | Ram | 65 | 30 | 80 | 1000 | 300 | 350 | 70 | 3 | 4 | 4200 | 0 | Academy 10, Workshop 1 (Workshop) |
| **`u18`** | `2` Teutons | Catapult | Catapult | 50 | 60 | 10 | 900 | 1200 | 600 | 60 | 6 | 3 | 9000 | 0 | Academy 15, Workshop 10 (Workshop) |
| **`u19`** | `2` Teutons | Chief | Chief | 40 | 60 | 40 | 35500 | 26600 | 25000 | 27200 | 4 | 5 | 70500 | 0 | Academy 20, Rally Point 5 (Res/Palace) |
| **`u20`** | `2` Teutons | Settler | Settler | 10 | 80 | 80 | 7200 | 5500 | 5800 | 6500 | 1 | 5 | 31000 | 3000 | Residence / Palace (Slot Level) |
| **`u21`** | `3` Gauls | Phalanx | Inf | 15 | 40 | 50 | 100 | 130 | 55 | 30 | 1 | 7 | 1040 | 35 | None (Default, Barracks) |
| **`u22`** | `3` Gauls | Swordsman | Inf | 65 | 35 | 20 | 140 | 150 | 185 | 60 | 1 | 6 | 1440 | 45 | Academy 3, Blacksmith 1 (Barracks) |
| **`u23`** | `3` Gauls | Pathfinder | Cav / Scout | 0 | 20 | 10 | 170 | 150 | 20 | 40 | 2 | 17 | 1360 | 0 | Academy 5, Stable 1 (Stable) |
| **`u24`** | `3` Gauls | Taranis Rider | Cav | 90 | 25 | 40 | 350 | 450 | 230 | 60 | 2 | 19 | 2480 | 75 | Academy 5, Stable 3 (Stable) |
| **`u25`** | `3` Gauls | Druid Rider | Cav | 45 | 115 | 55 | 360 | 330 | 280 | 120 | 2 | 16 | 2560 | 35 | Academy 5, Stable 5 (Stable) |
| **`u26`** | `3` Gauls | Aedui Noble | Cav | 140 | 50 | 165 | 500 | 620 | 675 | 170 | 3 | 13 | 3120 | 65 | Academy 15, Stable 10 (Stable) |
| **`u27`** | `3` Gauls | Ram | Ram | 50 | 30 | 105 | 950 | 555 | 330 | 75 | 3 | 4 | 5000 | 0 | Academy 10, Workshop 1 (Workshop) |
| **`u28`** | `3` Gauls | Trebuchet | Catapult | 70 | 45 | 10 | 960 | 1450 | 630 | 90 | 6 | 3 | 9000 | 0 | Academy 15, Workshop 10 (Workshop) |
| **`u29`** | `3` Gauls | Chieftain | Chief | 40 | 50 | 50 | 30750 | 45400 | 31000 | 37500 | 4 | 4 | 90700 | 0 | Academy 20, Rally Point 10 (Res/Palace) |
| **`u30`** | `3` Gauls | Settler | Settler | 0 | 80 | 80 | 5500 | 7000 | 5300 | 4900 | 1 | 5 | 22700 | 3000 | Residence / Palace (Slot Level) |
| **`u31`** | `4` Nature | Rat | Inf (NPC) | 10 | 25 | 20 | 85 | 75 | 120 | 25 | 1 | 7 | 1600 | 45 | NPC Oasis Spawn |
| **`u32`** | `4` Nature | Spider | Inf (NPC) | 20 | 35 | 40 | 125 | 130 | 60 | 40 | 1 | 7 | 1800 | 65 | NPC Oasis Spawn |
| **`u33`** | `4` Nature | Snake | Inf (NPC) | 60 | 40 | 60 | 140 | 150 | 40 | 60 | 1 | 6 | 1900 | 80 | NPC Oasis Spawn |
| **`u34`** | `4` Nature | Bat | Inf (NPC) | 10 | 66 | 50 | 95 | 120 | 65 | 25 | 1 | 9 | 2000 | 0 | NPC Oasis Spawn |
| **`u35`** | `4` Nature | Wild Boar | Cav (NPC) | 50 | 70 | 33 | 250 | 200 | 125 | 45 | 2 | 10 | 2000 | 120 | NPC Oasis Spawn |
| **`u36`** | `4` Nature | Wolf | Cav (NPC) | 100 | 80 | 70 | 250 | 125 | 250 | 150 | 2 | 9 | 2000 | 150 | NPC Oasis Spawn |
| **`u37`** | `4` Nature | Bear | Siege (NPC) | 250 | 140 | 200 | 250 | 220 | 135 | 50 | 3 | 4 | 2000 | 125 | NPC Oasis Spawn |
| **`u38`** | `4` Nature | Crocodile | Siege (NPC) | 450 | 380 | 240 | 125 | 250 | 300 | 65 | 3 | 3 | 2000 | 0 | NPC Oasis Spawn |
| **`u39`** | `4` Nature | Tiger | Exp (NPC) | 200 | 170 | 250 | 350 | 350 | 125 | 80 | 3 | 5 | 70500 | 0 | NPC Oasis Spawn |
| **`u40`** | `4` Nature | Elephant | Exp (NPC) | 600 | 440 | 520 | 350 | 250 | 135 | 100 | 5 | 5 | 31000 | 3000 | NPC Oasis Spawn |
| **`u41`** | `5` Archons | Archon Pikeman | Inf (NPC) | 20 | 35 | 50 | 0 | 0 | 0 | 0 | 1 | 6 | 0 | 0 | NPC Archon Garrison |
| **`u42`** | `5` Archons | Thorned Warden | Inf (NPC) | 65 | 30 | 10 | 0 | 0 | 0 | 0 | 1 | 7 | 0 | 0 | NPC Archon Garrison |
| **`u43`** | `5` Archons | Immortal Guard | Inf (NPC) | 100 | 90 | 75 | 0 | 0 | 0 | 0 | 1 | 6 | 0 | 0 | NPC Archon Garrison |
| **`u44`** | `5` Archons | Shadow Falcon | Inf / Scout | 0 | 50 | 25 | 0 | 0 | 0 | 0 | 2 | 25 | 0 | 0 | NPC Archon Garrison |
| **`u45`** | `5` Archons | Outrider | Cav (NPC) | 155 | 80 | 50 | 0 | 0 | 0 | 0 | 2 | 14 | 0 | 0 | NPC Archon Garrison |
| **`u46`** | `5` Archons | Archon Cavalier | Cav (NPC) | 170 | 140 | 80 | 0 | 0 | 0 | 0 | 3 | 12 | 0 | 0 | NPC Archon Garrison |
| **`u47`** | `5` Archons | War Elephant | Ram (NPC) | 250 | 120 | 150 | 0 | 0 | 0 | 0 | 4 | 5 | 0 | 0 | NPC Archon Garrison |
| **`u48`** | `5` Archons | Ballista | Catapult | 60 | 45 | 10 | 0 | 0 | 0 | 0 | 5 | 3 | 0 | 0 | NPC Archon Garrison |
| **`u49`** | `5` Archons | Archon Hierophant | Chief (NPC) | 80 | 50 | 50 | 0 | 0 | 0 | 0 | 1 | 5 | 0 | 0 | NPC Archon Garrison |
| **`u50`** | `5` Archons | Archon Settler | Settler | 30 | 40 | 40 | 0 | 0 | 0 | 0 | 1 | 5 | 0 | 0 | NPC Archon Garrison |
| **`u51`** | `6` Huns | Hun Warrior | Inf | 35 | 40 | 30 | 130 | 80 | 40 | 40 | 1 | 6 | 1120 | 50 | None (Default, Barracks) |
| **`u52`** | `6` Huns | Scout Rider | Inf / Scout | 0 | 20 | 10 | 170 | 150 | 20 | 40 | 2 | 19 | 1360 | 0 | Academy 1, Main Building 5 (Barracks) |
| **`u53`** | `6` Huns | Horse Archer | Cav | 120 | 30 | 15 | 290 | 370 | 190 | 45 | 2 | 16 | 2400 | 75 | Academy 5, Stable 3 (Stable) |
| **`u54`** | `6` Huns | Steppe Rider | Cav | 110 | 80 | 70 | 320 | 350 | 330 | 50 | 2 | 15 | 2480 | 105 | Academy 5, Stable 5 (Stable) |
| **`u55`** | `6` Huns | Hun Lancer | Cav | 180 | 60 | 40 | 450 | 560 | 610 | 140 | 3 | 14 | 2960 | 80 | Academy 15, Stable 10 (Stable) |
| **`u56`** | `6` Huns | Elite Rider | Cav | 230 | 90 | 65 | 660 | 750 | 800 | 180 | 4 | 13 | 3520 | 70 | Academy 15, Stable 10 (Stable) |
| **`u57`** | `6` Huns | Ram | Ram | 65 | 30 | 90 | 1060 | 330 | 360 | 70 | 3 | 4 | 4400 | 0 | Academy 10, Workshop 1 (Workshop) |
| **`u58`** | `6` Huns | Catapult | Catapult | 45 | 55 | 10 | 950 | 1280 | 620 | 60 | 6 | 3 | 9000 | 0 | Academy 15, Workshop 10 (Workshop) |
| **`u59`** | `6` Huns | Tribal Chief | Chief | 50 | 40 | 30 | 37200 | 27600 | 25200 | 27600 | 5 | 5 | 90700 | 0 | Academy 20, Rally Point 10 (Command Center) |
| **`u60`** | `6` Huns | Hun Settler | Settler | 10 | 80 | 80 | 6100 | 4600 | 4800 | 5400 | 1 | 5 | 28950 | 3000 | Command Center (Slot Level) |
| **`u61`** | `7` Egyptians | Armed Slave | Inf | 10 | 30 | 20 | 45 | 60 | 30 | 30 | 1 | 7 | 640 | 20 | None (Default, Barracks) |
| **`u62`** | `7` Egyptians | Egyptian Fighter | Inf | 30 | 55 | 40 | 115 | 100 | 145 | 60 | 1 | 6 | 1450 | 45 | Academy 1, Armoury 1 (Barracks) |
| **`u63`** | `7` Egyptians | Temple Guardian | Inf | 25 | 110 | 80 | 170 | 180 | 220 | 80 | 1 | 6 | 1760 | 35 | Academy 3, Blacksmith 1 (Barracks) |
| **`u64`** | `7` Egyptians | Horse Scout | Cav / Scout | 0 | 20 | 10 | 170 | 150 | 20 | 40 | 2 | 16 | 1360 | 0 | Academy 5, Stable 1 (Stable) |
| **`u65`** | `7` Egyptians | Chariot | Cav | 50 | 110 | 50 | 360 | 330 | 280 | 120 | 2 | 15 | 2560 | 50 | Academy 5, Stable 5 (Stable) |
| **`u66`** | `7` Egyptians | Royal Chariot | Cav | 110 | 120 | 150 | 450 | 560 | 610 | 180 | 3 | 10 | 3120 | 70 | Academy 15, Stable 10 (Stable) |
| **`u67`** | `7` Egyptians | Ram | Ram | 55 | 30 | 95 | 995 | 575 | 340 | 80 | 3 | 4 | 4800 | 0 | Academy 10, Workshop 1 (Workshop) |
| **`u68`** | `7` Egyptians | Stone Catapult | Catapult | 65 | 55 | 10 | 980 | 1510 | 660 | 100 | 6 | 3 | 9000 | 0 | Academy 15, Workshop 10 (Workshop) |
| **`u69`** | `7` Egyptians | Nomarch | Chief | 40 | 50 | 50 | 34000 | 50000 | 34000 | 42000 | 4 | 4 | 90700 | 0 | Academy 20, Rally Point 10 (Res/Palace) |
| **`u70`** | `7` Egyptians | Egyptian Settler | Settler | 0 | 80 | 80 | 5040 | 6510 | 4830 | 4620 | 1 | 5 | 24800 | 3000 | Residence / Palace (Slot Level) |
| **`u71`** | `8` Spartans | Spartan Hoplite | Inf | 50 | 35 | 30 | 110 | 185 | 110 | 35 | 1 | 6 | 1700 | 60 | None (Default, Barracks) |
| **`u72`** | `8` Spartans | Agoge Warrior | Inf | 40 | 85 | 45 | 145 | 95 | 245 | 45 | 1 | 6 | 1950 | 40 | Academy 1, Armoury 1 (Barracks) |
| **`u73`** | `8` Spartans | Homoioi | Inf | 95 | 80 | 75 | 130 | 200 | 400 | 65 | 1 | 5 | 2200 | 50 | Academy 5, Blacksmith 1 (Barracks) |
| **`u74`** | `8` Spartans | Perioikoi Scout | Inf / Scout | 0 | 25 | 12 | 185 | 150 | 35 | 75 | 2 | 16 | 1360 | 0 | Academy 1, Blacksmith 1 (Barracks) |
| **`u75`** | `8` Spartans | Spartan Rider | Cav | 55 | 120 | 90 | 555 | 445 | 330 | 110 | 2 | 16 | 2600 | 110 | Academy 1, Stable 3 (Stable) |
| **`u76`** | `8` Spartans | Hippeis | Cav | 195 | 55 | 80 | 660 | 495 | 995 | 165 | 3 | 9 | 3450 | 80 | Academy 15, Stable 10 (Stable) |
| **`u77`** | `8` Spartans | Ram | Ram | 65 | 30 | 85 | 980 | 545 | 390 | 80 | 3 | 4 | 4600 | 0 | Academy 10, Workshop 1 (Workshop) |
| **`u78`** | `8` Spartans | Ballista | Catapult | 60 | 55 | 10 | 1200 | 1240 | 610 | 80 | 6 | 3 | 9000 | 0 | Academy 15, Workshop 10 (Workshop) |
| **`u79`** | `8` Spartans | Ephor | Chief | 45 | 50 | 45 | 33450 | 30665 | 36240 | 27375 | 4 | 4 | 90700 | 0 | Academy 20, Rally Point 10 (Res/Palace) |
| **`u80`** | `8` Spartans | Spartan Settler | Settler | 10 | 80 | 80 | 5115 | 5580 | 6360 | 4485 | 1 | 5 | 26900 | 3000 | Residence / Palace (Slot Level) |
| **`u81`** | `9` Vikings | Viking Raider | Inf | 45 | 30 | 15 | 105 | 85 | 65 | 35 | 1 | 7 | 960 | 70 | None (Default, Barracks) |
| **`u82`** | `9` Vikings | Viking Scout | Inf / Scout | 0 | 15 | 8 | 155 | 110 | 45 | 45 | 1 | 10 | 1120 | 0 | Academy 1, Main Building 5 (Barracks) |
| **`u83`** | `9` Vikings | Norse Axeman | Inf | 70 | 35 | 25 | 145 | 135 | 185 | 70 | 1 | 6 | 1400 | 55 | Academy 3, Blacksmith 1 (Barracks) |
| **`u84`** | `9` Vikings | Berserker | Inf | 105 | 45 | 30 | 190 | 220 | 340 | 90 | 2 | 6 | 1960 | 60 | Academy 5, Blacksmith 1 (Barracks) |
| **`u85`** | `9` Vikings | Viking Rider | Cav | 100 | 70 | 55 | 415 | 380 | 305 | 85 | 2 | 12 | 2640 | 90 | Academy 5, Stable 5 (Stable) |
| **`u86`** | `9` Vikings | Huscarl | Cav | 165 | 95 | 100 | 590 | 640 | 715 | 160 | 3 | 10 | 3300 | 75 | Academy 15, Stable 10 (Stable) |
| **`u87`** | `9` Vikings | Ram | Ram | 65 | 35 | 80 | 1010 | 415 | 440 | 75 | 3 | 4 | 4700 | 0 | Academy 10, Workshop 1 (Workshop) |
| **`u88`** | `9` Vikings | Catapult | Catapult | 55 | 60 | 10 | 930 | 1350 | 640 | 80 | 6 | 3 | 9000 | 0 | Academy 15, Workshop 10 (Workshop) |
| **`u89`** | `9` Vikings | Jarl | Chief | 50 | 45 | 40 | 35800 | 29200 | 28600 | 31500 | 4 | 5 | 90700 | 0 | Academy 20, Rally Point 10 (Res/Palace) |
| **`u90`** | `9` Vikings | Viking Settler | Settler | 10 | 80 | 80 | 5900 | 5200 | 5700 | 5800 | 1 | 5 | 27500 | 3000 | Residence / Palace (Slot Level) |
| **`u99`** | `3` Gauls | Trap | Trap | 0 | 0 | 0 | 20 | 30 | 10 | 20 | 0 | 0 | 600 | 0 | Built in Trapper (`gid 36`) |

- **FR-005**: The unit catalog MUST also include Hero stat-scaling templates (`h1..h86`) for the 5 eligible combat units of each playable tribe (excluding scouts, siege, chiefs, and settlers), specifying base Offense (`atk`), Offense growth per point (`atkp`), Base Infantry Defense (`di`), Infantry Defense growth per point (`dip`), Base Cavalry Defense (`dc`), and Cavalry Defense growth per point (`dcp`) as defined in the canonical Terravia Renatus catalog (`catalog-data.json`).

#### 3. Building Catalog, Categories, Level Scaling & Prerequisite Hierarchy (`gid 1..50`)

- **FR-006**: The system MUST define all 50 building types (`gid 1..50`) organized across 6 functional categories and mapped to village slot boundaries (`Slot 1..18` for resource fields, `Slot 19..38` for inner village buildings, `Slot 39` fixed to Rally Point `gid 16`, `Slot 40` fixed to the tribe's Wall `gid 31, 32, 33, 42, 43, 47, 50`, and `Slot 99` fixed to World Wonder `gid 40` in WW villages):

##### Complete 50-Building Catalog & Dependency Validation Matrix

| GID | Building Name | Category | Max Level | Capital Rule | Tribe Restriction | Prerequisite Buildings (`GID >= Level` / `Exact`) | Multi-Instance (`Unique` / `RebuildAtLevel`) | Primary Functional Effect (`Lvl 1 -> Max`) |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **`1`** | Woodcutter | Resources | 20 (10 Non-Cap) | Cap > 10 | All | None | Multiple (`Slot 1..18` topology) | Base Wood production (`5 -> 2450`/hr at `1x` speed) |
| **`2`** | Clay Pit | Resources | 20 (10 Non-Cap) | Cap > 10 | All | None | Multiple (`Slot 1..18` topology) | Base Clay production (`5 -> 2450`/hr at `1x` speed) |
| **`3`** | Iron Mine | Resources | 20 (10 Non-Cap) | Cap > 10 | All | None | Multiple (`Slot 1..18` topology) | Base Iron production (`5 -> 2450`/hr at `1x` speed) |
| **`4`** | Cropland | Resources | 20 (10 Non-Cap) | Cap > 10 | All | None | Multiple (`Slot 1..18` topology) | Base Crop production (`5 -> 2450`/hr at `1x` speed) |
| **`5`** | Sawmill | Production Booster | 5 | Any | All | Woodcutter (`1`) >= 10, Main Building (`15`) >= 5 | Unique per village | `+5%` to `+25%` Wood production bonus |
| **`6`** | Brickyard | Production Booster | 5 | Any | All | Clay Pit (`2`) >= 10, Main Building (`15`) >= 5 | Unique per village | `+5%` to `+25%` Clay production bonus |
| **`7`** | Iron Foundry | Production Booster | 5 | Any | All | Iron Mine (`3`) >= 10, Main Building (`15`) >= 5 | Unique per village | `+5%` to `+25%` Iron production bonus |
| **`8`** | Grain Mill | Production Booster | 5 | Any | All | Cropland (`4`) >= 5 | Unique per village | `+5%` to `+25%` Crop production bonus |
| **`9`** | Bakery | Production Booster | 5 | Any | All | Main Building (`15`) >= 5, Cropland (`4`) >= 10, Grain Mill (`8`) >= 5 | Unique per village | Additional `+5%` to `+25%` Crop bonus (up to `+50%` with Grain Mill) |
| **`10`** | Warehouse | Storage | 20 | Any | All | Main Building (`15`) >= 1 | Additional allowed when an existing Warehouse == `20` | Stores Wood, Clay, Iron (`1200 -> 80000` per building at `1x` storage) |
| **`11`** | Granary | Storage | 20 | Any | All | Main Building (`15`) >= 1 | Additional allowed when an existing Granary == `20` | Stores Crop (`1200 -> 80000` per building at `1x` storage) |
| **`12`** | Blacksmith | Military | 20 | Any | All | Academy (`22`) >= 3, Main Building (`15`) >= 3 | Unique per village | Upgrades unit weapon/offense levels (`1..20`) |
| **`13`** | Armoury | Military | 20 | Any | All | Main Building (`15`) >= 3, Academy (`22`) >= 1 | Unique per village | Upgrades unit armor/defense levels (`1..20`) |
| **`14`** | Tournament Square | Military | 20 | Any | All | Rally Point (`16`) >= 15 | Unique per village | Increases troop movement speed after `20` tiles (`+10%` to `+200%` over base) |
| **`15`** | Main Building | Civic / Expansion | 20 | Any | All | None | Unique per village | Reduces building construction times (`100% -> 50%`); unlocks demolition at `Level >= 10` |
| **`16`** | Rally Point | Military | 20 | Any | All | None (Fixed at `Slot 39`) | Unique (`Slot 39`) | Coordinates troop movements, reveals incoming troop types vs. building level, dual catapult targets at `Level 20` |
| **`17`** | Marketplace | Civic / Expansion | 20 | Any | All | Main Building (`15`) >= 3, Warehouse (`10`) >= 1, Granary (`11`) >= 1 | Unique per village | Provides `1` merchant per level (`1..20`) for resource trading and trade routes |
| **`18`** | Embassy | Civic / Expansion | 20 | Any | All | Main Building (`15`) >= 1 | Unique per village | Allows joining an alliance at `Level 1`, founding an alliance at `Level 3`, max `3 x Level` members (up to `60`) |
| **`19`** | Barracks | Military | 20 | Any | All | Main Building (`15`) >= 3, Rally Point (`16`) >= 1 | Unique per village | Trains infantry units; reduces infantry training duration down to `20%` at `Level 20` |
| **`20`** | Stable | Military | 20 | Any | All | Blacksmith (`12`) >= 3, Academy (`22`) >= 5 | Unique per village | Trains cavalry units; reduces cavalry training duration down to `20%` at `Level 20` |
| **`21`** | Workshop | Military | 20 | Any | All | Academy (`22`) >= 10, Main Building (`15`) >= 5 | Unique per village | Trains siege engines (Rams and Catapults); reduces training duration down to `20%` at `Level 20` |
| **`22`** | Academy | Military | 20 | Any | All | Main Building (`15`) >= 3, Barracks (`19`) >= 3 | Unique per village | Researches new unit types for the village |
| **`23`** | Cranny | Civic / Expansion | 10 | Any | All | None | Additional allowed when an existing Cranny == `10` | Hides resources from enemy raids (`100 -> 1000` per resource; `2x` for Gauls) |
| **`24`** | Town Hall | Civic / Expansion | 20 | Any | All | Academy (`22`) >= 10, Main Building (`15`) >= 10 | Unique per village | Hosts Small Celebrations (`Level >= 1`) and Great Celebrations (`Level >= 10`) for Culture Points and loyalty bonuses |
| **`25`** | Residence | Civic / Expansion | 20 | Any | Not Huns (`!= 6`) | Main Building (`15`) >= 5, no Palace (`26`), no Command Center (`44`) | Unique per village | Prevents conquest while standing; unlocks `2` expansion slots (`Level 10` and `Level 20`) |
| **`26`** | Palace | Civic / Expansion | 20 | Any (1 per account) | Not Huns (`!= 6`) | Embassy (`18`) >= 1, Main Building (`15`) >= 5, no Residence (`25`), no Palace in any owned village | Unique across entire player account | Allows designating village as Capital; unlocks `3` expansion slots (`Levels 10, 15, 20`) |
| **`27`** | Treasury | Civic / Expansion | 20 | Not WW village | All | Main Building (`15`) >= 10 | Unique per village | Holds Artifacts (`Small` at `Level 10`, `Large/Unique/WW Plan` at `Level 20`) |
| **`28`** | Trade Office | Civic / Expansion | 20 | Any | All | Marketplace (`17`) == 20, Stable (`20`) >= 10 | Unique per village | Increases each merchant's carry capacity by `+10%` per level (`110%` at Lvl 1 up to `300%` at Lvl 20) |
| **`29`** | Great Barracks | Military | 20 | Non-Capital Only | All | Barracks (`19`) == 20 | Unique per village | Trains infantry concurrently with Barracks at `3x` resource cost |
| **`30`** | Great Stable | Military | 20 | Non-Capital Only | All | Stable (`20`) == 20 | Unique per village | Trains cavalry concurrently with Stable at `3x` resource cost |
| **`31`** | City Wall | Walls | 20 | Any | Romans (`1`) | None (Fixed at `Slot 40`) | Unique (`Slot 40`) | `+3%` base defense bonus per level (`1.03^L`), low durability against rams |
| **`32`** | Earth Wall | Walls | 20 | Any | Teutons (`2`) | None (Fixed at `Slot 40`) | Unique (`Slot 40`) | `+2%` base defense bonus per level (`1.02^L`), highest durability against rams |
| **`33`** | Palisade | Walls | 20 | Any | Gauls (`3`) | None (Fixed at `Slot 40`) | Unique (`Slot 40`) | `+2.5%` base defense bonus per level (`1.025^L`), medium durability against rams |
| **`34`** | Stonemason's Lodge | Civic / Expansion | 20 | Capital Only | All | Main Building (`15`) >= 5, Residence (`25`) == 0, and Palace (`26`) >= 3 (or Command Center `44` >= 3 for Huns) | Unique per village | Increases structural durability of all buildings in the capital against catapults by `10%` per level (up to `+200%` / `3x` stability at `Level 20`) |
| **`35`** | Brewery | Civic / Expansion | 10 | Capital Only | Teutons (`2`) | Granary (`11`) == 20, Rally Point (`16`) >= 10 | Unique per village | Hosts Mead Festival (`+1%` troop attack bonus per level for 72h; halves chief loyalty drop; catapults aim randomly) |
| **`36`** | Trapper | Military | 20 | Any | Gauls (`3`) | Rally Point (`16`) >= 1 | Additional allowed when an existing Trapper == `20` | Builds up to `400` traps (`u99`) per Level 20 building to capture incoming raiders/attackers |
| **`37`** | Hero's Mansion | Military | 20 | Any | All | Main Building (`15`) >= 3, Rally Point (`16`) >= 1 | Unique per village | Annexes up to `3` Oases within a `7x7` radius at `Levels 10, 15, and 20` |
| **`38`** | Great Warehouse | Storage | 20 | WW / Artifact | All | Main Building (`15`) >= 10, and either Archon/WW village OR active Great Storage Artifact | Additional allowed when an existing Great Warehouse == `20` | Stores `3x` standard Warehouse capacity (`3600 -> 240000` at `1x` storage) |
| **`39`** | Great Granary | Storage | 20 | WW / Artifact | All | Main Building (`15`) >= 10, and either Archon/WW village OR active Great Storage Artifact | Additional allowed when an existing Great Granary == `20` | Stores `3x` standard Granary capacity (`3600 -> 240000` at `1x` storage) |
| **`40`** | Wonder of the World | Civic / Expansion | 100 | WW Village (`Slot 99`) | All | Located in WW village (`Slot 99`); `Level 1..49` requires 1 active WW Building Plan owned by player; `Level 50..100` requires 2nd active WW Building Plan in the player's Alliance | Unique (`Slot 99`) | Endgame victory condition: reaching `Level 100` wins the server round |
| **`41`** | Horse Drinking Trough | Military | 20 | Any | Romans (`1`) | Stable (`20`) == 20, Rally Point (`16`) >= 10 | Unique per village | Speeds Roman cavalry training by `1%`/lvl; reduces crop upkeep of `u4/u5/u6` by `1`/hr at `Levels 10/15/20` |
| **`42`** | Stone Wall | Walls | 20 | Any | Egyptians (`7`) | None (Fixed at `Slot 40`) | Unique (`Slot 40`) | `+3%` defense bonus per level (`1.03^L`), high durability (`5x` Earth Wall profile) |
| **`43`** | Makeshift Wall | Walls | 20 | Any | Huns (`6`) | None (Fixed at `Slot 40`) | Unique (`Slot 40`) | `+1.5%` defense bonus per level (`1.015^L`), low durability |
| **`44`** | Command Center | Civic / Expansion | 20 | Any | Huns (`6`) | Main Building (`15`) >= 5, Residence (`25`) == 0, Palace (`26`) == 0 | Unique per village | Hun replacement for Residence/Palace: unlocks `3` expansion slots (`Levels 10, 15, 20`) in any village and satisfies Stonemason prerequisite at `Level 3` |
| **`45`** | Waterworks | Production Booster | 20 | Any | Egyptians (`7`) | Hero's Mansion (`37`) >= 10 | Unique per village | Increases production bonus of all annexed Oases by `+5%` per level (`+5% -> +100%`) |
| **`46`** | Hospital | Military | 20 | Any | Not Spartans/Vikings (`!= 8, 9`) | Main Building (`15`) >= 10, Academy (`22`) >= 15 | Unique per village | Recovers battle casualties as wounded troops that can be healed at reduced time/cost |
| **`47`** | Defensive Wall | Walls | 20 | Any | Spartans (`8`) | None (Fixed at `Slot 40`) | Unique (`Slot 40`) | `+2.8%` defense bonus per level (`1.028^L`), Spartan defensive barrier |
| **`48`** | Big Hospital | Military | 20 | Any | Spartans (`8`), Vikings (`9`) | Main Building (`15`) >= 10, Academy (`22`) >= 15 | Unique per village | Enhanced casualty recovery and faster healing queue for Spartans and Vikings |
| **`49`** | Great Workshop | Military | 20 | Non-Capital Only | All (when enabled in round config) | Workshop (`21`) == 20 | Unique per village | Trains siege engines concurrently with Workshop at `3x` resource cost |
| **`50`** | Barricade | Walls | 20 | Any | Vikings (`9`) | None (Fixed at `Slot 40`) | Unique (`Slot 40`) | `+2.2%` defense bonus per level (`1.022^L`), Viking defensive barrier |

- **FR-007**: For every building `gid 1..50` and every valid level (`1..20`, `1..5`, `1..10`, or `1..100` for World Wonder `gid 40`), the catalog MUST provide exact per-level values from the canonical Terravia Renatus building catalog (`catalog-data.json`):
  1. `wood`, `clay`, `iron`, `crop` construction cost.
  2. `pop`: Incremental population (crop consumption) added upon completing that level.
  3. `cp`: Daily Culture Points generated at that building level (scaled by server speed rules).
  4. `time`: Base construction time in seconds at Main Building Level 1 on `1x` server speed, modified by the village's current Main Building level multiplier (`0.964^(MainBuildingLevel - 1)`, or `5x` slower when Main Building is Level 0) and divided by the round's `Speed` multiplier.
  5. `attr` (and `trn` where applicable): The building's functional effect value at that level (e.g., hourly resource output, percentage bonus, storage capacity before `STORAGE_MULTIPLIER`, training duration multiplier, wall defense bonus, or trap capacity).

#### 4. Construction Queue, Roman Dual-Queue, Master Builder & Demolition Rules

- **FR-008**: **Standard vs. Roman Concurrent Construction**:
  - A non-Roman player without a Plus Subscription may have at most **1** active construction job (`basic` slot) across all slots (`Slot 1..40, 99`).
  - A **Roman** player (`Tribe 1`, or any player when a round's `AllowAllTribeRomanQueue` config is enabled) may run **2** concurrent active constructions: **1** Resource Field upgrade (`Slot 1..18`) and **1** Inner Village Building upgrade (`Slot 19..40, 99`) simultaneously, each progressing on its own independent timer.
- **FR-009**: **Plus Subscription Waiting Loop (`IsWaitingLoop = true`)**:
  - When a player has an active Plus Subscription, they gain **1** additional waiting-loop slot (`plus` slot).
  - For non-Roman players, the waiting-loop job starts automatically after the currently active job finishes, plus a transition delay of `ceil(60 / Speed)` seconds.
  - For Roman players, a waiting-loop job on a Resource Field (`Slot 1..18`) chains after the last Resource Field job (`timestamp + ceil(60 / Speed) + duration`), while a waiting-loop job on an Inner Building (`Slot 19..40, 99`) chains after the last Inner Building job (`timestamp + ceil(60 / Speed) + duration`).
  - Resources for both active and Plus waiting-loop jobs MUST be deducted immediately when the player places the order.
- **FR-010**: **Master Builder Queue Reservations (`IsMasterBuilder = true`)**:
  - Players may queue up to the configured maximum number of Master Builder reserved slots (`MAX_MASTER_BUILDER`, default up to 3 reservations) when active/Plus slots are full or when village resources are currently insufficient (provided Warehouse and Granary capacities are large enough to hold the required cost).
  - Reserving a Master Builder slot MUST NOT deduct Wood, Clay, Iron, Crop, or Gold at the moment of reservation.
  - Whenever an active construction slot becomes available (for that slot category) AND the village's available resources meet the required upgrade cost AND the player holds the required Gold balance (default `1` Gold per Master Builder activation), the earliest eligible Master Builder job MUST automatically transition to an active construction (`IsMasterBuilder = false`), deduct the required Wood, Clay, Iron, Crop, and Gold, and schedule its completion timer.
- **FR-011**: **Instant Completion with Gold ("Finish All")**:
  - Players may spend Gold (`2` Gold for active/Plus queues, active demolition, and Academy/Blacksmith/Armoury researches; `3` Gold if both Master Builder and standard/Plus jobs are completed together) to immediately complete active constructions, active timed demolition, and researches in the current village, subject to three strict exclusions:
    1. If the village is a World Wonder village (`Slot 99` contains `gid 40`), instant Gold completion is completely disabled for **all** buildings in that village.
    2. Residence (`gid 25`), Palace (`gid 26`), and World Wonder (`gid 40`) upgrades MUST NEVER be completed instantly with Gold in any village.
    3. Any Master Builder jobs included in a "Finish All" action MUST still verify building prerequisites (`MeetsPrerequisites(gid)`), verify and deduct the full Wood/Clay/Iron/Crop upgrade cost from the village, and charge the Master Builder Gold fee.
- **FR-012**: **Level-by-Level & Instant Complete Demolition (`Main Building >= 10`)**:
  - A village can initiate building demolition only when its Main Building (`gid 15`) is at **Level 10 or higher** and only for inner village slots (`Slot 19..40`, plus `Slot 99` in Archon/WW villages) that do not currently have an active or Plus-loop upgrade in progress.
  - **Embassy Safeguard**: If the target building is an Embassy (`gid 18`), the player is the owner/founder of an Alliance with `> 1` member (`IsAllianceOwner && AllianceMemberCount > 1`), the current Embassy level equals the minimum level required to hold that member count (`max(3, ceil(AllianceMemberCount / 3.0))`), and the player owns no other Embassy across their villages at `>=` that minimum required level, demolition (both timed and 10-Gold instant) MUST be rejected (`CannotDemolishLastAllianceEmbassy`).
  - **Timed 1-Level Demolition**: Only **1** timed demolition job may be active per village at any time. Each timed demolition job reduces the target building's level by exactly `1` after a duration equal to one-half (`floor(buildTime / 2)`) of the current level's construction time (or immediately when finished via the 2-Gold "Finish All" action), with zero resource refund. When a building is demolished from Level 1 to Level 0, its building type (`gid`) on that slot is reset to `0` (empty slot), any pending build queue items for that slot are cleared, and village population, storage capacity, and Culture Points are recalculated.
  - **10-Gold Instant Complete Demolition**: For `10` Gold, a player may immediately raze a target building from its current level $L \ge 1$ directly to Level `0` (`gid = 0`). This cancels any active demolition in the village, deducts the cumulative population sum $\sum_{i=1}^{L} \text{pop}(i)$ across all demolished levels, recalculates Warehouse/Granary capacity if `gid` is in `{10, 11, 38, 39}`, and recalculates village Culture Points.

### Key Entities

- **TribeDefinition**: Represents one of the 9 factions (`TribeId 1..9`), storing display name, playable flag, merchant speed, merchant carry capacity, cranny multiplier, wall `BuildingTypeId`, exclusive `BuildingTypeIds`, and cultural trait flags (e.g., `HasRomanDualBuildQueue`, `ExpansionSlotLevels`).
- **UnitDefinition**: Represents one of the 90 unit types (`UnitTypeId 1..90`) or Trap (`99`), storing `TribeId`, unit slot (`1..10`), tactical category (`Infantry`, `Cavalry`, `Scout`, `Ram`, `Catapult`, `Chief`, `Settler`, `Trap`), combat stats (`Offense`, `DefenseInfantry`, `DefenseCavalry`), resource cost (`Wood`, `Clay`, `Iron`, `Crop`), `UpkeepCropPerHour`, `SpeedTilesPerHour`, `BaseTrainingSeconds`, `CarryCapacity`, `HorseDrinkingTroughLevelThreshold`, and Academy/Building research prerequisites.
- **HeroUnitTemplate**: Represents the base hero stat scaling profile (`h1..h86`) associated with eligible playable unit types, storing base and per-attribute-point growth rates for offense, infantry defense, and cavalry defense.
- **BuildingDefinition**: Represents one of the 50 building types (`BuildingTypeId 1..50`), storing category (`Resource`, `Booster`, `Storage`, `Military`, `Wall`, `Civic`), `MaxLevel`, slot placement rules (`ResourceField`, `InnerSlot`, `RallyPointSlot39`, `WallSlot40`, `WorldWonderSlot99`), capital restriction (`Any`, `CapitalOnly`, `NonCapitalOnly`), tribe allow/deny lists, prerequisite building rules (`MinLevels`, `ExactLevels`, `MutualExclusions`, `RequiresArtifactOrArchon`, `RequiresWwPlan`), uniqueness/rebuild rules (`IsUnique`, `RebuildAtLevel`, `AccountWideUnique`), and an indexed array of `BuildingLevelStats` (`Level 1..MaxLevel`).
- **BuildingLevelStats**: Value object for a specific `(BuildingTypeId, Level)` containing `WoodCost`, `ClayCost`, `IronCost`, `CropCost`, `PopulationDelta`, `CulturePointsPerDay`, `BaseBuildTimeSeconds`, and `EffectValue`.
- **ConstructionQueueState**: Represents a village's active, Plus-loop, Master Builder, and Demolition queue items, enforcing concurrency limits, Roman dual-queue partitioning (`Slot <= 18` vs. `Slot >= 19`), target level calculation across queued jobs, and resource/Gold deduction timing.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: **100% Catalog & Formula Verification**: Automated verification suites confirm that 100% of the 9 tribes, 90 unit types (`u1..u90` + `u99`), 35 hero unit templates (`h1..h86`), and 50 building types across all defined levels (`gid 1..50`) match the canonical Terravia Renatus catalog schema (`catalog-data.json`) with zero discrepancies in costs, combat stats, speeds, capacities, population yields, culture points, or construction durations.
- **SC-002**: **100% Prerequisite & Restriction Accuracy**: All 50 building prerequisite rules—including tribe-exclusive buildings, Hun Command Center replacements (`gid 44` replacing `gid 25/26` and unlocking Stonemason `gid 34`), Spartan/Viking Big Hospital replacements (`gid 48` replacing `gid 46`), Capital vs. Non-Capital restrictions, multi-instance rebuild thresholds (`Warehouse`, `Granary`, `Cranny`, `Trapper`, `Great Storage`), account-wide Palace uniqueness, and multi-queued World Wonder plan checks (`Level >= 50` requiring 2nd plan)—pass 100% of positive and negative boundary test cases.
- **SC-003**: **Deterministic Queue & Demolition Integrity**: Simulation tests of standard, Plus Subscription waiting-loop, Roman dual-build, Master Builder reservation, and Main Building demolition workflows verify zero resource leaks, zero invalid concurrent builds, and exact millisecond-accurate completion ordering across 1,000+ randomized command sequences.
- **SC-004**: **Instant Player & AI Lookup Performance**: Players and automated decision agents can inspect complete catalog definitions and evaluate building eligibility for all 40 village slots in under 1 millisecond per village evaluation.

## Assumptions

- **Canonical Catalog Schema**: The embedded Terravia Renatus catalog dataset (`specs/001-core-game-catalog/catalog-data.json`) is the single authoritative source of truth for all numerical balance values, unit stats, building level tables, and prerequisite rules.
- **Speed & Storage Multipliers**: All baseline catalog times and capacities are defined at `1x` server speed and `1x` storage multiplier (`80,000` base Warehouse/Granary capacity at Level 20), and scale deterministically with the active round's `Speed`, `TroopSpeed`, and `StorageMultiplier` configuration values.
- **Separate Blacksmith (`gid 12`) & Armoury (`gid 13`)**: Blacksmith (`gid 12`) exclusively upgrades unit offense (`1..20`) and Armoury (`gid 13`) exclusively upgrades unit defense (`1..20`).
- **World Wonder Building Plans**: World Wonder (`gid 40`) upgrades from Level 0 to Level 49 require an active World Wonder Building Plan artifact controlled by the village owner, and upgrades from Level 50 to Level 100 require a second active World Wonder Building Plan held by another member of the same Alliance (inspected against the highest level currently built or queued in `Slot 99`).
