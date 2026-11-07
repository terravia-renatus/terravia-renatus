# Quickstart Validation Guide: Military Recruitment, Technological Research, Specialty Crafting, and Hospital Casualty Recovery

**Feature Branch**: `003-military-research-hospital`  
**Date**: 2026-11-07  
**Specification**: [spec.md](./spec.md) | **Contracts**: [domain-services.md](./contracts/domain-services.md)

---

## Prerequisites

- **.NET 10.0 SDK** (`dotnet --version` $\ge 10.0.100$)
- **Node.js 22+ & pnpm** (for `client/packages/game-contracts`)
- Working directory: `d:\Projects\terravia-renatus`

---

## 1. Build & Verify Zero-Dependency Domain Layer

```powershell
cd d:\Projects\terravia-renatus\server
dotnet build Terravia.sln --configuration Release
```

**Expected Outcome**:
- `Terravia.Domain`, `Terravia.Actors`, `Terravia.Infrastructure`, `Terravia.BotAI`, and `Terravia.WebAPI` compile with `0` errors and `0` warnings.

---

## 2. Run Golden-Vector Domain & Property-Based Tests (`SC-001`..`SC-005`)

```powershell
cd d:\Projects\terravia-renatus\server
dotnet test tests/Terravia.Domain.Tests/Terravia.Domain.Tests.csproj --filter "FullyQualifiedName~Military|FullyQualifiedName~Training|FullyQualifiedName~Research|FullyQualifiedName~Hospital|FullyQualifiedName~Trapper"
```

### Verification Scenarios Covered:
1. **4-Stage Training Speed Pipeline (`SC-001`)**:
   - Validates `TrainingTimeCalculator.CalculateEachTimeSeconds` across all 10 facilities (`gid 19, 20, 21, 25, 26, 29, 30, 36, 44, 49`), building levels `1..20`, Roman `Horse Drinking Trough` (`gid 41`, `-1%` per level), single-pass Trainer Artifacts (`1.0, 0.75, 0.50`), stationed living Hero Helmets (`ItemId 10..15`, `-10%`, `-15%`, `-20%`), and progressive Alliance Recruitment Bonus (`Levels 1..5`).
2. **Sequential Unit Graduation, Great Military `3x` Cost & Expansion Slot Guards (`SC-002`, `SC-003`)**:
   - Verifies `MilitaryTrainingManager` unit-by-unit graduation (`floor(elapsed / eachtime)`), real-time crop upkeep updates, trailing batch merging, `3x` cost in `GreatBarracks`/`GreatStable`/`GreatWorkshop`, simultaneous Chief+Settler submission rejection, and Sitter permission bitmask checks.
3. **Academy Discovery, Smithy Upgrades & Combat Efficiency (`SC-004`)**:
   - Verifies `TechnologyResearchManager` server-side prerequisite guards, Plus 2nd Smithy slot (`+ ceil(60 / Speed)`s with upfront resource deduction), `2` Gold instant research completion, and `CombatUpgradeCalculator` (`1.007^L` pop-weighted stat formula and `round(200 * 1.0205^L) / 200` siege multiplier).
4. **Hospital & Big Hospital Interception, Bandage Precedence & Sequential Healing (`SC-005`)**:
   - Verifies `HospitalHealingManager` T4 Hero Bandage (`25%` / `33%`) pre-deduction, `40%` (`30*L`) vs. `50%` (`60*L`) casualty admission in slot `1..8` order, soft-cap retention on building damage, conquest/razing cleanup, and `50%`-cost sequential single-lane healing.
5. **Gaul Trapper Interception, Prisoner Liberation & Free Auto-Repair (`SC-005`)**:
   - Verifies `TrapperManager` multi-Trapper capacity (`400` per Level 20), Reconnaissance bypass, Archon Capital `100%` auto-trap, Hero (`t11`) escape on partial capture, sequential `1..10` remainder clamping, `25%` combat-liberation mortality, same-village army merge, and `round(totalFreed / 3)` / `100%` free trap repair.

---

## 3. Run Virtual-Time `Akka.TestKit` Actor Concurrency Suite

```powershell
cd d:\Projects\terravia-renatus\server
dotnet test tests/Terravia.Actors.Tests/Terravia.Actors.Tests.csproj --filter "FullyQualifiedName~VillageMilitaryActorTests"
```

**Expected Outcome**:
- Verifies `VillageActor` timer scheduling across concurrent `Barracks`, `Stable`, `Workshop`, `GreatBarracks`, `ResidencePalace`, `Trapper`, `Healing`, `Academy`, `Blacksmith`, and `Armoury` queues using virtual time without background polling loops.

---

## 4. Validate TypeScript Shared Contracts

```powershell
cd d:\Projects\terravia-renatus\client\packages\game-contracts
pnpm test
```
