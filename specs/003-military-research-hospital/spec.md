# Feature Specification: Military Recruitment, Technological Research, Specialty Crafting, and Hospital Casualty Recovery

**Feature Branch**: `003-military-research-hospital`  
**Created**: 2026-11-06  
**Status**: Ready for Planning  
**Input**: User description: "Develop the military recruitment, technological research, specialty crafting, and hospital casualty recovery systems for Terravia Renatus."

## Clarifications

### Session 2026-11-07

- Q: Under what crop deficit condition should a village be blocked from starting new military training or hospital healing batches? (FR-005) → A: Option A — Granary Stockpile Gate (Default: allow training/healing whenever stored crop > 0 and covers upfront batch crop cost, even if net hourly crop production is negative) + Optional `BlockTrainingOnNegativeCrop` world setting (`Default = false`).
- Q: What should happen to un-healed wounded troops already stored in a village's Hospital pool (`w1..w8`) if the Hospital (`gid 46`/`48`) is damaged or demolished so that current wounded exceed the reduced building capacity? (FR-011) → A: Option A — Retain existing wounded in `w1..w8` (soft cap on new admissions: `freeCapacity = max(0, MaxCapacity(L) - sum(w1..w8))`); if Hospital reaches Level 0, it must be rebuilt to Level >= 1 before new healing batches can be queued.
- Q: When a Blacksmith (`b1..b8`) or Armoury (`a1..a8`) upgrade completes in a home village, how should that upgrade apply to armies already marching in transit or stationed as foreign reinforcements? (FR-009) → A: Option B — Snapshot at Dispatch Only: Outbound movements and stationed reinforcements lock in the home village's Blacksmith/Armoury levels at the moment of dispatch until they return home and re-deploy.
- Q: Which Sitter permission flags (`SitterPermissions` / `TerraviaPermissions.Sitter`) should authorize a delegated Sitter to queue military training, Hospital healing, Academy/Smithy research, Gold instant research completion, and Trapper prisoner release/dissolution? (FR-005) → A: Option A — Map to existing bitmask: require `BuildAndDemolish` (`1 << 6`) for queuing training, healing, traps, and Academy/Smithy upgrades; `BuyAndSpendGold` (`1 << 3`) for `2` Gold instant research completion; and `Reinforce` (`1 << 1`) for Rally Point prisoner release/dissolution.
- Q: Which exact formula should govern the number of destroyed traps automatically queued for free (`0`-cost) repair when prisoners are liberated by combat or dissolved by the troop owner? (FR-015) → A: Option A — Canonical `1/3` Free Trap Repair (`round(totalFreed / 3)`) upon combat liberation or attacker dissolution (with `round(count / 4)` = `25%` liberated prisoner mortality), and `100%` (`totalFreed`) upon manual release by the Gaul trap owner.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Military Training Queues, Great Military Buildings & Civic Expansion Training (Priority: P1)

A player recruits infantry, cavalry, siege engines, and civic expansion units across dedicated village training facilities. Each facility maintains its own concurrent FIFO queue, while non-capital villages with Great Barracks (`gid 29`), Great Stable (`gid 30`), or Great Workshop (`gid 49`) can run a second parallel queue at `3x` resource cost. Units graduate sequentially one-by-one (`floor(elapsed / eachtime)`), immediately joining the village garrison and increasing village crop upkeep in real time. Initiated training batches cannot be cancelled to hide resources from incoming raids, and recruitment requests are gated by available village resources, unlocked expansion slots, and safe crop deficit rules.

**Why this priority**: Core military recruitment and expansion unit production are foundational to army building, village founding/conquest, and resource consumption across all playable tribes.

**Independent Test**: Can be independently tested by queuing unit batches across Barracks (`gid 19`), Great Barracks (`gid 29`), Stable (`gid 20`), Great Stable (`gid 30`), Workshop (`gid 21`), Great Workshop (`gid 49`), and Residence/Palace/Command Center (`gid 25`/`26`/`44`), advancing the simulation clock in 1-second increments, and verifying sequential single-unit graduation, `3x` Great Military cost deduction, expansion-slot limits, and non-cancellable queue enforcement.

**Acceptance Scenarios**:

1. **Given** a village with a Barracks (`gid 19`), Stable (`gid 20`), and Workshop (`gid 21`), **When** the player queues infantry in the Barracks, cavalry in the Stable, and siege engines in the Workshop, **Then** all three queues advance concurrently in parallel while batches within the same facility execute strictly in FIFO order.
2. **Given** a non-capital village with both a Barracks (`gid 19`) and a Great Barracks (`gid 29`), **When** the player queues infantry in the Great Barracks, **Then** the engine deducts exactly `3x` the unit's base Wood, Clay, Iron, and Crop cost (`unit_cost * 3 * amount`) and processes the Great Barracks queue in parallel with the standard Barracks queue.
3. **Given** an active training batch of `10` units with per-unit training duration `eachtime = 60` seconds queued at $t = 0$, **When** the simulation clock reaches $t = 150$ seconds, **Then** `floor(150 / 60) = 2` units are immediately added to the village garrison (`StackType = Home`), the active batch's remaining amount is decremented to `8`, the next unit threshold `commence` advances by `2 * 60 = 120` seconds, and the village's net crop upkeep increases by `2 * unit_pop`.
4. **Given** consecutive training batches in the same facility for the **same** unit type with identical `eachtime` and `QueueCategory`, **When** the player submits the new batch, **Then** the engine merges the new batch into the trailing queue entry by adding `amount` to `AmountRemaining` and extending `EndAt` by `eachtime * amount`.
5. **Given** a village with a Residence (`gid 25`) at Level `10` (1 unlocked expansion slot) and `2` Settlers already in the garrison, **When** the player attempts to queue `2` additional Settlers or `1` Chief/Senator, **Then** the engine clamps or rejects the request so that total expansion slot usage (`1` Chief/Senator or `3` Settlers per unlocked slot across garrison, training queue, movements, reinforcements, traps, and founded/conquered daughter villages) never exceeds `1` slot, and blocks submitting both a Chief (`u*9`) and a Settler (`u*0`) in a single training request.
6. **Given** an active military training batch or a village experiencing crop starvation / insufficient resources, **When** a player attempts to cancel an active training batch to hide resources from an incoming raid or attempts to queue troops whose cost exceeds available resources or violates the crop deficit threshold, **Then** the engine rejects the cancellation and clamps or blocks the training submission.

---

### User Story 2 - Training Accelerators & Stacking Multipliers (Priority: P1)

A player accelerates unit recruitment using higher-level training buildings, the Roman Horse Drinking Trough (`gid 41`), Training Speed Artifacts (`Academic Advancement` / `Trainer` kind `5`), Hero Recruitment Helmets (`ItemId 10..15`), and the T4 Alliance Recruitment Bonus (`Levels 1..5`). The engine calculates per-unit training duration (`eachtime`) through a deterministic 4-stage rounding pipeline with a strict minimum floor of `1` second per unit.

**Why this priority**: Training speed multipliers govern military replenishment rates and late-game army scaling; any divergence in rounding order or stacking order breaks simulation parity.

**Independent Test**: Can be independently tested by evaluating `getTrainingTime(facility, unitId)` across combinations of building levels (`1..20`), Horse Drinking Trough levels (`0..20`), Small/Large/Unique Trainer Artifacts (`0.5x`, `0.75x`, `0.5x`), Hero Infantry/Cavalry Helmets (`-10%`, `-15%`, `-20%`), and Alliance Recruitment Bonus levels (`1..5`, including new-member progressive unlock timers) and verifying exact integer-second parity.

**Acceptance Scenarios**:

