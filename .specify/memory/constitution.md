<!--
Sync Impact Report:
- Version change: 1.0.0 -> 1.1.0 (Minor Amendment: Added Naming & Structural Conventions)
- Modified principles: None
- Added sections:
  - Naming & Structural Conventions (under Architectural & Technology Boundaries)
- Removed sections: None
- Follow-up TODOs: None
-->

# Terravia Renatus Constitution

## Core Principles

### I. Actor Boundaries as DDD Aggregate Roots
Every in-memory domain actor (`VillageActor`, `PlayerActor`, `AllianceActor`, `MarketActor`) MUST serve as the sole Aggregate Root for its bounded domain context. All state mutations MUST be serialized through single-threaded actor message processing in Akka.NET. Direct cross-actor state modification, shared mutable memory, and database row-locking are strictly prohibited.

### II. Zero-Allocation Hot Paths & Pure Domain Services
Critical high-frequency execution paths (battle simulation, resource calculation, building validation) MUST minimize heap allocations by utilizing contiguous memory layouts (`int[]`, `InlineArray`, `Span<T>`) rather than complex object graphs. Mathematical combat mechanics, prerequisite evaluations, and production formulas MUST be implemented as stateless, deterministic, pure C# domain services injected via interfaces (`IBattleSimulator`, `IBuildingRuleCatalog`).

### III. Lazy Evaluation Over Polling Loops
Game systems MUST NOT execute periodic database polling sweeps or 1-second background loops over world entities to advance continuous state. Resource accumulation MUST be evaluated lazily in $O(1)$ time based on timestamp differentials (`DateTimeOffset` delta calculations) upon command reception or timer triggers. Discrete future events (troop arrivals, construction completions, training batches) MUST be scheduled via millisecond-precision priority queues (`MovementSchedulerActor`) and actor timers.

### IV. Hybrid Data Access & Write-Behind Micro-Batching
Persistence MUST adhere to a strict workload separation:
- **EF Core** MUST be used exclusively for schema migrations, database seeding, static balance catalogs, account authentication, and low-frequency administrative CRUD.
- **Dapper with Table-Valued Parameters (TVPs)** MUST be used for high-frequency actor hydration, map viewport queries, and write-behind persistence.
- Routine actor state updates MUST be micro-batched (defaulting to 250ms intervals) via `SqlWriteBehindCoordinatorActor`. High-stakes financial and tactical mutations (Gold expenditures, battle resolutions, village conquests, troop dispatches) MUST trigger immediate transactional persistence flushes.

### V. Strict Rule & Fog-of-War Parity
All autonomous agents (Utility AI `BotPlayerActor`s) and delegated sessions (Sitters) MUST interact with the game engine exclusively through the standardized `IGameCommandGateway`. Autonomous bots MUST possess zero information beyond their player perception snapshot (visible map tiles, owned villages, allied reports, own battle/scout reports). Sitter actions MUST be validated against granular permission bitmasks prior to command dispatch.

### VI. Test-First & Canonical Mathematical Parity (TDD Mandatory)
Test-Driven Development (TDD) is non-negotiable across all domain logic. Before implementing any feature:
1. Automated unit tests asserting expected behaviors MUST be authored.
2. Mathematical parity against the canonical Terravia Renatus domain catalogs and golden test vectors (combat loss ratios, wall multipliers, loyalty attrition, catapult target distributions) MUST be proven through deterministic and property-based test suites.
3. Concurrency and timer sequencing MUST be verified using `Akka.TestKit` virtual-time schedulers before feature acceptance.

## Architectural & Technology Boundaries

### System Topology & Deployment Isolation
- The system operates under a **Single-World Per Deployment** model. Each game round MUST be encapsulated within an isolated deployment container and dedicated SQL Server database instance.
- Adding multi-tenant partitioning columns (e.g., `WorldId`) to core tables or domain contracts is forbidden.

