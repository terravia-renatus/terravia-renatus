# Phase 0 Research: Military Recruitment, Technological Research, Specialty Crafting, and Hospital Casualty Recovery

**Feature Branch**: `003-military-research-hospital`  
**Date**: 2026-11-07  
**Specification**: [spec.md](./spec.md)

---

## 1. Concurrent Facility Training Queues, Sequential Single-Unit Graduation & Batch Merging (`FR-001`..`FR-003`)

### Decision
Implement a pure domain manager `MilitaryTrainingManager` paired with a stateless calculator `TrainingTimeCalculator` in `Terravia.Domain.Villages`. `VillageState` maintains `List<TrainingQueueItem> TrainingQueues` partitioned logically across 9 independent `TrainingFacilityCategory` lanes (`Barracks = 1`, `GreatBarracks = 2`, `Stable = 3`, `GreatStable = 4`, `Workshop = 5`, `GreatWorkshop = 6`, `ResidencePalace = 7`, `Trapper = 8`, `Healing = 9`).
- Each lane executes strictly in FIFO order, while different lanes execute concurrently in parallel.
- Submitting a batch for the same `UnitTypeId` with identical `EachTimeSeconds` as the trailing item in that `TrainingFacilityCategory` lane merges into the trailing item (`AmountRemaining += amount`, `FinishesAtMs += amount * EachTimeSeconds * 1000L`).
- During lazy evaluation or scheduled timer ticks at `nowMs >= CommenceAtMs`, graduated units are evaluated in $O(1)$ per active lane head:
  $$\text{elapsedSeconds} = \left\lfloor \frac{\text{nowMs} - \text{CommenceAtMs}}{1000} \right\rfloor, \quad \text{graduated} = \min\left(\text{AmountRemaining}, \left\lfloor \frac{\text{elapsedSeconds}}{\text{EachTimeSeconds}} \right\rfloor\right)$$
- When $\text{graduated} \ge 1$, units are immediately credited to the village's `Home` stack (`UnitCounts[UnitTypeId] += graduated`, or `TotalTrapsCount += graduated` for `u99`), `CommenceAtMs += graduated * EachTimeSeconds * 1000L`, `AmountRemaining -= graduated`, and `RecalculateEconomyRates()` updates village net crop upkeep in real time.

### Rationale
- Matches canonical `TravianZ` `AutomationTraining::trainingComplete()` unit-by-unit graduation semantics while replacing 1-second database polling sweeps with exact `ITimerScheduler` next-unit wakeup timestamps (`NextGraduationAtMs = CommenceAtMs + EachTimeSeconds * 1000L`) per **Constitution Principle III**.
- Fixing the legacy `TravianZ` `Technology::healUnit()` bug where multiple Hospital healing batches started at `time()` in parallel: `TrainingFacilityCategory.Healing` uses the exact same single-lane sequential FIFO tail-chaining (`CommenceAtMs = previousEntry.FinishesAtMs`) as all other recruitment facilities.

### Alternatives Considered
- **Batch-end-only graduation**: Rejected because players rely on individual units graduating incrementally to defend against incoming attacks and because crop upkeep must scale unit-by-unit in real time (`SC-002`).
- **Separate actor timers per facility lane**: Consolidated into a single `MilitaryTrainingTimerKey` on `VillageActor` scheduled at $\min_{\text{active lane heads}}(\text{CommenceAtMs} + \text{EachTimeSeconds} \times 1000)$, minimizing Akka timer overhead across 25,000+ villages.

---

## 2. Deterministic 4-Stage Training Speed Pipeline & Single-Pass Artifact Fix (`FR-006`)

### Decision
Implement `TrainingTimeCalculator.CalculateEachTimeSeconds(in TrainingSpeedContext context)` as a pure, zero-allocation 4-stage integer-second rounding pipeline with a strict minimum floor of `1` second at every stage:
1. **Stage 1 (Building Level & Roman Horse Drinking Trough `gid 41`)**:
   - Building multiplier $\text{attri}(L) = \text{round}(100 \times 0.9^{L-1}, 2)$ (looked up directly from `IGameCatalog.GetBuildingLevelStats(facilityGid, level).PrimaryAttribute`).
   - For `Stable` (`gid 20`) and `Great Stable` (`gid 30`) when Roman `Horse Drinking Trough` (`gid 41`) is at level $L_{\text{HDT}} \in [1..20]$, divide $\text{attri}(L)$ by $\text{attri}_{\text{HDT}}(L_{\text{HDT}}) = 1 + 0.01 \times L_{\text{HDT}}$ (`1.01..1.20`):
     $$t_1 = \max\left(1, \text{round}\left(\frac{\text{effectiveAttri}}{100.0} \times \frac{\text{BaseTrainingSeconds}}{\text{ServerSpeed}}\right)\right)$$