1. **Given** a Roman village with a Stable (`gid 20`) at Level `20` (`attri = 13.51`) and a Horse Drinking Trough (`gid 41`) at Level `20` (`attri = 1.20`, reducing cavalry training time by `1%` per level via `1 / 1.20`), **When** calculating the base building step $t_1$ for Equites Imperatoris (`u5`, base time `2640`s) at `1x` speed, **Then** $t_1 = \text{round}(((13.51 / 1.20) / 100) \times 2640 / 1) = 297$ seconds, and its crop upkeep is reduced by `1` per unit per day in that village.
2. **Given** a village affected by a Small Trainer Artifact (`0.5x`), a Hero wearing a Helmet of the Ruler (`ItemId 15`, `-20%` infantry training time) stationed in the village, and an Alliance Recruitment Bonus at Effective Level `5` (`+10%` speed, divisor `1.10`), **When** queuing an infantry unit with $t_1 = 100$ seconds, **Then** the engine computes $t_2 = \max(1, \text{round}(100 \times 0.5)) = 50$, $t_3 = \max(1, \text{round}(50 \times 0.80)) = 40$, and $t_4 = \max(1, \text{round}(40 / 1.10)) = 36$ seconds.
3. **Given** a Hero wearing an Infantry or Cavalry Helmet stationed in a village, **When** the player queues siege engines in the Workshop (`gid 21`/`49`), expansion units in the Residence/Palace/Command Center (`gid 25`/`26`/`44`), or traps in the Trapper (`gid 36`), **Then** the Hero Helmet modifier is NOT applied to those facilities, whereas the Alliance Recruitment Bonus and Trainer Artifact DO apply to training queues (`gid 19, 20, 21, 25, 26, 29, 30, 44, 49`).
4. **Given** a player who joined an alliance `30` hours ago on a `1x` speed server where the alliance has unlocked Level `5` Recruitment Bonus, **When** evaluating the player's effective Alliance Recruitment Bonus, **Then** only Level `2` (`+4%`, divisor `1.04`, unlocked at `24h / Speed`) is active for that member until the `48h`, `72h`, and `96h` membership thresholds elapse.

---

### User Story 3 - Academy Unit Discovery & Blacksmith / Armoury Combat Upgrades (Priority: P1)

A player researches new unit types (`X2..X9`) in the Academy (`gid 22`) once building prerequisites are met, and upgrades unit offense (`b1..b8`) in the Blacksmith (`gid 12`) and unit defense (`a1..a8`) in the Armoury (`gid 13`) from Level `1` to `20`. Each upgrading building processes projects sequentially, caps upgrade levels at the current building level, and increases unit combat efficiency according to the canonical exponential pop-weighted formula.

**Why this priority**: Technological progression unlocks the unit roster beyond the default tier-1 infantry and Settler and scales unit combat power and siege durability damage across the game lifecycle.

**Independent Test**: Can be independently tested by verifying Academy prerequisite checks, queuing research (`t{unit}`) and Blacksmith/Armoury upgrades (`b1..b8`, `a1..a8`) with and without Plus waiting-loop slots, testing Gold instant completion (`2` Gold), and asserting exact combat stat output at levels `0..20`.

**Acceptance Scenarios**:

1. **Given** a newly founded village of any playable tribe, **When** inspecting unlocked unit technologies before constructing an Academy, **Then** the tier-1 base infantry (`u1, u11, u21, u41, u51, u61, u71, u81`) and Settler (`u10, u20, u30, u50, u60, u70, u80, u90`) are unlocked by default, while slots `X2..X9` require Academy (`gid 22`) discovery.
2. **Given** a village with an Academy (`gid 22`) and prerequisite buildings matching a unit's catalog requirements, **When** the player initiates Academy research for that unit (`t{unit}`), **Then** the engine deducts the cataloged `r{unit}` resources, schedules completion at $\text{round}(\text{BaseTime} \times (\text{attri}_{\text{Academy}}(L) / 100) / \text{Speed})$, and rejects any additional Academy research in that village until the active research finishes.
3. **Given** a village with a Blacksmith (`gid 12`) at Level `10` and Armoury (`gid 13`) at Level `8`, **When** the player queues offense upgrades (`b1..b8`) and defense upgrades (`a1..a8`) for researched combat units (`X1..X8`), **Then** Blacksmith and Armoury queues run concurrently in their respective buildings, enforce `CurrentLevel + QueuedUpgrades <= BuildingLevel`, and allow 1 active upgrade (or 1 active + 1 Plus waiting-loop upgrade with a `+ ceil(60 / Speed)` second delay when Plus is active).
4. **Given** a combat unit with base stat $S \in \{\text{atk}, \text{di}, \text{dc}\}$, crop upkeep $\text{pop}$, and Blacksmith/Armoury level $L \in [1..20]$, **When** computing its upgraded combat stat for battle resolution, **Then** the engine evaluates $\text{round}(S + (S + 300 \times \text{pop} / 7) \times (1.007^L - 1), 4)$ (and $\text{round}(200 \times 1.0205^L) / 200$ for Ram/Catapult structural demolition upgrades).
5. **Given** an active Academy research or Blacksmith/Armoury upgrade in a village, **When** the player triggers "Finish All" instant completion for `2` Gold, **Then** the active Academy and Blacksmith/Armoury projects complete immediately, whereas active military training, Trapper, and Hospital healing queues remain unaffected.

---

### User Story 4 - Hospital & Big Hospital Casualty Interception & Healing Queue (Priority: P2)

When a village constructs a Hospital (`gid 46`, non-Spartan/Viking) or a Spartan/Viking Big Hospital (`gid 48`), a percentage of its combat unit casualties (`40%` for Hospital, `50%` for Big Hospital) from both home village defense and outbound village attacks are intercepted and placed into the village's Wounded Troops pool (`StackType = Hospital`) up to the Hospital's level-based capacity. Players can heal wounded troops back into active garrison status via a dedicated Healing Queue at `50%` of base resource cost and Hospital-scaled healing duration.

**Why this priority**: Casualty recovery is a defining strategic mechanic for sustaining armies and differentiates Spartans and Vikings (`Tribe 8` and `Tribe 9`) via the exclusive Big Hospital (`gid 48`).

**Independent Test**: Can be independently tested by resolving offensive and defensive battles for villages with a Hospital (`gid 46`) or Big Hospital (`gid 48`), verifying casualty interception (`floor(dead * rate)`), slot-ordered capacity clamping (`Level * 30` vs. `Level * 60`), zero crop upkeep while wounded, and Healing Queue graduation back to the home garrison.

**Acceptance Scenarios**:

1. **Given** a village with a Hospital (`gid 46`) at Level `10` (capacity `10 * 30 = 300`, save rate `40%`) or a Spartan/Viking village with a Big Hospital (`gid 48`) at Level `10` (capacity `10 * 60 = 600`, save rate `50%`), **When** the village's own troops suffer casualties defending the home village or attacking another village, **Then** for each combat unit slot `X1..X8` (`unit % 10 in 1..8`), the engine computes $\text{wounded} = \lfloor \text{dead} \times \text{rate} \rfloor$ and admits them into the village's `StackType = Hospital` pool.
2. **Given** a battle where Chiefs/Senators (`X9`), Settlers (`X0`), Traps (`u99`), Heroes, or foreign reinforcements suffer casualties, **When** evaluating Hospital casualty interception, **Then** none of those non-eligible units or foreign reinforcements are admitted to the target village's Hospital pool.
3. **Given** a Hospital (`gid 46`) at Level `5` (max capacity `150`) that already holds `120` wounded troops (`free = 30`), **When** a battle generates `25` wounded slot-1 infantry (`X1`) and `20` wounded slot-2 infantry (`X2`), **Then** the engine admits all `25` slot-1 infantry and clamps slot-2 infantry to the remaining `5` free slots (`120 + 25 + 5 = 150`), discarding any overflow beyond capacity.
4. **Given** wounded troops residing in the Hospital pool (`StackType = Hospital`) or queued in the Healing Queue (`QueueCategory = Healing`), **When** calculating village crop consumption and defensive battle participation, **Then** wounded troops consume `0` crop upkeep and do not participate in combat until each individual unit graduates from the Healing Queue into the `Home` garrison.
5. **Given** a player healing `amt` wounded units of type `u` in a Hospital (`gid 46`) or Big Hospital (`gid 48`) at Level $L$, **When** the healing batch is submitted, **Then** the engine deducts `ceil(unit_cost * amt / 2)` (`50%` of base Wood, Clay, Iron, Crop cost), removes `amt` units from the Wounded pool, and schedules sequential unit graduation at `eachtime = max(1, round(unit_time * attri_Hospital(L) / Speed))` (or `50%` reduced baseline recruitment time at Level 7+ where `attri <= 0.5314`).

