<!--
Sync Impact Report:
- Version change: Uninitialized Template -> 1.0.0 (Initial Ratification)
- Modified principles: None (initial creation from architecture blueprint)
- Added sections:
  - Core Principles:
    - I. Actor Boundaries as DDD Aggregate Roots
    - II. Zero-Allocation Hot Paths & Pure Domain Services
    - III. Lazy Evaluation Over Polling Loops
    - IV. Hybrid Data Access & Write-Behind Micro-Batching
    - V. Strict Rule & Fog-of-War Parity
    - VI. Test-First & TravianZ Equivalence (TDD Mandatory)
  - Architectural & Technology Boundaries
  - Quality Gates & Testing Standards
  - Governance
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

### VI. Test-First & TravianZ Equivalence (TDD Mandatory)
Test-Driven Development (TDD) is non-negotiable across all domain logic. Before implementing any feature:
1. Automated unit tests asserting expected behaviors MUST be authored.
2. Mathematical parity against legacy TravianZ v11 baseline formulas (combat loss ratios, wall multipliers, loyalty attrition, catapult target distributions) MUST be proven through deterministic and property-based test suites.
3. Concurrency and timer sequencing MUST be verified using `Akka.TestKit` virtual-time schedulers before feature acceptance.

## Architectural & Technology Boundaries

### System Topology & Deployment Isolation
- The system operates under a **Single-World Per Deployment** model. Each game round MUST be encapsulated within an isolated deployment container and dedicated SQL Server database instance.
- Adding multi-tenant partitioning columns (e.g., `WorldId`) to core tables or domain contracts is forbidden.

### Solution Layering & Dependency Flow
- **`server/src/Terravia.Domain`**: Pure C# domain model, aggregate boundaries, value objects, pure math services, and command definitions. It MUST have ZERO dependencies on Akka.NET, EF Core, ASP.NET Core, SignalR, or third-party I/O frameworks.
- **`server/src/Terravia.Actors`**: Akka.NET actor hierarchy, scheduler actors, micro-batch flush coordination, and message routing. Depends on `Terravia.Domain`.
- **`server/src/Terravia.BotAI`**: Utility AI decision engine, consideration curves, and personality archetypes. Interacts with the game exclusively via `IGameCommandGateway`.
- **`server/src/Terravia.Infrastructure`**: SQL Server access layer (EF Core DbContext, Dapper TVP repositories, `SqlBulkCopy` world generation).
- **`server/src/Terravia.WebAPI`**: ASP.NET Core host, SignalR hubs, JWT authentication pipeline, and Akka.Hosting integration.
- **`client/packages/game-contracts`**: Single source of truth for TypeScript DTOs, SignalR protocol contracts, and 60fps client-side resource interpolation algorithms shared between `game-ui` and `admin-ui`.
- **`client/apps/game-ui`**: Full-canvas game client built on Phaser v4 within a SvelteKit SPA container. DOM elements MUST only be utilized for native input fields and clipboard operations.
- **`client/apps/admin-ui`**: High-density operations dashboard built with SvelteKit, Tailwind CSS v4, `shadcn-svelte`, and Apache ECharts.

## Quality Gates & Testing Standards

### Automated Testing Requirements
- Every domain pull request MUST include automated unit tests implementing the Red-Green-Refactor cycle.
- Combat, resource accrual, and building cost logic MUST include equivalence test coverage validating results against TravianZ v11 reference data.
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

**Version**: 1.0.0 | **Ratified**: 2026-11-06 | **Last Amended**: 2026-11-06