2. **Stage 2 (Trainer Artifact `Kind 5` — Applied Exactly Once)**:
   - Multiply by $M_{\text{art}} \in \{1.0, 0.75, 0.50\}$ (`0.50` Small/Unique, `0.75` Large):
     $$t_2 = \max\left(1, \text{round}(t_1 \times M_{\text{art}})\right)$$
3. **Stage 3 (Stationed Living Hero Recruitment Helmet `ItemId 10..15`)**:
   - Applied only if `HasStationedLivingHero == true` and facility is `Barracks`/`GreatBarracks` (Infantry Helmet bonus $P_{\text{hero}} \in \{10, 15, 20\}$) or `Stable`/`GreatStable` (Cavalry Helmet bonus $P_{\text{hero}} \in \{10, 15, 20\}$); $P_{\text{hero}} = 0$ for `Workshop`, `GreatWorkshop`, `ResidencePalace`, `Trapper`, and `Healing`:
     $$t_3 = \max\left(1, \text{round}\left(t_2 \times \frac{100 - \min(90, P_{\text{hero}})}{100.0}\right)\right)$$
4. **Stage 4 (T4 Alliance Recruitment Bonus `Levels 1..5`)**:
   - Applied to military recruitment and trap crafting (`gid 19, 20, 21, 25, 26, 29, 30, 36, 44, 49`), where effective alliance bonus level $L_{\text{eff}} \in [0..5]$ is capped by member tenure $\lfloor \text{MembershipElapsedSeconds} \times \text{ServerSpeed} / 86400 \rfloor + 1$:
     $$t_4 = \max\left(1, \text{round}\left(\frac{t_3}{1.0 + 0.02 \times L_{\text{eff}}}\right)\right)$$
   - All `round(...)` calls use `Math.Round(..., MidpointRounding.AwayFromZero)` to match PHP `round()` half-away-from-zero semantics.

### Rationale
- Eliminates the legacy `TravianZ` double-application bug where `Technology::getTrainingTime()` and `DatabaseTroopQueries::trainUnit()` both multiplied by the Trainer Artifact (`Kind 5`).
- Enforces `MidpointRounding.AwayFromZero` at each stage for 100% golden-vector parity (`SC-001`).

### Alternatives Considered
- **Single combined floating-point multiplication rounded only at the end**: Rejected because intermediate `round()` calls in stages 1–4 produce distinct integer values on 1x/3x/5x speed worlds that must match canonical Travian T4 timing vectors.

---

## 3. Academy Discovery, Smithy Upgrades & Dispatch Snapshot Architecture (`FR-008`..`FR-010`)

### Decision
Implement `TechnologyResearchManager` and `CombatUpgradeCalculator` in `Terravia.Domain.Villages`:
- `VillageState` holds `bool[] UnlockedUnitSlots = new bool[11]` (indices `1..10`, initialized with `UnlockedUnitSlots[1] = true` and `UnlockedUnitSlots[10] = true`), `int[] OffenseUpgradeLevels = new int[9]` (`b1..b8`, indices `1..8`), `int[] DefenseUpgradeLevels = new int[9]` (`a1..a8`, indices `1..8`), and `List<ResearchQueueItem> ResearchQueue`.
- **Queue Concurrency Rules**:
  - `Academy` (`gid 22`, `ResearchCategory.Academy`): Max `1` active research per village; Plus waiting loop does **not** apply to Academy.
  - `Blacksmith` (`gid 12`, `ResearchCategory.Blacksmith`) and `Armoury` (`gid 13`, `ResearchCategory.Armoury`): Run concurrently in separate lanes. Each building allows `1` active upgrade, or `1` active + `1` Plus waiting-loop upgrade when `HasPlusSubscription == true`.
  - Both the 1st and 2nd Plus Smithy slots deduct their cataloged `SmithyUpgrades[TargetLevel - 1].Cost` immediately upon submission, and the 2nd slot schedules completion at $\text{FinishesAtMs}_2 = \text{FinishesAtMs}_1 + (T_{\text{smithy}} + \lceil 60 / \text{ServerSpeed} \rceil) \times 1000$.