---

### User Story 5 - Gaul Trapper, Prisoner Capture, Combat Liberation & Auto-Repair (Priority: P2)

A Gaul (`Tribe 3`) player constructs one or more Trappers (`gid 36`, holding up to `400` traps per Level `20` Trapper) and queues Traps (`u99`) in a dedicated Trapper queue. Incoming hostile ground troops are captured by free traps (`total_traps - occupied_traps`) before combat begins. When a normal attack from the troop owner or an allied/confederate/NAP partner wins combat in the trapper village, trapped prisoners are liberated (`25%` of liberated prisoners die), all occupied traps holding them are destroyed, and a portion of those traps (`round(totalFreed / 3)`) is automatically queued for free repair without resource cost.

**Why this priority**: The Trapper is the signature defensive specialty facility of the Gaul tribe, requiring tight coordination between trap crafting, combat pre-interception, prisoner lifecycle management, and zero-cost automatic trap repair.

**Independent Test**: Can be independently tested by building a Trapper (`gid 36`) in a Gaul village, crafting traps (`u99`), executing hostile raids/attacks to capture prisoners, and testing all three prisoner release paths: (1) combat liberation by normal attack, (2) manual release by the Gaul trap owner, and (3) manual dissolution by the trapped troop owner.

**Acceptance Scenarios**:

1. **Given** a Gaul village with a Trapper (`gid 36`) at Level `20` (capacity `400` traps), **When** the player constructs a second Trapper (`gid 36`) in another building slot, **Then** the engine permits the additional Trapper only because an existing Trapper has reached Level `20`, increases total village trap capacity by the new Trapper's level capacity, and uses the highest Trapper level in the village to determine trap crafting speed (`attri(maxLevel)`).
2. **Given** a Gaul village with `F = total_traps - occupied_traps` free traps (`u99 - u99o`), **When** an incoming hostile ground army arrives on a **Normal Attack** (`AttackType = 3`) or **Raid** (`AttackType = 4`) (excluding **Reconnaissance/Scouting** missions `AttackType = 1`, which bypass traps), **Then** if `F >= totalAttackingUnits`, all attacking units (`t1..t11`, including Hero `t11`) are captured; otherwise (`F < totalAttackingUnits`), the Hero (`t11`) escapes capture and up to `F` troop units (`t1..t10`) are proportionally captured (`intval(count_i * F / totalAttackingUnits)` followed by sequential slot `1..10` remainder distribution capped at `count_i`), deducted from the attacking force before battle resolution, stored in a `Prisoners` record (`from -> wref`), and added to `occupied_traps` (`u99o`).
3. **Given** prisoners held in a Gaul village, **When** a **Normal Attack** (`AttackType = 3`, not a Raid) from the prisoner owner or an allied/confederate/NAP player attacks the Gaul village and at least `1` attacking unit survives combat, **Then** `25%` of each liberated prisoner stack dies (`surviving = count - round(count / 4)`), surviving prisoners from the **same attacking village** (`prisoner.from == attacker.from`) merge directly into the returning attacking army while prisoners from **other villages or allies/confederates/NAPs** embark on separate return movements (`MovementType = 4`) home, all `totalFreed` occupied traps (`u99` and `u99o`) are decremented, and `round(totalFreed / 3)` traps are automatically queued in the Trapper for free (`0` resource cost) if free capacity permits.
4. **Given** prisoners held in a Gaul village, **When** the **Gaul village owner** manually releases the prisoners from the Rally Point, **Then** `100%` of the prisoners survive and walk home (`MovementType = 4`), `totalFreed` is deducted from `u99` and `u99o`, and `100%` (`totalFreed`) of the traps are automatically queued for free repair (`0` resource cost).
5. **Given** prisoners held in a Gaul village, **When** the **trapped troop owner** manually dissolves/kills their own trapped troops from their Rally Point, **Then** `100%` of those prisoners (including any trapped Hero) die immediately without returning home, `totalFreed` is deducted from `u99` and `u99o` in the Gaul village, and `round(totalFreed / 3)` traps are automatically queued for free repair in the Gaul village.

---

### Edge Cases

- **Training or Healing Building Demolition While Queue Active or Pool Populated**: If a Barracks, Stable, Workshop, Great Military building, Residence/Palace/Command Center, Trapper, or Hospital is demolished or destroyed by catapults to Level `0` while its training/crafting/healing queue is active, all already-paid batches in that queue continue processing to completion at their locked-in `eachtime` duration; no new batches can be queued until the facility is rebuilt to Level $\ge 1$. Furthermore, if a Hospital (`gid 46`/`48`) is damaged or destroyed so that existing un-healed wounded (`sum(w1..w8)`) exceed the reduced `MaxCapacity(NewLevel)`, existing wounded troops in `w1..w8` are retained without loss, while `freeCapacity = max(0, MaxCapacity(NewLevel) - sum(w1..w8))` prevents new battle admissions until capacity is restored or wounded troops are healed.
- **Academy, Blacksmith, or Armoury Demolition While Research Active**: If an Academy (`gid 22`), Blacksmith (`gid 12`), or Armoury (`gid 13`) is destroyed during an active research/upgrade project, the active paid project completes normally and persists the unlocked technology or upgrade level in the village's technology record (`vdata` / `tdata` / `abdata`).
- **Simultaneous Chief and Settler Submission Guard**: If a player submits a single training request containing both a cultural leader (`u*9`: Senator, Chief, Chieftain, Archon Hierophant, Tribal Chief, Nomarch, Ephor, Jarl) and a Settler (`u*0`), the engine immediately aborts the request (`procTrain` simultaneous expansion guard) to prevent bypassing expansion-slot accounting.
- **Expansion Slot Accounting Across All States**: When validating whether a village can train a cultural leader (`1` slot) or Settlers (`1/3` slot each), the engine counts existing units in the home garrison, active training queues, outbound/returning movements, stationed reinforcements in other villages/oases, trapped prisoners in Gaul villages, plus already founded or conquered daughter villages (`slotsUsed`).
- **Hospital Overflow & Sequential Slot Priority**: When a battle produces more total wounded units (`sum_{slot=1..8} floor(dead_slot * rate)`) than the Hospital's remaining free capacity (`maxCapacity - currentWounded`), slots are filled strictly in ascending unit slot order (`X1` through `X8`). The slot that hits the cap is partially admitted up to `free`, and all subsequent slots receive `0` admissions.
- **Trapper Free Auto-Repair Capacity Cap**: When combat liberation or prisoner dissolution triggers automatic free trap repair (`round(totalFreed / 3)`), the engine checks `existingTraps + currentlyQueuedTraps < maxTrapperCapacity`. If the village's Trapper(s) were demolished by catapults during that same battle such that current + queued traps already meet or exceed the remaining Trapper capacity, the free repair batch is clamped or skipped so `u99` never exceeds structural capacity.
- **Hospital & Bandage Post-Battle Sequencing & Village Conquest/Razing Cleanup**: When an attacking Hero carries T4 Bandages (`Small Bandage` `id 206` at `25%` or `Bandage` `id 207` at `33%`), Bandages revive casualties on the battlefield first (`bandaged_s`), and the attacker's home Hospital (`40%` or `50%`) intercepts only the remaining un-bandaged casualties ($\text{deadAfterBandages}_s = \text{dead}_s - \text{bandaged}_s$). Furthermore, if a village is conquered by an enemy Cultural Leader or razed to `0` population, its `Hospital` wounded pool and active `Healing` queue are immediately cleared (`clearHospital`).
- **Archon Capital Auto-Trap Protection**: Any attack or scouting mission targeting the Archon (`Tribe 5`) Capital village (`owner == Archon && capital == 1`) automatically traps `100%` of all incoming units (`t1..t11`) prior to combat resolution.
- **Zero-Speed / Minimum 1-Second Training Floor**: Across high-speed servers (`Speed >= 100x`) combined with Level 20 facilities, Unique Trainer Artifacts (`0.5x`), Hero Helmets (`-20%`), and Alliance Recruitment Bonus (`Level 5`), every stage of the training/healing/crafting time pipeline enforces $\max(1, \dots)$ so `eachtime` is never `0` or negative, preventing division-by-zero in `floor(elapsed / eachtime)`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001 (Facility-to-Unit Assignment & Concurrent Training Queues)**: The engine MUST partition unit recruitment into independent, concurrent per-village FIFO queues keyed by `QueueCategory`:
  - `Barracks` (`gid 19`, `QueueCategory = Barracks`): Trains all tribe infantry units (`u1..u3, u11..u14, u21..u22, u31..u34, u41..u44, u51..u52, u61..u63, u71..u74, u81..u84`).
  - `Stable` (`gid 20`, `QueueCategory = Stable`): Trains all tribe cavalry units (`u4..u6, u15..u16, u23..u26, u35..u36, u45..u46, u53..u56, u64..u66, u75..u76, u85..u86`).
  - `Workshop` (`gid 21`, `QueueCategory = Workshop`): Trains all tribe siege engines (`u7..u8, u17..u18, u27..u28, u37..u38, u47..u48, u57..u58, u67..u68, u77..u78, u87..u88`).
  - `Residence` (`gid 25`), `Palace` (`gid 26`), or Hun `Command Center` (`gid 44`) (`QueueCategory = ResidencePalace`): Trains tribe cultural leaders (`u9, u19, u29, u39, u49, u59, u69, u79, u89`) and Settlers (`u10, u20, u30, u40, u50, u60, u70, u80, u90`).
  - `Trapper` (`gid 36`, `QueueCategory = Trapper`, Gauls `Tribe 3` only): Crafts Traps (`u99`).
  - `Hospital` (`gid 46`) or Spartan/Viking `Big Hospital` (`gid 48`) (`QueueCategory = Healing`): Heals wounded combat units (`X1..X8`).
