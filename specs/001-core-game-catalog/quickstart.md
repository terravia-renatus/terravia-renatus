# Quickstart & Validation Guide: Core Game Catalog

**Feature**: `001-core-game-catalog`
**Date**: 2026-11-06
**Spec**: [spec.md](./spec.md) | **Data Model**: [data-model.md](./data-model.md) | **Contracts**: [domain-services.md](./contracts/domain-services.md), [catalog-api.md](./contracts/catalog-api.md)

---

## 1. Prerequisites

- **.NET SDK**: `.NET 10.0` (`dotnet --version` >= `10.0.100`)
- **Node.js & Package Manager**: Node `>= 22` / `pnpm` (for `client/packages/game-contracts` schema validation)
- **Canonical Catalog Dataset**: Embedded `specs/001-core-game-catalog/catalog-data.json` for automated golden-vector verification.

---

## 2. Build & Automated Golden-Vector Test Verification (TDD Gate)

All domain catalog definitions, building prerequisite evaluations, and construction/demolition queue state transitions are verified via deterministic xUnit + `Akka.TestKit` test suites without requiring a live external database.

### 2.1 Run Pure Domain Catalog & Formula Verification Tests
```powershell
dotnet test server/tests/Terravia.Domain.Tests/Terravia.Domain.Tests.csproj --filter "FullyQualifiedName~Catalog"
```
**Expected Outcomes**:
- **Tribe & Unit Parity (`SC-001`)**: Confirms all 9 tribes (`1..9`), 90 units (`u1..u90`), Trap (`u99`), and 35 Hero templates (`h1..h86`) match `catalog-data.json` (including Barracks assignment for `u52, u74, u82` and Horse Drinking Trough upkeep reductions for `u4, u5, u6`).
- **Building Level & Prerequisite Parity (`SC-001`, `SC-002`)**: Confirms all 50 buildings (`gid 1..50`) across all levels (`1..20` and `1..100` for WW `gid 40`) match `catalog-data.json` and `IBuildingRuleCatalog.MeetsPrerequisites`:
  - Separate Blacksmith (`gid 12`) and Armoury (`gid 13`) upgrade trees.
  - Hun Command Center (`gid 44`) blocking Residence (`25`) / Palace (`26`) and unlocking Stonemason's Lodge (`34`) at Level 3 in the capital.
  - Spartan/Viking Big Hospital (`gid 48`) replacing standard Hospital (`gid 46`).
  - Multi-instance rebuild thresholds (`Warehouse 10`, `Granary 11`, `Trapper 36`, `Great Storage 38/39` at Level 20; `Cranny 23` at Level 10).
  - Account-wide Palace (`gid 26`) uniqueness strictly checking inner slots `19..40`.
  - World Wonder (`gid 40`) Building Plan checks inspecting the highest queued level across the entire build queue (`Level >= 50` requiring 2nd alliance plan).

### 2.2 Run Actor Queue, Master Builder & Demolition Virtual-Time Tests
```powershell
dotnet test server/tests/Terravia.Actors.Tests/Terravia.Actors.Tests.csproj --filter "FullyQualifiedName~ConstructionQueue"
```
**Expected Outcomes**:
- **Roman Dual-Build & Plus Loop (`FR-008`, `FR-009`)**: Verifies simultaneous Resource Field (`Slot 1..18`) + Inner Building (`Slot 19..40`) execution for Romans, single active build for non-Romans, and `ceil(60 / Speed)` waiting-loop chaining.
- **Master Builder Lifecycle (`FR-010`)**: Verifies zero resource/Gold deduction on reservation (`master = 1`), automatic promotion when a slot frees and resources/Gold are available, and zero refund on cancelling an unstarted reservation.
- **Gold Finish All & Demolition (`FR-011`, `FR-012`)**: Verifies WW village block on "Finish All", exclusion of `gid 25, 26, 40`, timed 1-level demolition (`floor(buildTime / 2)`), 10-Gold instant complete demolition to Level 0 with cumulative `pop` and storage/CP recalculation, and Alliance Founder Embassy (`gid 18`) demolition protection.

---

## 3. API & Contract Verification (`GET /api/game/catalog`)

Start the Web API host and verify the hash-cached catalog endpoint:
```powershell
dotnet run --project server/src/Terravia.WebAPI/Terravia.WebAPI.csproj
```
Query the catalog endpoint and verify `ETag` caching:
```powershell
curl -i http://localhost:5000/api/game/catalog
```
**Expected Outcome**:
- Returns `200 OK` with `ETag: "sha256:..."` and the complete JSON payload containing `9` tribes, `91` unit entries (`u1..u90, u99`), `35` hero templates, and `50` building definitions.
- Repeating the request with `If-None-Match: "<ETag>"` returns `304 Not Modified` in `< 1ms`.