- **Snapshot at Dispatch Only (Clarification Q3)**:
  - `StarvationCandidateStack` / `VillageTroopStack` carries `int[] OffenseUpgradeLevels` (`b1..b8`) and `int[] DefenseUpgradeLevels` (`a1..a8`).
  - When a Smithy upgrade completes in a village, `VillageState.OffenseUpgradeLevels[slot]` (or `DefenseUpgradeLevels[slot]`) and the village's `Home` stack are updated immediately, while outbound movements and stationed foreign reinforcements retain the immutable upgrade snapshot captured when they were dispatched from their home village.
- **2 Gold Instant Research Completion (`FinishAllResearchWithGoldCommand`)**:
  - Completes all active `Academy`, `Blacksmith`, and `Armoury` items in `VillageState.ResearchQueue` immediately for `2` Gold (validated against `SitterPermissions.BuyAndSpendGold`), leaving military training, Trapper, and Hospital healing queues untouched.

### Rationale
- Snapshot-at-dispatch preserves **Constitution Principle I** (`Actor Boundaries as DDD Aggregate Roots`) by eliminating cross-actor state queries or fan-out broadcasts to foreign villages and in-flight movements when a Smithy upgrade completes.
- `CombatUpgradeCalculator` implements the exact pop-weighted exponential formula $\text{round}(S_0 + (S_0 + 300 \times \text{pop} / 7) \times (1.007^L - 1), 4)$ and siege factor $\text{round}(200 \times 1.0205^L) / 200$ as a pure `double` function (`SC-004`).

### Alternatives Considered
- **Cross-actor `VillageTechUpgradedEto` fan-out to all reinforced villages**: Rejected during `/speckit-clarify` (Question 3 → Option B) in favor of Snapshot-at-Dispatch for deterministic actor isolation and zero message amplification.

---

## 4. Hospital (`gid 46`) & Big Hospital (`gid 48`) Casualty Interception, Bandage Sequencing & Soft-Cap Retention (`FR-011`..`FR-012`)

### Decision
Implement `HospitalHealingManager` in `Terravia.Domain.Villages`:
- `VillageState` stores `int[] WoundedUnitsBySlot = new int[9]` (`w1..w8` for combat unit slots `1..8`), which consume `0` crop upkeep and are excluded from combat and starvation.
- **Capacity & Interception Rate**:
  - `Hospital` (`gid 46`, Tribes `1, 2, 3, 6, 7`): Save rate `0.40` (`40%`), max capacity $30 \times L_{\text{Hospital}}$.
  - `Big Hospital` (`gid 48`, Spartans `8` & Vikings `9`): Save rate `0.50` (`50%`), max capacity $60 \times L_{\text{BigHospital}}$.
- **T4 Hero Bandage Precedence**:
  - For attacking armies with a surviving Hero carrying `Small Bandages` (`ItemId 206`, `25%`) or `Bandages` (`ItemId 207`, `33%`), battlefield bandage revival $\text{bandaged}_s = \min(\text{AvailableBandages}, \lfloor \text{dead}_s \times \text{bandageRate} \rfloor)$ is deducted first, and the home village's Hospital evaluates only $\text{deadAfterBandages}_s = \text{dead}_s - \text{bandaged}_s$.
- **Sequential Slot Priority (`1..8`) & Soft-Cap Retention on Building Damage (Clarification Q2)**:
  - Casualties are admitted in ascending slot order $s = 1 \dots 8$: $\text{admitted}_s = \min(\lfloor \text{deadAfterBandages}_s \times \text{rate} \rfloor, \text{freeCapacity})$.
  - If catapults or demolition reduce the Hospital level (even to Level `0`), existing `WoundedUnitsBySlot[1..8]` are retained without loss (`freeCapacity = max(0, MaxCapacity(NewLevel) - sum(w1..w8))`), blocking only new admissions and requiring Level $\ge 1$ to queue new healing batches.
  - On village conquest or razing to `0` population, `WoundedUnitsBySlot` and `TrainingFacilityCategory.Healing` queue items are purged immediately (`ClearHospitalOnConquestOrRaze`).