### Solution Layering & Dependency Flow
- **One Type Per File (C#)**: Every public C# `class`, `record`, `struct`, `interface`, or `enum` MUST reside in its own dedicated `.cs` file whose filename matches the type name `1:1` (without splitting classes across `partial` `<TypeName>.<Feature>.cs` files).
- **`server/src/Terravia.Domain`**: Pure C# domain model, aggregate boundaries, value objects, pure math services, and command definitions. It MUST have ZERO dependencies on Akka.NET, EF Core, ASP.NET Core, SignalR, or third-party I/O frameworks.
- **`server/src/Terravia.Actors`**: Akka.NET actor hierarchy, scheduler actors, micro-batch flush coordination, and message routing. Depends on `Terravia.Domain`.
- **`server/src/Terravia.BotAI`**: Utility AI decision engine, consideration curves, and personality archetypes. Interacts with the game exclusively via `IGameCommandGateway`.
- **`server/src/Terravia.Infrastructure`**: SQL Server access layer (EF Core DbContext, Dapper TVP repositories, `SqlBulkCopy` world generation).
- **`server/src/Terravia.WebAPI`**: ASP.NET Core host, SignalR hubs, JWT authentication pipeline, and Akka.Hosting integration.
- **`client/packages/game-contracts`**: Single source of truth for TypeScript DTOs, SignalR protocol contracts, and 60fps client-side resource interpolation algorithms shared between `game-ui` and `admin-ui`.
- **`client/apps/game-ui`**: Full-canvas game client built on Phaser v4 within a SvelteKit SPA container. DOM elements MUST only be utilized for native input fields and clipboard operations.
- **`client/apps/admin-ui`**: High-density operations dashboard built with SvelteKit, Tailwind CSS v4, `shadcn-svelte`, and Apache ECharts.

### Naming & Structural Conventions
To maintain architectural clarity across all domain, actor, infrastructure, and contract layers, the codebase MUST enforce the following naming and organization standards:

1. **Vertical Feature-Folder Organization & 1-File-Per-Type Rule**:
   - Every project (`Terravia.Domain`, `Terravia.Actors`, `Terravia.Infrastructure`, `Terravia.WebAPI`) MUST be organized vertically by bounded domain feature (`Catalog/`, `Villages/`, `Players/`, `Alliances/`, `Movements/`, `Markets/`, `Heroes/`) rather than horizontal technical folders (`Models/`, `Rules/`, `Data/`, `Commands/`, `Services/`, `Entities/`, `Repositories/`, `Endpoints/`). Namespaces MUST match the folder hierarchy (`Terravia.Domain.Catalog`, `Terravia.Domain.Villages`, `Terravia.Infrastructure.Villages`).
   - Every file MUST contain a single primary type whose file name matches the type name (`VillageActor.cs` -> `sealed class VillageActor`, `VillageState.cs` -> `sealed class VillageState`). Splitting Aggregate Roots or Actors across multiple `partial` files (`VillageActor.Construction.cs`, `VillageActor.Combat.cs`) is prohibited; instead, keep actors as cohesive message-routing and timer coordinators that delegate domain behavior to dedicated `*Manager` classes.
2. **Domain Services (`*Manager`, `*Calculator`, `*Catalog`) vs. Application Endpoints (`*AppService`, `*Endpoints`)**:
   - Stateful or multi-aggregate domain orchestration services in `Terravia.Domain` MUST be suffixed with **`Manager`** (e.g., `VillageConstructionManager`, `OasisAnnexationManager`, `TrapperManager`, `HospitalHealingManager`, `AllianceBonusManager`) and injected as concrete classes (do NOT create redundant `I*Manager` interfaces unless multiple runtime implementations exist).
   - Pure mathematical evaluators and static lookup tables MUST be suffixed with **`Calculator`** or **`Catalog`** (e.g., `ConstructionCalculator`, `BuildingRuleCatalog`).
   - Administrative and HTTP orchestration services in `Terravia.WebAPI` MUST be suffixed with **`AppService`** or **`Endpoints`** — never conflating domain services with application services.
3. **Persistence Models, Technology-Prefixed Repositories & `*Consts`**:
   - Persistence and domain models MUST use clean, singular domain nouns (`Village`, `VillageBuilding`, `VillageQueueItem`, `VillageTroopStack`) and MUST NOT append the redundant `Entity` suffix.
   - Infrastructure repository implementations MUST be prefixed with their data-access technology and placed in the corresponding feature folder (`DapperVillageRepository`, `EfCorePlayerRepository`).
   - Database table names, schema prefixes, and field length/range constraints MUST be centralized in `TerraviaDbProperties` and feature-scoped `*Consts` classes (`CatalogConsts`, `VillageConsts`, `PlayerConsts`, `AllianceConsts`).
4. **Events (`*Event` vs. `*Eto`)**:
   - All events MUST be named in the **past tense** without an `On` prefix (`On*` is strictly reserved for event handler methods).
   - Events crossing actor, SignalR, or persistence process boundaries MUST be immutable records suffixed with **`Eto` (Event Transfer Object)** across both C# and TypeScript contracts (e.g., `VillageConstructionChangedEto`, `VillageStateFlushEto`).
5. **Data Transfer Objects, Commands & Queries (`*Dto`, `*Command`, `*Query`, `*Input`)**:
   - Outbound read projections and SignalR payloads MUST be suffixed with **`Dto`** (e.g., `VillageSlotDto`, `VillageSnapshotDto`).
   - State-mutating messages routed via `IGameCommandGateway` or actor timers MUST be suffixed with **`Command`** (`ConstructOrUpgradeBuildingCommand`, `ProcessDueVillageTimersCommand`).
   - Read-only actor or repository requests MUST be suffixed with **`Query`** or **`Input`** (`GetVillageStateQuery`, `GetMapViewportInput`).
6. **Hierarchical Error Codes, Permissions & Settings**:
   - Domain validation and rule failures MUST use structured `<Namespace>:<Feature>:<Code>` constants defined in `TerraviaDomainErrorCodes` (e.g., `Terravia:Villages:InsufficientResources`, `Terravia:Sitters:PermissionDenied`) to support deterministic testing and client-side localization.
   - Permissions and live-ops server settings MUST be organized in nested static hierarchy classes (`TerraviaPermissions`, `TerraviaSettings`) using dot-separated identifiers (`Terravia.Sitter.BuildAndDemolish`, `Terravia.Sitter.SpendGold`, `Terravia.Admin.ManageRound`).

## Quality Gates & Testing Standards

### Automated Testing Requirements
- Every domain pull request MUST include automated unit tests implementing the Red-Green-Refactor cycle.
- Combat, resource accrual, and building cost logic MUST include canonical golden-vector test coverage validating results against the authoritative Terravia Renatus catalog specifications.
- State machines and asynchronous actor workflows MUST be accompanied by `Akka.TestKit` automated test suites verifying race-condition-free execution under concurrent message delivery.

### Recommended Performance Benchmarks
- **Combat Simulation Throughput**: Stateless combat engines SHOULD achieve $\ge 50,000$ simulated encounters per second in unit benchmark harnesses.
- **Micro-Batch Write Latency**: Database write-behind flushes through Dapper TVPs SHOULD complete within $\le 250\text{ms}$ under nominal round concurrency.
- **Procedural World Generation**: Full map generation and `SqlBulkCopy` database population ($400 \times 400$ world) SHOULD execute in $\le 3\text{ seconds}$.

## Governance

### Constitutional Authority
This Constitution represents the supreme architectural and engineering policy for Terravia Renatus. All architectural plans, specifications, tasks, code implementations, and pull requests MUST comply with the rules established herein. `docs/architecture_and_features.md` serves as the authoritative blueprint and domain inventory.

### Amendment Procedure
1. Proposed changes to this Constitution MUST be documented with explicit technical rationale, architectural trade-off evaluations, and migration impact plans.
2. Any amendment requires review and formal approval.
3. Changes to templates and downstream artifacts MUST be synchronized upon amendment ratification.

### Versioning Policy
This Constitution follows Semantic Versioning (`MAJOR.MINOR.PATCH`):
- **MAJOR**: Incompatible changes, deletions, or structural redefinitions of Core Principles or Architectural Boundaries.
- **MINOR**: Addition of new principles, structural expansion of guidelines, or newly ratified technology boundaries.
- **PATCH**: Non-semantic clarifications, typographical corrections, formatting adjustments, or link updates.

**Version**: 1.1.0 | **Ratified**: 2026-11-06 | **Last Amended**: 2026-11-06