- **FR-002 (Great Military Buildings & 3x Resource Cost)**: In **non-capital** villages (`CapitalFlag = false`, or when `GREAT_WKS` is enabled), players MUST be able to construct and queue units in `Great Barracks` (`gid 29`, `QueueCategory = GreatBarracks`), `Great Stable` (`gid 30`, `QueueCategory = GreatStable`), and `Great Workshop` (`gid 49`, `QueueCategory = GreatWorkshop`) once their standard counterpart reaches Level `20`. Units trained in Great Military facilities MUST cost exactly **`3x`** the base Wood, Clay, Iron, and Crop cost (`unit_cost * 3 * amount`), have identical `1x` per-unit base training time (`getTrainingTime` uses the Great building's level `1..20`), and run concurrently alongside the standard facility's queue.
- **FR-003 (Sequential Per-Unit Graduation & Batch Merging)**: Each training, crafting, or healing queue entry MUST store `UnitType`, `AmountRemaining`, `PopUpkeep`, `EachTime` (seconds per unit), `CommenceAt` (timestamp when the current unit started), `EndAt` (timestamp when the entire batch completes), and `QueueCategory`.
  - When a new batch of `amount` units is submitted to a `QueueCategory` whose trailing entry has the **same** `UnitType` and identical `EachTime`, the engine MUST merge the batch into that trailing entry (`AmountRemaining += amount`, `EndAt += EachTime * amount`). Otherwise, it appends a new entry with `CommenceAt = previousEntry.EndAt` (or `now` if the queue is empty) and `EndAt = CommenceAt + EachTime * amount`.
  - During tick resolution at timestamp $t \ge \text{CommenceAt}$, the engine MUST compute graduated units as:
    $$\text{trained} = \min\left(\text{AmountRemaining}, \left\lfloor \frac{t - \text{CommenceAt}}{\text{EachTime}} \right\rfloor\right)$$
  - When $\text{trained} \ge 1$, the engine MUST immediately credit $\text{trained}$ units to the village garrison (`StackType = Home`, or `u99` traps for `UnitType = 99`), increment village crop upkeep by $\text{trained} \times \text{PopUpkeep}$ in real time, advance $\text{CommenceAt} \leftarrow \text{CommenceAt} + \text{trained} \times \text{EachTime}$, and decrement $\text{AmountRemaining} \leftarrow \text{AmountRemaining} - \text{trained}$, deleting the row when $\text{AmountRemaining} = 0$.
- **FR-004 (Civic Expansion Slot Gating)**: Training cultural leaders (`u*9`) or Settlers (`u*0`) in `Residence` (`gid 25`), `Palace` (`gid 26`), or Hun `Command Center` (`gid 44`) MUST enforce expansion slot limits:
  - `Residence` (`gid 25`) unlocks `1` slot at Level `10` and a `2nd` slot at Level `20` ($\lfloor L / 10 \rfloor$, max `2` slots).
  - `Palace` (`gid 26`, non-Hun) and `Command Center` (`gid 44`, Huns `Tribe 6` only) unlock `1` slot at Level `10`, a `2nd` slot at Level `15`, and a `3rd` slot at Level `20` ($\lfloor (L - 5) / 5 \rfloor$, max `3` slots).
  - Each unlocked slot accommodates either **`1` Cultural Leader** (`u*9`) OR **`3` Settlers** (`u*0`), where even `1` or `2` existing Settlers occupy that slot against Cultural Leader training.
  - The engine MUST calculate remaining trainable Settler and Cultural Leader capacities as:
    $$\text{settlerSlots} = \max\left(0, 3 \times (\text{UnlockedSlots} - \text{SlotsUsedByChildVillages} - \text{TotalChiefs}) - \text{TotalSettlers}\right)$$
    $$\text{chiefSlots} = \begin{cases} 0 & \text{if } t_{\text{chief}} = 0 \\ \max\left(0, \text{UnlockedSlots} - \text{SlotsUsedByChildVillages} - \text{TotalChiefs} - \left\lfloor \frac{\text{TotalSettlers} + 2}{3} \right\rfloor\right) & \text{if } t_{\text{chief}} = 1 \end{cases}$$
    where `TotalChiefs` and `TotalSettlers` sum all expansion units of that village across the home garrison, active training queue, all outbound/returning troop movements, village and oasis reinforcements, and Gaul prisoner traps (`u9, u19, u29, u39, u49, u59, u69, u79, u89` and `u10..u90`).
  - Submitting both a Cultural Leader (`u*9`) and a Settler (`u*0`) in the same training request MUST be rejected.
- **FR-005 (Anti-Exploit Non-Cancellable Queues & Crop / Resource Safety)**:
  - Once a military training batch, Trapper batch, Hospital healing batch, Academy research, or Blacksmith/Armoury upgrade is initiated and its resources are deducted, the queue entry **MUST NOT** be cancellable by the player (preventing resource hiding during incoming raids).
  - Negative or zero requested unit quantities (`amt <= 0`) MUST be rejected. If a player requests a positive `amt` exceeding available stockpiles, expansion slots, or trapper capacity, `amt` MUST be clamped down to the maximum valid trainable quantity:
    $$\text{amt}_{\text{clamped}} = \min_{r \in \{\text{W,C,I,K}\}} \left\lfloor \frac{\text{Available}_r}{\text{UnitCost}_r} \right\rfloor$$
  - By default (`TerraviaSettings.Military.BlockTrainingOnNegativeCrop = false`), training or healing is permitted even when net hourly crop production is negative as long as current granary crop is $> 0$ and covers the batch's upfront crop cost (`AvailableCrop >= BatchCropCost`). When `BlockTrainingOnNegativeCrop = true` is explicitly enabled on a world, training or healing MUST be clamped or blocked if the village's net free crop rate after adding the batch would fall below `0` ($\text{NetFreeCrop} < \text{amt} \times \text{PopUpkeep}$).
  - **Sitter Permission Bitmask Validation**: Delegated Sitter sessions MUST hold `SitterPermissions.BuildAndDemolish` (`1 << 6`) to queue military training, Hospital healing, Trapper trap crafting, or Academy/Blacksmith/Armoury projects; `SitterPermissions.BuyAndSpendGold` (`1 << 3`) to trigger `2` Gold instant completion on Academy/Smithy queues; and `SitterPermissions.Reinforce` (`1 << 1`) to manually release or dissolve Trapper prisoners from the Rally Point.
- **FR-006 (Deterministic 4-Stage Training Speed Pipeline)**: Per-unit training duration `EachTime` (in seconds) for `Barracks` (`19`), `Great Barracks` (`29`), `Stable` (`20`), `Great Stable` (`30`), `Workshop` (`21`), `Great Workshop` (`49`), `Residence` (`25`), `Palace` (`26`), `Command Center` (`44`), and `Trapper` (`36`) MUST be computed in four sequential stages (each applied **at most once** per batch):
  1. **Stage 1 — Building Level & Roman Horse Drinking Trough (`gid 41`)**:
     - For all facilities except `Stable` (`20`) and `Great Stable` (`30`), let building multiplier be $\text{attri}(L) = \text{round}(100 \times 0.9^{L-1}, 2)$ (`100.00` at Level 1 down to `13.51` at Level 20):
       $$t_1 = \text{round}\left(\frac{\text{attri}(L)}{100} \times \frac{\text{BaseTime}}{\text{Speed}}\right)$$
     - For `Stable` (`20`) and `Great Stable` (`30`), if the village has a Roman `Horse Drinking Trough` (`gid 41`) at level $L_{\text{HDT}} \in [1..20]$ with $\text{attri}_{\text{HDT}}(L_{\text{HDT}}) = 1 + 0.01 \times L_{\text{HDT}}$ (`1.01` at Level 1 to `1.20` at Level 20):
       $$t_1 = \text{round}\left(\frac{\text{attri}(L) / \text{attri}_{\text{HDT}}(L_{\text{HDT}})}{100} \times \frac{\text{BaseTime}}{\text{Speed}}\right)$$
  2. **Stage 2 — Training Speed Artifact (`Trainer` / `Academic Advancement`, Kind `5`)**:
     - Apply the active village/account Trainer Artifact multiplier $M_{\text{art}} \in \{1.0, 0.75, 0.50\}$ **exactly once** (`0.50` for Small village-scope or Unique account-scope, `0.75` for Large account-scope; if both a village-scope and account-scope artifact apply, village-scope takes precedence per canonical artifact rules):
       $$t_2 = \max\left(1, \text{round}(t_1 \times M_{\text{art}})\right)$$
  3. **Stage 3 — Hero Recruitment Helmet Bonus (`ItemId 10..15`)**:
     - If the village owner's Hero is **alive (`dead = 0`)**, stationed in the training village (`hero.wref == villageId`), and has an equipped Helmet matching the unit's domain:
       - **Infantry (`Barracks gid 19` / `Great Barracks gid 29`)**: `Helmet of the Mercenary` (`id 13`, $P_{\text{hero}} = 10$), `Helmet of the Warrior` (`id 14`, $P_{\text{hero}} = 15$), `Helmet of the Ruler` (`id 15`, $P_{\text{hero}} = 20$).
       - **Cavalry (`Stable gid 20` / `Great Stable gid 30`)**: `Helmet of the Horseman` (`id 10`, $P_{\text{hero}} = 10$), `Helmet of the Cavalry` (`id 11`, $P_{\text{hero}} = 15$), `Helmet of the Heavy Cavalry` (`id 12`, $P_{\text{hero}} = 20$).
       - **All other facilities (`Workshop`, `Residence/Palace/Command Center`, `Trapper`, `Hospital`)**: $P_{\text{hero}} = 0$.
       $$t_3 = \max\left(1, \text{round}\left(t_2 \times \frac{100 - \min(90, P_{\text{hero}})}{100}\right)\right)$$
  4. **Stage 4 — T4 Alliance Recruitment Bonus (`Levels 1..5`)**:
     - For military training and trap-crafting facilities (`gid 19, 20, 21, 25, 26, 29, 30, 36, 44, 49`), if the village owner belongs to an alliance with effective Recruitment Bonus level $L_{\text{ally}} \in [1..5]$ ($M_{\text{ally}} = 1 + 0.02 \times L_{\text{ally}} \in \{1.02, 1.04, 1.06, 1.08, 1.10\}$):
       $$t_4 = \max\left(1, \text{round}\left(\frac{t_3}{M_{\text{ally}}}\right)\right)$$
       where a newly joined alliance member progressively unlocks Level $k \in [1..5]$ after $\lfloor (k - 1) \times 86400 / \text{Speed} \rfloor$ seconds of membership (`0h, 24h, 48h, 72h, 96h` at `1x` speed).
- **FR-007 (Roman Horse Drinking Trough Crop Upkeep Reduction)**: In addition to accelerating cavalry training in `Stable` (`gid 20`) and `Great Stable` (`gid 30`), a Roman `Horse Drinking Trough` (`gid 41`) MUST reduce the crop upkeep of Roman cavalry units stationed in or supported by that village by `1` crop/hour per unit at the canonical level thresholds: `Equites Legati` (`u4`) at **Level 10**, `Equites Imperatoris` (`u5`) at **Level 15**, and `Equites Caesaris` (`u6`) at **Level 20**.
- **FR-008 (Academy Unit Discovery System)**:
  - Each playable tribe's slot-1 infantry (`u1, u11, u21, u41, u51, u61, u71, u81`) and slot-10 Settler (`u10, u20, u30, u50, u60, u70, u80, u90`) MUST be unlocked by default in every village without research.
  - Unit slots `X2..X9` (`unit % 10 in 2..9`) MUST be researched per village in the `Academy` (`gid 22`) before they can be trained or upgraded in that village.
  - Initiating Academy research `t{unit}` MUST server-side validate that: (a) `unit` belongs to the village owner's tribe, (b) all cataloged building prerequisites (`meetRRequirement`) for `unit` are met in the village, (c) the unit is not already researched (`t{unit} == 0`) or queued, (d) **no other Academy research** is currently active in the village (strict limit of `1` active Academy project per village; Plus waiting loop does not apply to Academy), and (e) the village has sufficient resources and atomically deducts the cataloged `r{unit}` cost (`Wood = 6 * u_wood + 100`, etc., per `academyResearchByUnitId` in `001-core-game-catalog`).
  - Academy research duration MUST be calculated as:
    $$T_{\text{acad}} = \max\left(1, \text{round}\left(\text{BaseResearchTime}(u) \times \frac{\text{attri}_{\text{Academy}}(L)}{100 \times \text{Speed}}\right)\right)$$
- **FR-009 (Blacksmith & Armoury Combat Upgrade System)**:
  - `Blacksmith` (`gid 12`) upgrades unit offense (`b1..b8`) and `Armoury` (`gid 13`) upgrades unit defense (`a1..a8`) for combat unit slots `1..8` (`X1..X8`) from Level `0` to `20`. Cultural leaders (`X9`) and Settlers (`X0`) cannot be upgraded in the Blacksmith or Armoury.
  - Queueing an upgrade for slot $s \in [1..8]$ in `Blacksmith` (`b{s}`) or `Armoury` (`a{s}`) MUST require:
    1. The unit is unlocked in the village ($s = 1$ or `t{unit} == 1`).
    2. $\text{CurrentUpgradeLevel}(s) + \text{QueuedUpgrades}(s) < \text{BuildingLevel}$ (where $\text{BuildingLevel} \in [1..20]$ is the current level of the Blacksmith for `b{s}` or Armoury for `a{s}`).
    3. The respective building (`Blacksmith` or `Armoury`) has `0` active upgrades queued, OR `1` active upgrade queued when the player has an active Plus account.
    4. **Immediate Resource Deduction for Plus 2nd Upgrade Slot**: Both the 1st active upgrade and the 2nd Plus waiting-loop upgrade MUST deduct their respective `ab{unit}[CurrentTech + 1]` resources **immediately upon submission**. For the 2nd Plus slot, completion is scheduled at:
       $$\text{EndAt}_2 = \text{EndAt}_1 + T_{\text{smithy}}(u, \text{CurrentTech} + 1, B) + \left\lceil \frac{60}{\text{Speed}} \right\rceil$$
  - Upgrade cost and base time for level $L \in [1..20]$ MUST match `smithyUpgradesByUnitId` (`ab{unit}[L]` in `001-core-game-catalog`), with duration:
    $$T_{\text{smithy}}(u, L, B) = \max\left(1, \text{round}\left(\text{abTime}(u, L) \times \frac{\text{attri}_{\text{Smithy}}(B)}{100 \times \text{Speed}}\right)\right)$$
  - **Snapshot at Dispatch Rule**: Completed Blacksmith (`b1..b8`) and Armoury (`a1..a8`) upgrades immediately update the village's `Home` garrison, whereas outbound troop movements and stationed foreign reinforcements permanently lock in a snapshot of the home village's `b1..b8` / `a1..a8` levels captured at the moment of dispatch until those troops return home and re-deploy.
- **FR-010 (Combat Efficiency Upgrade Formula)**: During battle resolution, a unit's effective Offense (`atk`), Defense vs. Infantry (`di`), and Defense vs. Cavalry (`dc`) at Blacksmith/Armoury level $L \in [0..20]$ MUST be computed as:
  $$S_{\text{eff}}(L) = \begin{cases} S_0 & \text{if } L = 0 \\ \text{round}\left(S_0 + \left(S_0 + \frac{300 \times \text{pop}}{7}\right) \times \left(1.007^L - 1\right), 4\right) & \text{if } L \in [1..20] \end{cases}$$
  where $S_0$ is the base stat (`35` offense and `20` defense for Scouts during reconnaissance missions `type = 1`, or the unit's cataloged `atk`, `di`, `dc` during normal/raid combat), and $\text{pop}$ is the unit's crop upkeep. For Rams (`slot 7`) and Catapults (`slot 8`), structural demolition power MUST additionally scale by the upgrade factor:
  $$U_{\text{siege}}(L) = \frac{\text{round}\left(200 \times 1.0205^L\right)}{200}$$
- **FR-011 (Hospital & Big Hospital Construction, Bandage Sequencing & Casualty Interception)**:
  - `Hospital` (`gid 46`) is available to Romans (`1`), Teutons (`2`), Gauls (`3`), Huns (`6`), and Egyptians (`7`) with prerequisites `Main Building (gid 15) >= 10` and `Academy (gid 22) >= 15` (max `1` per village, max Level `20`). It intercepts **`40%`** (`rate = 0.40`) of eligible casualties and provides a wounded capacity of **`30 * Level`** (`30` at Level 1 up to `600` at Level 20).
  - `Big Hospital` (`gid 48`) is exclusive to Spartans (`8`) and Vikings (`9`) with prerequisites `Main Building (gid 15) >= 10` and `Academy (gid 22) >= 15` (max `1` per village, max Level `20`, `2x` Hospital construction cost). It intercepts **`50%`** (`rate = 0.50`) of eligible casualties and provides a wounded capacity of **`60 * Level`** (`60` at Level 1 up to `1,200` at Level 20).
  - **T4 Hero Bandage Precedence**: For attacking armies led by a surviving Hero equipped with `Bandages` (`id 207`, `33%`) or `Small Bandages` (`id 206`, `25%`), Bandage revival ($\text{bandaged}_s$) MUST be resolved first, and the attacker's home Hospital MUST evaluate only the remaining un-bandaged casualties $\text{deadAfterBandages}_s = \text{dead}_s - \text{bandaged}_s$ (preventing double-recovery of the same dead troop).
  - Whenever a village's own troops suffer battle casualties — either **defending their own home village** or **attacking another village** — if that troop's home village has a `Hospital` (`gid 46`) or `Big Hospital` (`gid 48`) at Level $L \ge 1$, the engine MUST iterate over combat unit slots $s \in [1..8]$ (`X1..X8`) in ascending slot order:
    1. Compute $\text{wounded}_s = \lfloor \text{deadAfterBandages}_s \times \text{rate} \rfloor$.
    2. Clamp $\text{admitted}_s = \min(\text{wounded}_s, \text{freeCapacity})$, where $\text{freeCapacity} = \max(0, \text{MaxCapacity}(L) - \sum_{k=1}^{8} \text{CurrentWounded}_k)$.
    3. Increment the home village's `StackType = Hospital` pool (`w1..w8`) by $\text{admitted}_s$ and decrement $\text{freeCapacity}$ by $\text{admitted}_s$.
  - Non-combat/special units (`X9` Cultural Leaders, `X0` Settlers, `u99` Traps, and Heroes) and foreign reinforcements defending another village MUST NOT be admitted to the target village's Hospital pool.
  - If a Hospital (`gid 46`) or Big Hospital (`gid 48`) is damaged or demolished to a lower level (including Level `0`), un-healed wounded troops already stored in `w1..w8` MUST be retained without loss even if $\sum_{k=1}^{8} \text{CurrentWounded}_k > \text{MaxCapacity}(L_{\text{new}})$ (blocking only new battle admissions via $\text{freeCapacity} = 0$, and requiring the building to be at Level $\ge 1$ to initiate new healing batches). However, if a village is conquered by an enemy Cultural Leader or destroyed to `0` population, its entire `Hospital` wounded pool and `Healing` queue MUST be cleared (`clearHospital`).
- **FR-012 (Hospital Wounded Pool State & Sequential FIFO Healing Queue)**:
  - Wounded troops stored in `StackType = Hospital` (`w1..w8`) or queued in `QueueCategory = Healing` MUST consume **`0` crop upkeep** and MUST NOT participate in offensive or defensive battles until healed.
  - When a player submits a healing request in the Hospital (`gid 46`) or Big Hospital (`gid 48`), the engine MUST validate that the unit slot is $s \in [1..8]$, clamp requested quantity `amt` to the available wounded count `w{s}` and maximum affordable quantity at **`50%` resource cost**:
    $$\text{Cost}_{\text{heal}}(u, \text{amt}) = \left\lceil \frac{\text{BaseCost}(u) \times \text{amt}}{2} \right\rceil \quad \forall \text{res} \in \{\text{Wood}, \text{Clay}, \text{Iron}, \text{Crop}\}$$
  - Deduct $\text{Cost}_{\text{heal}}$ from the village, decrement `w{s}` by `amt`, and enqueue the batch sequentially at the tail of `QueueCategory = Healing` (`CommenceAt = previousHealingEntry.EndAt` or `now` if empty, never running multiple healing batches in parallel) with per-unit healing duration:
    $$\text{EachTime}_{\text{heal}}(u, L) = \max\left(1, \text{round}\left(\text{BaseTime}(u) \times \frac{\text{attri}_{\text{Hospital}}(L)}{\text{Speed}}\right)\right)$$
    where $\text{attri}_{\text{Hospital}}(L) = \text{round}(0.9^{L-1}, 4)$ (`1.0000` at Level 1, `0.5314` / `~50%` at Level 7, down to `0.1351` at Level 20). As each unit graduates from the Healing Queue, it is credited to the village's `StackType = Home` garrison (`u{unitId}`) and resumes standard crop upkeep.
- **FR-013 (Gaul Trapper Construction, Multiple Trappers & Trap Crafting)**:
  - `Trapper` (`gid 36`) is exclusive to Gauls (`Tribe 3`), requires `Rally Point (gid 16) >= 1`, and can be constructed multiple times in the same village once an existing Trapper in that village reaches Level `20`.
  - Total village trap capacity MUST equal the sum of `bid36[level]['attri'] * TRAPPER_CAPACITY` across all Trapper buildings in the village, where a single Trapper at Levels `1..20` provides `[10, 22, 35, 49, 64, 80, 97, 115, 134, 154, 175, 196, 218, 241, 265, 290, 316, 343, 371, 400]` trap slots (`400` at Level 20).
  - Queueing Traps (`u99`, base cost `20` Wood, `30` Clay, `10` Iron, `20` Crop, `0` crop upkeep, base time `600`s) MUST enforce $\text{CurrentTraps} (\text{u99}) + \text{QueuedTraps} + \text{amt} \le \text{TotalTrapCapacity}$, using the village's **highest** Trapper building level (`max(getTypeLevel(36), 1)`) for the Stage 1 speed multiplier.
- **FR-014 (Pre-Combat Trap Capture, Hero Exclusion & Remainder Distribution)**:
  - **Archon Capital Auto-Trap**: If the target village is the Archon (`Tribe 5`) Capital (`owner == Archon && capital == 1`), `100%` of all incoming units (`t1..t11`, including scouts) are automatically trapped before combat.
  - **Gaul Trapper Interception**: When a **Normal Attack** (`AttackType = 3`, including attacks with Catapults/Rams) or **Raid** (`AttackType = 4`) arrives at a village with $\text{freeTraps} = \max(0, \text{u99} - \text{u99o}) > 0$ (whereas **Reconnaissance/Scouting** missions `AttackType = 1` bypass traps completely), let $\text{TotalTroops} = \sum_{i=1}^{11} \text{count}_i$:
    1. **Full Army Capture ($\text{freeTraps} \ge \text{TotalTroops}$)**: Every attacking unit across `t1..t11` (**including the Hero `t11`**) is captured ($\text{trapped}_i = \text{count}_i$).
    2. **Partial Army Capture ($\text{freeTraps} < \text{TotalTroops}$)**:
       - The **Hero (`t11`) is excluded from capture** ($\text{trapped}_{11} = 0$).
       - Each troop slot $i \in [1..10]$ initially receives $\text{trapped}_i = \lfloor \text{count}_i \times (\text{freeTraps} / \text{TotalTroops}) \rfloor$.
       - Remaining traps $\text{rem} = \text{freeTraps} - \sum_{i=1}^{10} \text{trapped}_i$ are distributed `+1` in sequential slot order $i = 1 \dots 10$ across slots where $\text{trapped}_i < \text{count}_i$ until $\text{rem} = 0$.
  - Captured units are subtracted from the attacking army prior to battle resolution, persisted or accumulated in a `Prisoners` record (`wref = defenderWid`, `from = attackerWid`, `t1..t11`), and added to the defender's occupied trap count (`u99o += totalTrapped`). While trapped, prisoners continue to consume crop upkeep from their **home village (`from`)**.
- **FR-015 (Prisoner Liberation, Manual Release, Dissolution & Automatic Trap Repair)**:
  - **Path A — Combat Liberation**: When a **Normal Attack** (`AttackType = 3`) from the prisoner owner or an allied/confederate/NAP player attacks the Gaul village and at least `1` attacking unit survives combat ($\text{totalSent} - \text{totalDead} - \text{totalTrapped} > 0$), all prisoner records in that village belonging to the attacker or their allied/confederate/NAP partners (`same owner`, `same alliance`, `Confederacy type 1`, or `NAP type 2`) are liberated:
    - **`25%` Prisoner Mortality**: For each slot $i \in [1..11]$, $\text{deadPrisoners}_i = \text{round}(\text{count}_i / 4)$ and $\text{surviving}_i = \text{count}_i - \text{deadPrisoners}_i$ (note: for a single Hero `t11 = 1`, $\text{round}(1 / 4) = 0$, so the Hero always survives).
    - **Same-Village vs. Allied/Other-Village Return**:
      - If a liberated prisoner record originated from the **exact same attacking village** (`prisoner.from == attacker.from`), its surviving units ($\text{surviving}_1 \dots \text{surviving}_{11}$) are **merged directly into the attacking army's returning troop counts (`modifyAttack2`)** and walk home together with the surviving attackers.
      - If a liberated prisoner record originated from **another village** (of the same player, ally, confederate, or NAP partner), a separate return movement (`MovementType = 4`, adjusted by the prisoner owner's Artifact Kind 2 speed bonus) is created from `wref` back to `prisoner.from`.
    - **Trap Destruction & Free Auto-Repair**: All $\text{totalFreed}$ occupied traps are destroyed (`u99 -= totalFreed`, `u99o -= totalFreed`), and $\text{repairCount} = \text{round}(\text{totalFreed} / 3)$ traps are automatically appended to the defender's `Trapper` queue at **`0` resource cost** (clamped so $\text{u99} + \text{queuedTraps} + \text{repairCount} \le \text{TotalTrapCapacity}$).
  - **Path B — Manual Release by Trap Owner (Gaul Defender)**: When the Gaul village owner releases a prisoner record from their Rally Point, `100%` of the prisoners survive and walk home (`MovementType = 4`), `u99` and `u99o` are decremented by $\text{totalFreed}$, and `100%` ($\text{totalFreed}$) of the traps are automatically queued in the Trapper for **`0` resource cost** (clamped to $\text{TotalTrapCapacity}$).
  - **Path C — Manual Dissolution by Troop Owner (Attacker)**: When the prisoner owner dissolves their trapped troops from their Rally Point, `100%` of the prisoners (including any trapped Hero, setting `hero.dead = 1, health = 0`) die immediately, `u99` and `u99o` in the Gaul village are decremented by $\text{totalFreed}$, and $\text{round}(\text{totalFreed} / 3)$ traps are automatically queued in the Gaul village's Trapper for **`0` resource cost** (clamped to $\text{TotalTrapCapacity}$).

### Key Entities *(include if feature involves data)*

- **TrainingQueueItem (`training`)**: Represents an active batch in a village recruitment, crafting, or healing facility (`Id`, `VillageId`, `UnitType`, `AmountRemaining`, `PopUpkeep`, `EachTime`, `CommenceAt`, `EndAt`, `QueueCategory` $\in \{\text{Barracks}, \text{GreatBarracks}, \text{Stable}, \text{GreatStable}, \text{Workshop}, \text{GreatWorkshop}, \text{ResidencePalace}, \text{Trapper}, \text{Healing}\}$).
- **VillageTechnologyState (`tdata` / `abdata`)**: Per-village persistent technology state tracking unlocked Academy unit discoveries (`t2..t9` booleans) and Blacksmith offense (`b1..b8` $\in [0..20]$) and Armoury defense (`a1..a8` $\in [0..20]$) upgrade levels.
- **ResearchQueueItem (`research`)**: Represents an active or Plus-looped technological project in a village (`Id`, `VillageId`, `TechCode` $\in \{\text{t2..t89}, \text{b1..b8}, \text{a1..a8}\}$, `Category` $\in \{\text{Academy}, \text{Blacksmith}, \text{Armoury}\}$, `CommenceAt`, `EndAt`, `IsPlusWaitingLoop`).
- **HospitalWoundedPool (`wounded` / `StackType = Hospital`)**: Per-village pool of wounded combat troops (`w1..w8`) awaiting healing, bounded by the village's `Hospital` (`gid 46`, `30 * Level`) or `Big Hospital` (`gid 48`, `60 * Level`) capacity, consuming `0` crop upkeep.
- **PrisonerStack (`prisoners`)**: Represents a group of attacking troops (`t1..t11`) captured by a Gaul village's `Trapper` (`wref = captorVillageId`, `from = ownerVillageId`), occupying $\sum_{i=1}^{11} t_i$ traps (`u99o`) in `wref` while consuming crop upkeep from `from`.

---

## Canonical Domain Reference Tables & Formulas *(normative)*

### 1. Facility Queue Categories & Unit Slot Mapping

| Facility | Building GID | Queue Category | Tribe Eligibility | Cost Multiplier | Eligible Unit Slots (per Tribe Offset $O = (\text{tribe}-1) \times 10$) |
| :--- | :---: | :--- | :--- | :---: | :--- |
| **Barracks** | `19` | `Barracks` | All (`1..9`) | `1x` | Infantry slots (`u1..u3, u11..u14, u21..u22, u31..u34, u41..u44, u51..u52, u61..u63, u71..u74, u81..u84`) |
| **Great Barracks** | `29` | `GreatBarracks` | Playable (`1..3, 6..9`), Non-Capital | `3x` | Same infantry slots as Barracks |
| **Stable** | `20` | `Stable` | All (`1..9`) | `1x` | Cavalry slots (`u4..u6, u15..u16, u23..u26, u35..u36, u45..u46, u53..u56, u64..u66, u75..u76, u85..u86`) |
| **Great Stable** | `30` | `GreatStable` | Playable (`1..3, 6..9`), Non-Capital | `3x` | Same cavalry slots as Stable |
| **Workshop** | `21` | `Workshop` | All (`1..9`) | `1x` | Siege slots (`O+7` Ram, `O+8` Catapult) |
| **Great Workshop** | `49` | `GreatWorkshop` | Playable (`1..3, 6..9`), Non-Capital | `3x` | Same siege slots as Workshop (`O+7`, `O+8`) |
| **Residence / Palace / Command Center** | `25 / 26 / 44` | `ResidencePalace` | `25`: All; `26`: Non-Hun; `44`: Huns (`6`) | `1x` | Expansion slots (`O+9` Cultural Leader, `O+10` Settler) |
| **Trapper** | `36` | `Trapper` | Gauls (`3`) only | `1x` (`0x` on auto-repair) | Trap (`u99`) |
| **Hospital / Big Hospital** | `46 / 48` | `Healing` | `46`: Tribes `1,2,3,6,7`; `48`: Tribes `8,9` | `0.5x` (`ceil(cost*amt/2)`) | Wounded combat slots (`O+1` through `O+8`) |

### 2. Building Speed Multiplier (`attri`) & Capacity Progression (Levels 1–20)

| Level | Standard Training / Research Speed `attri(L)` (%) (`gid 19, 20, 21, 22, 12, 13, 25, 26, 29, 30, 36, 44, 49`) | Hospital / Big Hospital Healing Multiplier (`attri_Hosp(L)`) (`gid 46, 48`) | Roman Horse Drinking Trough Divisor (`attri_HDT(L)`) (`gid 41`) | Gaul Trapper Capacity (`gid 36`) | Hospital Capacity (`gid 46`) | Big Hospital Capacity (`gid 48`) |
| :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **1** | `100.00%` | `1.0000` | `1.01` (`-1%`) | `10` | `30` | `60` |
| **2** | `90.00%` | `0.9000` | `1.02` (`-2%`) | `22` | `60` | `120` |
| **3** | `81.00%` | `0.8100` | `1.03` (`-3%`) | `35` | `90` | `180` |
| **4** | `72.90%` | `0.7290` | `1.04` (`-4%`) | `49` | `120` | `240` |
| **5** | `65.61%` | `0.6561` | `1.05` (`-5%`) | `64` | `150` | `300` |
| **6** | `59.05%` | `0.5905` | `1.06` (`-6%`) | `80` | `180` | `360` |
| **7** | `53.14%` | `0.5314` | `1.07` (`-7%`) | `97` | `210` | `420` |
| **8** | `47.83%` | `0.4783` | `1.08` (`-8%`) | `115` | `240` | `480` |
| **9** | `43.05%` | `0.4305` | `1.09` (`-9%`) | `134` | `270` | `540` |
| **10** | `38.74%` | `0.3874` | `1.10` (`-10%`) | `154` | `300` | `600` |
| **11** | `34.87%` | `0.3487` | `1.11` (`-11%`) | `175` | `330` | `660` |
| **12** | `31.38%` | `0.3138` | `1.12` (`-12%`) | `196` | `360` | `720` |
| **13** | `28.24%` | `0.2824` | `1.13` (`-13%`) | `218` | `390` | `780` |
| **14** | `25.42%` | `0.2542` | `1.14` (`-14%`) | `241` | `420` | `840` |
| **15** | `22.88%` | `0.2288` | `1.15` (`-15%`) | `265` | `450` | `900` |
| **16** | `20.59%` | `0.2059` | `1.16` (`-16%`) | `290` | `480` | `960` |
| **17** | `18.53%` | `0.1853` | `1.17` (`-17%`) | `316` | `510` | `1,020` |
| **18** | `16.68%` | `0.1668` | `1.18` (`-18%`) | `343` | `540` | `1,080` |
| **19** | `15.01%` | `0.1501` | `1.19` (`-19%`) | `371` | `570` | `1,140` |
| **20** | `13.51%` | `0.1351` | `1.20` (`-20%`) | `400` | `600` | `1,200` |

### 3. Academy Discovery (`r{unit}`) & Blacksmith/Armoury (`ab{unit}[L]`) Parametric Generation Formulas

All exact values are cataloged in `specs/001-core-game-catalog/catalog-data.json` (`academyResearchByUnitId` and `smithyUpgradesByUnitId`) and obey the following canonical relationships with base unit cost $(W_u, C_u, I_u, K_u)$ and base unit training time $T_u$:

- **Academy Research Cost & Time (`r{unit}` for Combat Slots `X2..X8`)**:
  $$\text{Wood}_r = 6 W_u + 100, \quad \text{Clay}_r = 4 C_u + 100, \quad \text{Iron}_r = 8 I_u + 200, \quad \text{Crop}_r = 6 K_u + 160, \quad \text{Time}_r = 6 T_u + 1800$$
- **Blacksmith / Armoury Level 1 Cost & Time (`ab{unit}[1]` for Combat Slots `X1..X8`)**:
  $$\text{Wood}_{ab,1} = \left\lfloor \frac{7 \text{Wood}_r + 750}{2} \right\rfloor \text{ (or cataloged } ab_1\text{)}, \quad \text{Res}_{ab,L} = \text{round\_to\_5}\left(\text{Res}_{ab,1} \times L^{0.8}\right), \quad \text{Time}_{ab,L} = \text{round}\left(\text{Time}_{ab,1} \times L^{0.8}\right)$$

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001 (100% Training Time & Multiplier Parity)**: Automated verification tests across all 10 facility types (`gid 19, 20, 21, 25, 26, 29, 30, 36, 44, 49`), building levels `1..20`, Roman Horse Drinking Trough levels `0..20`, Trainer Artifacts (`1.0, 0.75, 0.50`), Hero Helmets (`0%, 10%, 15%, 20%`), and Alliance Recruitment Bonus levels `0..5` produce `100%` bit-for-bit integer-second parity with the 4-stage pipeline formula.
- **SC-002 (Real-Time Sequential Unit Graduation & Upkeep Integrity)**: In simulation tests running `1,000` concurrent training, healing, and trap-crafting batches across multiple villages, `100%` of units graduate sequentially at exact `CommenceAt + k * EachTime` boundaries without batch-end-only delays or crop upkeep drift.
- **SC-003 (Zero Resource-Hiding Exploits & Expansion Slot Compliance)**: `100%` of attempts to cancel active training/research/healing queues, queue simultaneous Chief + Settler batches in one request, or exceed Residence/Palace/Command Center expansion slots across garrison, queues, movements, reinforcements, traps, and daughter villages are deterministically rejected.
- **SC-004 (Combat Upgrade & Siege Scaling Accuracy)**: Unit combat stats (`atk`, `di`, `dc`) and Ram/Catapult structural upgrade multipliers at Blacksmith/Armoury levels `0..20` match the canonical formulas to `4` decimal places (`0.0001` tolerance) across all 7 playable tribes and Archon units.
- **SC-005 (Hospital & Trapper Casualty/Prisoner Accounting)**: Across defensive and offensive battle simulations, Hospital (`40%`, `30*L`) and Big Hospital (`50%`, `60*L`) casualty interception, `50%`-cost healing queues, Gaul Trapper proportional prisoner capture, `25%` combat-liberation prisoner mortality, and free automatic trap repair (`round(totalFreed / 3)` on combat liberation/dissolution, `100%` on defender manual release) maintain `100%` troop conservation with zero orphaned or duplicated units.

## Assumptions

- **Canonical Data Reference**: Base unit costs, training times, crop upkeep (`pop`), Academy prerequisite trees, `academyResearchByUnitId` (`r2..r89`), and `smithyUpgradesByUnitId` (`ab1..ab88`) are sourced directly from `001-core-game-catalog` (`specs/001-core-game-catalog/catalog-data.json`).
- **Healing Duration & Cost Alignment**: In accordance with both the canonical `TravianZ` Hospital implementation (`Technology::getHealingCost`, `Technology::getHealingTime`) and the user requirement, Hospital healing always deducts `50%` of base unit resource costs (`ceil(cost * amt / 2)`) and scales healing time by the Hospital's level attribute (`round(unit_time * attri / Speed)`, which reaches `~50%` of base recruitment time at Level 7 and accelerates further down to `13.51%` at Level 20).
- **Trapper Auto-Repair Ratio**: Combat liberation and troop-owner prisoner dissolution deterministically queue `round(totalFreed / 3)` free trap repairs (matching `AutomationBattleResolution::handlePrisoners` and `Units::deletePrisoners`), while manual release by the Gaul trap owner queues `100%` (`totalFreed`) free trap repairs.