### Rationale
- Ensures 100% troop conservation (`SC-005`), prevents double-saving casualties via Hero Bandages + Hospital, and matches canonical `TravianZ` `AutomationBattleResolution::updateWounded()` and `DatabaseVillageQueries::clearHospital()`.

### Alternatives Considered
- **Killing excess wounded troops when Hospital is damaged by catapults**: Rejected during `/speckit-clarify` (Question 2 → Option A).

---

## 5. Gaul Trapper (`gid 36`), Pre-Combat Interception, Prisoner Lifecycle & Free Auto-Repair (`FR-013`..`FR-015`)

### Decision
Implement `TrapperManager` in `Terravia.Domain.Villages`:
- **Multiple Trappers & Capacity**:
  - Gauls (`Tribe 3`) can construct additional Trappers (`gid 36`) once at least one existing Trapper in the village is at Level `20`.
  - Total trap capacity is the sum of `GetBuildingLevelStats(36, level).PrimaryAttribute` (`10..400`) across all Trapper slots in the village. Trap crafting (`u99`) uses the highest Trapper level (`maxLevel`) for Stage 1 speed calculation and enforces $\text{TotalTrapsCount} + \text{QueuedTrapsCount} + \text{amt} \le \text{TotalTrapCapacity}$.
- **Pre-Combat Interception (`EvaluatePreCombatTrapInterception`)**:
  - **Reconnaissance (`AttackType = 1`)**: Bypasses Gaul Trappers completely (except against the **Archon Capital** `IsArchonVillage && IsCapital`, which auto-traps `100%` of all incoming units `t1..t11` including scouts).
  - **Normal Attack (`AttackType = 3`) & Raid (`AttackType = 4`)**: Let $F = \max(0, \text{TotalTrapsCount} - \text{OccupiedTrapsCount})$ and $T = \sum_{i=1}^{11} \text{count}_i$:
    - If $F \ge T$: All units `t1..t11` (including the Hero `t11`) are captured.
    - If $F < T$: Hero (`t11`) escapes (`trapped[11] = 0`); slots $i \in [1..10]$ receive $\lfloor \text{count}_i \times F / T \rfloor$, and remainder traps $F - \sum_{i=1}^{10} \text{trapped}_i$ are distributed `+1` sequentially from slot `1` to `10` where $\text{trapped}_i < \text{count}_i$.
- **Three Prisoner Release Paths & Free Auto-Repair (Clarification Q5)**:
  - **Path A (Combat Liberation by Normal Attack with $\ge 1$ Survivor)**: Liberates prisoner stacks belonging to the attacker or allied/confederate/NAP partners. `25%` of each liberated slot dies ($\text{dead}_i = \text{round}(\text{count}_i / 4)$, $\text{surviving}_i = \text{count}_i - \text{dead}_i$). Surviving prisoners from the **same attacking village** (`OwnerVillageId == AttackerVillageId`) merge directly into the returning attacking army; prisoners from other villages return via separate `MovementType = 4` movements. Destroys $\text{totalFreed}$ traps (`TotalTrapsCount -= totalFreed`, `OccupiedTrapsCount -= totalFreed`) and queues $\text{round}(\text{totalFreed} / 3)$ free (`0`-cost) trap repairs clamped to `TotalTrapCapacity`.
  - **Path B (Manual Release by Gaul Trap Owner)**: `100%` of prisoners survive and return home (`MovementType = 4`); destroys $\text{totalFreed}$ traps and queues `100%` ($\text{totalFreed}$) free trap repairs clamped to `TotalTrapCapacity`.
  - **Path C (Manual Dissolution by Trapped Troop Owner)**: `100%` of prisoners (including any trapped Hero) die immediately; destroys $\text{totalFreed}$ traps and queues $\text{round}(\text{totalFreed} / 3)$ free trap repairs in the Gaul village clamped to `TotalTrapCapacity`.

### Rationale
- Fixes the legacy `TravianZ` remainder-distribution bug where remainder traps could exceed `count[i]` on depleted slots, while preserving canonical Hero escape, same-village army merge, and `round(totalFreed / 3)` / `100%` free auto-repair rules.

### Alternatives Considered
- **`25%` trap repair ratio on combat liberation**: Resolved during `/speckit-clarify` (Question 5 → Option A) in favor of the canonical `round(totalFreed / 3)` trap repair ratio paired with `round(count / 4)` (`25%`) prisoner mortality.
