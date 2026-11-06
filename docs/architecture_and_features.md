# Terravia Renatus: Architecture Blueprint & Domain Systems Specification

> [!IMPORTANT]
> **Shared Design Alignment Summary (Questions 1–15)**
> - **World Topology**: **Single-World Per Deployment** — One isolated container/process + one dedicated SQL Server database per game round (~500–5,000 players + bots). No `WorldId` tenant key overhead; trivial round resets and container archival.
> - **Repository Layout**: Self-contained `server/` (`server/Terravia.sln`, `server/src/` for C# projects [`Terravia.Domain`, `Terravia.Actors`, `Terravia.BotAI`, `Terravia.Infrastructure`, `Terravia.WebAPI`], and `server/tests/` for test projects, with Akka.NET hosted in-process inside `Terravia.WebAPI` via `Akka.Hosting`) and root-level `client/` monorepo (`apps/game-ui`, `apps/admin-ui`, `packages/game-contracts`, `packages/shared-ui`).
> - **Actor State, Timing & Durability**: Stateful In-Memory Domain Actors with lazy timestamp-delta resource math, millisecond-precision Akka timers (`DateTimeOffset` ms + FIFO `MovementId` tie-breaker), immediate SQL flush on critical events (Gold/Battle/Movement), and `<250ms` command-triggered micro-batch TVP flushes + unified `GameAuditLogs`.
> - **Database & Provisioning**: Modernized SQL Server schema (**EF Core** for migrations/auth/admin CRUD; **Dapper + TVPs** for actor hydration, micro-batch write-behind, map viewports, and rankings) + procedural C# `WorldGenerator` (`SqlBulkCopy` in `<3s` for `400x400` maps, triggerable via env auto-seed or `admin-ui` Reset Wizard).
> - **Auth, Sitters & Balance Catalogs**: Scoped JWTs with Sitter delegation claims (`{ sub: ownerId, act: sitterId, perms: bitmask }`) for zero false-positive MAD flags, and compiled read-only memory catalogs hydrated from versioned JSON/DB exposed via hash-cached `GET /api/game/catalog`.
> - **Bot-End**: Dual-mode C# 10 **Utility AI** running by default as in-process `BotPlayerActor`s via `IGameCommandGateway` (plus optional SignalR/HTTP load-test mode) with Raider, Builder, and Defender personalities and circadian activity schedules.
> - **Frontend Stack**: **Full-Canvas Phaser v4** in `client/apps/game-ui` (parallel `UIScene` for HUD/resource bars/popups over `ResourceFieldsScene`, `VillageCenterScene`, `WorldMapScene`, and `BattleReplayScene`, using Phaser scale-synced `DOMElement` for native text inputs/clipboard) + **Tailwind CSS v4 + `shadcn-svelte` (Bits UI) + ECharts** in `client/apps/admin-ui`.

---

## Part I: Architecture & Best Practices for Terravia Renatus

### 1. Architectural Principles: Pragmatic DDD & Performance-First S.O.L.I.D

Classic browser RTS engines fail at scale when every page view or 1-second polling tick executes database `SELECT`/`UPDATE` queries across villages, troop queues, and movements.

To balance **S.O.L.I.D + DDD** with **Performance-First** execution:

1. **Aggregate Root = Actor Boundary**: In DDD, an Aggregate Root enforces invariants and serializes state mutations. In Akka.NET, each `VillageActor`, `PlayerActor`, `AllianceActor`, and `MarketActor` **is** the in-memory Aggregate Root. Single-threaded message processing inside each actor eliminates database row-locking and race conditions (`TOCTOU`) by design.
2. **Zero-Allocation Hot Paths**: Inside `VillageActor` and `BattleEngine`, avoid object-graph bloat. Represent per-village building slots (`40` slots + Wonder slot `99`), troop counts (`90` unit types across 9 tribes), and smithy upgrades as contiguous `int[]` or `InlineArray` / `Span<int>` structures in RAM rather than 90 separate entity objects.
3. **Pure Static Domain Services for Math & Combat**: Keep combat resolution (`BattleSimulator`), production formulas (`ResourceProductionCalculator`), and prerequisite tables (`BuildingRuleCatalog`) as **stateless, deterministic C# structs/classes** injected via interfaces (`IBattleSimulator`, `IBuildingRuleCatalog`). This obeys **Single Responsibility (SRP)** and **Open/Closed Principle (OCP)** while enabling 100,000+ combat simulations per second in unit tests and Utility AI evaluations.
4. **Lazy Evaluation Over Polling Loops**: Never run a 1-second loop over 20,000 villages to add resources. Store `{ BaseAmount, ProductionPerHour, StorageCap, LastCalculatedAtMs }` in `VillageActor`. Current resources at any instant $t$ are calculated in $O(1)$ time:
   $$\text{Resource}(t) = \min\left(\text{StorageCap}, \text{BaseAmount} + \text{ProductionPerHour} \times \frac{t - \text{LastCalculatedAtMs}}{3,600,000}\right)$$
   You only mutate `BaseAmount` and `LastCalculatedAtMs` when a command deducts/adds resources or a building/oasis/hero bonus changes `ProductionPerHour`.

---

### 2. Solution Structure (`server/src` + `server/tests` & `client/` Root Layout)

```text
terravia-renatus/
├── docs/
│   └── architecture_and_features.md
├── server/
│   ├── Terravia.sln
│   ├── src/
│   │   ├── Terravia.Domain/          # Pure C# Aggregates, ValueObjects, Commands/DTOs, IGameCommandGateway, Frozen Catalogs, Battle Math
│   │   ├── Terravia.Actors/          # Akka.NET Actor System, Hierarchy, Timers, Sitter Pipeline, <250ms Micro-Batch Flush Coordinator
│   │   ├── Terravia.BotAI/           # C# Utility AI Engine, Considerations, Response Curves, Personalities
│   │   ├── Terravia.Infrastructure/  # SQL Server: EF Core DbContext, Dapper TVP Repos, SqlBulkCopy WorldGenerator
│   │   └── Terravia.WebAPI/          # ASP.NET Core Web API, Akka.Hosting IHostedService, SignalR Hubs (GameHub, AdminHub), JWT
│   └── tests/
│       ├── Terravia.Domain.Tests/    # Property-based & canonical golden-vector verification tests for domain math
│       ├── Terravia.Actors.Tests/    # Akka.TestKit deterministic timer & movement tests
│       └── Terravia.BotAI.Tests/     # Utility AI decision scoring benchmarks
└── client/
    ├── package.json                  # pnpm / Vite monorepo root
    ├── packages/
    │   ├── game-contracts/           # Shared TypeScript DTOs, SignalR schemas, Catalog types, 60fps interpolation math
    │   └── shared-ui/                # Shared utilities, auth/token stores, SignalR client manager
    └── apps/
        ├── game-ui/                  # SvelteKit (SPA shell) + Phaser v4 Full-Canvas Game Client (UIScene + World Scenes)
        └── admin-ui/                 # SvelteKit + Tailwind CSS v4 + shadcn-svelte (Bits UI) + ECharts Live-Ops Portal
```

---

### 3. Backend Architecture (Akka.NET, C#, SQL Server, SignalR)

#### 3.1 Akka.NET Single-World Actor Hierarchy

Because each deployment hosts a single dedicated game world (round), the `ActorSystem` (`TerraviaSystem`) is 100% dedicated to that round with flat, low-latency actor paths and zero multi-tenant routing overhead:

```mermaid
flowchart TD
    Root["/user (TerraviaSystem Root)"]
    
    WCoord["WorldCoordinatorActor (Round State, Speed Config, Milestones)"]
    VSup["VillageSupervisorActor"]
    PSup["PlayerSupervisorActor"]
    ASup["AllianceSupervisorActor"]
    Mov["MovementSchedulerActor (Global Min-Heap)"]
    Mkt["MarketAndAuctionActor"]
    Archon["ArchonDirectorActor (Artifacts & WW AI)"]
    BotDir["BotDirectorActor (Manages Utility AI Bots)"]
    Flush["SqlWriteBehindCoordinatorActor"]
    Bridge["SignalRBridgeActor"]

    Root --> WCoord
    Root --> VSup
    Root --> PSup
    Root --> ASup
    Root --> Mov
    Root --> Mkt
    Root --> Archon
    Root --> BotDir
    Root --> Flush
    Root --> Bridge

    VSup --> V1["VillageActor (villageId: 1001)"]
    VSup --> V2["OasisActor (tileId: 1002)"]
    PSup --> P1["PlayerActor (playerId: 42 + HeroState)"]
    ASup --> A1["AllianceActor (allianceId: 7)"]
    BotDir --> B1["BotPlayerActor (playerId: 501 - Raider)"]
    BotDir --> B2["BotPlayerActor (playerId: 502 - Builder)"]
```

#### 3.2 Key Actor Responsibilities & Lifecycle

| Actor | State Held in RAM | Timers Scheduled (`ITimerScheduler`) | Passivation / Lifecycle |
| :--- | :--- | :--- | :--- |
| **`VillageActor`** | Resource balances & rates, 40 building slots + World Wonder (`Slot 99`), active build/demolition queues, garrisoned & owned troops, smithy tech levels, active celebrations/festivals, hospital wounded queue, starvation timestamp. | Exact timer for next `BuildCompleted`, `TrainingBatchCompleted`, `ResearchCompleted`, `CelebrationEnded`, `StarvationTick`. | Passivates after 30m of player inactivity **only if** no timers are pending; wakes automatically on incoming command or movement arrival. |
| **`PlayerActor`** | Profile, Gold/Silver balance, Plus/GoldClub expirations, Hero state + equipped `HeroItems`, active `HeroAdventure` list, Sitter permissions, Push-Protection rolling counters. | `HeroRegenTick`, `PlusBonusExpired`, `VacationModeEnded`. | Active while player (or Sitter/Bot) is online or Hero is on mission. |
| **`MovementSchedulerActor`** | Priority Queue (Min-Heap sorted by `(ArrivalTimeMs, MovementId)`) of all in-flight troop and merchant movements across the game world. | Single high-precision timer always set to the **earliest** `ArrivalTimeMs` in the heap. | Singleton per server. On wakeup, pops all movements where `ArrivalTimeMs <= NowMs` in strict FIFO order and dispatches `ResolveArrivalCommand` to target `VillageActor`/`OasisActor`. |
| **`AllianceActor`** | Member roster & ranks/permissions, diplomacy treaties, Alliance Bonus pools & levels (`Recruitment`, `Philosophy`, `Metallurgy`, `Commerce`). | `AllianceBonusUpgradeCompleted`. | One actor per active Alliance. |
| **`BotPlayerActor`** | Bot personality profile, blackboard memory (known farm targets, threat history, build plan), circadian schedule. | Staggered `EvaluateUtilityTick` (e.g., every 15–90 seconds with random jitter, paused during bot "sleep" hours). | Managed by `BotDirectorActor`; throttled automatically if server CPU exceeds threshold. |
| **`SqlWriteBehindCoordinatorActor`** | Dirty-entity buffer (`Channel<IDirtyStateSnapshot>`). | Every `250ms` (or when buffer hits `500` items), flushes dirty states to SQL Server via Dapper Table-Valued Parameters (TVPs). | Singleton with graceful shutdown flush guarantee (`CoordinatedShutdown`). |

#### 3.3 Deterministic Movement & Combat Protocol (Zero Race Conditions)

In naive polling-based movement processors, concurrent movements arriving at the same village can race with training or building completions. In **Terravia Renatus**:
1. When `VillageActor(A)` sends an attack/reinforcement to `VillageActor(B)`, `VillageActor(A)` validates & deducts the troops in RAM, persists the new `Movement` row, and sends `ScheduleMovement(movement)` to `MovementSchedulerActor`.
2. `VillageActor(B)` receives an `IncomingMovementNotification` (with masked troop counts unless revealed by Rally Point/Artifact Sight rules) and pushes a real-time alert to Player B via SignalR.
3. At `ArrivalTimeMs` (millisecond precision, ties broken deterministically by `MovementId`), `MovementSchedulerActor` sends `ProcessArrivingMovement(movement)` to `VillageActor(B)`.
4. **Inside `VillageActor(B)`**: Before resolving the arrival, `VillageActor(B)` synchronously advances its own lazy resource/training/building state up to `movement.ArrivalTimeMs`. This guarantees that any troop trained or wall built at `ArrivalTimeMs - 1ms` participates in the battle, and anything finishing at `ArrivalTimeMs + 1ms` does not!
5. `VillageActor(B)` invokes the pure `BattleEngine.Resolve(...)`, updates garrison/building/loyalty state, emits `BattleReportCreated`, schedules survivor return via `MovementSchedulerActor`, and triggers an **immediate persistence flush** for the battle outcome.

---

#### 3.4 SQL Server Hybrid Strategy (EF Core + Dapper) & Modernized Schema

Instead of wide 90-column horizontal tables, **Terravia Renatus** uses a normalized, high-density single-world SQL Server schema with compact indexes:

##### Schema Design Highlights
- **`ServerConfig`**: Single-row / key-value configuration table for the round (`ServerName, Speed, TroopSpeed, EvasionSpeed, StorageMultiplier, MapRadius, StartDateUtc, Status, IsMaintenanceMode, AllianceBonusesEnabled`).
- **`Players`**: `(PlayerId, Username, PasswordHash, Email, TribeId, AccessRole, Gold, Silver, CulturePoints, PlusExpiresAt, GoldClubExpiresAt, BonusExpiresJson, Sitter1Id, Sitter1PermMask, Sitter2Id, Sitter2PermMask, AllianceId, ProtectionExpiresAt, VacationModeUntil, IsBot, BotPersonalityId)`.
- **`MapTiles`**: `(TileId, X, Y, FieldTopologyId, OasisTypeId, BestOasisCropBonus, OccupiedByVillageId)` with a composite spatial index `IX_MapTiles_XY (X, Y) INCLUDE (FieldTopologyId, OasisTypeId, OccupiedByVillageId)`.
- **`Villages`**:
  - Scalar columns for indexed/queried fields: `(VillageId, TileId, OwnerPlayerId, Name, IsCapital, IsArchon, Population, CulturePointsPerDay, Loyalty, Wood, Clay, Iron, Crop, WarehouseCap, GranaryCap, LastUpdatedMs)`.
  - **Normalized high-density child tables**:
    - `VillageBuildings`: `(VillageId, SlotId [1..40, 99], BuildingTypeId [1..50], Level)` — indexed by `PK (VillageId, SlotId)`.
    - `VillageTroopStacks`: Unified table for all stationed, reinforcing, trapped, and hospitalized units: `(StackId, LocationVillageId, OwnerVillageId, StackType [Home/Reinforcement/Trapped/Hospital], UnitTypeId [1..90, 99], Count)`. An army of 3 unit types takes 3 compact rows instead of a 95-column row of zeros.
    - `VillageQueueItems`: Unified table for Building, Demolition, Training, Healing, and Research queues: `(QueueItemId, VillageId, QueueCategory, TargetId, TargetSlotOrLevel, Quantity, EachDurationMs, StartedAtMs, FinishesAtMs, IsWaitingLoop, IsMasterBuilder)`.

##### EF Core vs. Dapper Division Matrix

| Workload | Tool | Why / Best Practice |
| :--- | :--- | :--- |
| **Schema Migrations, Seeding & Rule Catalogs** | **EF Core** | Code-first migrations, world installation/reset, strongly-typed seeding of Tribes, 50 Buildings, 90 Units, Hero Items, and Quests. |
| **Auth, Account Settings, Admin CRUD** | **EF Core** | Low-frequency, high-invariant CRUD where change tracking and navigation properties shine. |
| **Actor State Hydration (`VillageActor` startup)** | **Dapper (`QueryMultipleAsync`)** | Fetches `Village` + `VillageBuildings` + `VillageTroopStacks` + `VillageQueueItems` in a **single round-trip** multi-resultset SQL batch in `< 1.5ms`. |
| **Write-Behind Batch Flushing (`SqlWriteBehindCoordinatorActor`)** | **Dapper + SQL Server TVPs** | Passes a `DataTable` / `SqlDataRecord` Table-Valued Parameter (`@VillageUpdates`, `@StackUpdates`) to a compiled stored procedure `usp_FlushVillageStates`, updating 500 villages in a single ~5ms transaction. |
| **World Map Viewport & Crop Finder** | **Dapper** | Queries `IX_MapTiles_XY` with `X BETWEEN @xMin AND @xMax AND Y BETWEEN @yMin AND @yMax` directly into lightweight `MapTileDto` structs with zero EF tracking overhead. |
| **Leaderboards, MAD & Push-Protection Analytics** | **Dapper** | Executes windowed aggregations (`RANK() OVER (...)`) and 7-day rolling transfer sums directly in SQL Server. |

---

#### 3.5 SignalR Real-Time Architecture

```mermaid
sequenceDiagram
    participant UI as SvelteKit GameUI
    participant Hub as SignalR GameHub
    participant Bridge as Akka SignalRBridgeActor
    participant VActor as VillageActor (villageId: 1001)

    UI->>Hub: Connect (JWT) + SubscribeVillage(1001)
    Hub->>Bridge: SubscribePlayer(connectionId, playerId, activeVillageId: 1001)
    Bridge->>VActor: GetVillageSnapshot(villageId: 1001)
    VActor-->>UI: VillageSnapshotDto { resources, rates, queues, troops, serverTimeMs }
    Note over UI: UI interpolates resources locally at 60fps using rates & serverTimeMs
    
    VActor->>VActor: Timer Fires: BuildCompleted(Slot 19, Barracks Lvl 3)
    VActor->>Bridge: PublishEvent(VillageUpdatedDelta)
    Bridge->>Hub: Clients.Group("player:42").SendAsync("OnVillageDelta", delta)
    Hub-->>UI: Apply new ProductionPerHour / Queue state & recalculate local interpolation
```

- **SignalR Groups**:
  - `server:global` — Server announcements, World Wonder milestones, Global Chat messages & live poll votes.
  - `alliance:{allianceId}` — Alliance chat, shared battle reports, incoming attack alerts on alliance members, Alliance Bonus progress.
  - `player:{playerId}` — Incoming movement alerts, unread message/report badges, Hero status changes, Gold/Silver updates.
  - `village:{villageId}` — Active village building/training/research completions and resource rate changes (subscribed when player switches active village).

#### 3.6 Crash Durability, Unified Audit Logging & Provisioning
1. **Command-Triggered Micro-Batch Persistence (`< 250ms`)**:
   - Because resource accumulation is a pure function of `(BaseAmount, ProductionPerHour, LastUpdatedMs)`, routine seconds passing require **zero database writes**.
   - Only **explicit commands** (e.g., `ConstructOrUpgradeBuildingCommand`, `TrainUnitsCommand`, `DonateAllianceBonusCommand`) and **timer completions** (`BuildCompleted`, `TrainingBatchCompleted`) mutate actor state.
   - High-stakes mutations (`SpendGold`, `DispatchTroopMovement`, `ResolveBattle`, `ConquerVillage`, `AuctionBid`) flush synchronously/immediately; routine village mutations and audit entries are micro-batched every `250ms` via Dapper TVPs in `SqlWriteBehindCoordinatorActor`, with `CoordinatedShutdown` flushing any remaining items on container stop.
2. **Unified `GameAuditLogs` Table + OpenTelemetry**:
   - Consolidates all construction, research, Gold financial, market, moderation, authentication, security, and alliance audit trails into a single indexed, partitioned `GameAuditLogs` table: `(LogId, TimestampMs, Category, ActorPlayerId, TargetPlayerId, VillageId, AllianceId, ActionCode, GoldDelta, PayloadJson, IpAddress)`.
   - Structured logs and actor metrics (active actors, mailbox depth, TVP flush latency, bot tick duration) are exported via **Serilog + OpenTelemetry** to `admin-ui`.
3. **Scoped JWT Authentication & Sitter Delegation**:
   - Primary login issues a JWT (`{ sub: playerId, role: Player|Enforcer|Admin }`).
   - Switching to a sitted account issues a short-lived delegated Sitter JWT containing `{ sub: ownerPlayerId, act: sitterPlayerId, perms: sitPermBitmask }`.
   - A central Akka command pipeline validates `perms` (e.g., `SITTER_PERM_RAID`, `SITTER_PERM_ATTACK`, `SITTER_PERM_GOLD`) before routing to `PlayerActor`/`VillageActor` and records both `ActorPlayerId` and `TargetPlayerId` in `MultiAccountSessions` and `GameAuditLogs` so legal sitting never triggers false-positive multi-account alerts.
4. **Compiled Read-Only Balance Catalogs & `< 3s` Procedural `WorldGenerator`**:
   - Base definitions for the 9 Tribes, 90 Units (`+ Trap u99`), 50 Buildings, Hero Items, and Quests live in versioned JSON files hydrated at boot into immutable memory tables (`FrozenDictionary` / `ImmutableArray`) in `Terravia.Domain`, merged with DB overrides (`ServerConfig`, `QuestConfig`), and served to `client/` via hash-cached `GET /api/game/catalog` (`ETag`).
   - `WorldGenerator` procedurally generates up to `400x400` (`641,601` tiles) maps, computes `BestOasisCropBonus` for every 9c/15c cropper, seeds Nature oasis garrisons, NPC accounts (`Nature`, `Archons`, `Taskmaster`, `Enforcer`), and initial Utility AI Bots, streaming everything to SQL Server via `SqlBulkCopy` in `< 3 seconds` (triggered via environment config on first boot or from the `admin-ui` Round Reset Wizard).

---

### 4. Frontend Architecture: `client/apps/game-ui` & `client/apps/admin-ui`

#### 4.1 Technology Split: Full-Canvas Phaser v4 (`game-ui`) vs. Data-Rich Dashboard (`admin-ui`)

| App | Core Rendering & UI Stack | Responsibility |
| :--- | :--- | :--- |
| **`client/apps/game-ui`** | **SvelteKit (`ssr = false` SPA shell) + Vite + Phaser v4 (WebGPU/WebGL2 Full-Canvas)** | Renders the entire gameplay experience inside Phaser v4 scenes (`UIScene` HUD, resource bars, village switcher, modal popups, `ResourceFieldsScene`, `VillageCenterScene`, `WorldMapScene`, `BattleReplayScene`), using Phaser's scale-synced `DOMElement` strictly for `<input>`/`<textarea>` fields so native mobile keyboards, IME, and copy/paste work seamlessly. |
| **`client/apps/admin-ui`** | **SvelteKit + Vite + Tailwind CSS v4 (`@tailwindcss/vite`) + `shadcn-svelte` (Bits UI) + Apache ECharts** | High-density Live-Ops & Moderation portal: Server Setup/Reset Wizard, Multi-Account Detection (MAD) network graphs, 7-day Push-Protection tables, World Map Heatmap, Quest/Catalog Editor, and Live Utility AI Bot Director. |
| **`client/packages/game-contracts`** | **TypeScript Strict** | Shared DTOs, SignalR hub contracts, `GameCatalog` lookup helpers, and deterministic 60fps resource/timer interpolation math used by both Phaser scenes and Admin tools. |

#### 4.2 Phaser v4 Multi-Scene Architecture in `client/apps/game-ui`

```mermaid
flowchart TD
    SvelteShell["SvelteKit App Shell (Auth, SignalR Client, Catalog Loader)"]
    Store["Reactive GameState Store (Server Clock Sync + 60fps Resource Interpolation)"]
    
    SvelteShell --> Store
    Store <--> PhaserGame["Phaser v4 Game Instance (WebGPU / WebGL2)"]

    subgraph ParallelScenes["Active Phaser v4 Scenes"]
        Boot["BootAndPreloadScene (Loads Texture Atlases & Catalog)"]
        UI["UIScene (Always-On Top Layer: Resource Bar, Village Sidebar, Queue Timers, Modal Windows, DOMElement Inputs)"]
        
        subgraph WorldViewport["Active Viewport Scene (Switched Instantly)"]
            Outskirts["ResourceFieldsScene (Outskirts: 18 Resource Slots + Workers)"]
            Core["VillageCenterScene (Settlement Core: 22 Inner Slots + Wall + Wonder Slot 99)"]
            WorldMap["WorldMapScene (World Map: Infinite Pan/Zoom Tilemap, Oases, Troop Vectors)"]
            Replay["BattleReplayScene (Animated Clash from BattleReport)"]
        end
    end

    PhaserGame --> Boot
    PhaserGame --> UI
    PhaserGame --> WorldViewport
```

1. **`BootAndPreloadScene`**: Fetches `/api/game/catalog` and loads the **Terravia Renatus** semantic asset manifest (`load.pack('terravia-assets', ...)`), supporting both procedural geometric dev atlases and production WebP/KTX2 texture atlases.
2. **`UIScene` (Persistent Overlay Scene)**:
   - Renders the top Wood/Clay/Iron/Crop/Upkeep bar (updated every frame from the deterministic interpolation formula), Gold/Silver counters, Hero status portrait, Village list switcher, active Building/Training/Movement countdown widgets, and modal windows (Buildings, Marketplace, Rally Point, Alliance, Reports, Hero Inventory/Auction, Global Chat).
   - Uses Phaser's `this.add.dom(x, y, ...)` inside modal windows for text/number inputs (troop dispatch counts, market offers, chat/IGM messages, coordinate inputs) so players get native OS copy-paste, tab navigation, and mobile keyboards inside the full-canvas UI.
3. **Spatial Viewport Scenes (`ResourceFieldsScene`, `VillageCenterScene`, `WorldMapScene`, `BattleReplayScene`)**:
   - **Camera & Culling**: `WorldMapScene` uses Phaser's `Cameras.Scene2D` for smooth drag-panning and pinch-to-zoom across the `400x400` world, streaming `32x32` tile chunks from `GET /api/game/map` and rendering alliance diplomacy borders, `9c`/`15c` crop-finder badges, and animated marching troop lines.
   - **`BattleReplayScene`**: Turns any deterministic `BattleReport` into an optional animated visual replay showing attacking vs. defending unit stacks, ram/catapult volleys, trap captures, and hero combat effects.

#### 4.3 `client/apps/admin-ui` Capabilities (Tailwind CSS v4 + `shadcn-svelte` + ECharts)
1. **Round Provisioning & Live Config**: Configure round speed multipliers, toggle endgame systems, manage maintenance mode, and trigger the `< 3s` `SqlBulkCopy` Round Reset Wizard.
2. **Anti-Cheat & Moderation Center**:
   - **Multi-Account Detection (MAD) Graph (ECharts Force-Directed Graph)**: Visualizes suspicious account pairs scored by shared IP/subnet, User-Agent hash, login switch windows (`< 15m`), and resource push flows.
   - **Push-Protection Dashboard**: 7-day rolling resource transfer balance vs. allowed hours of production with 1-click World Wonder/Artifact exemptions.
3. **World Map Heatmap (ECharts Canvas Heatmap)**: Visual overlay of player density, combat casualties, resource flows, and Archon/Artifact distribution.
4. **Bot AI Live-Ops Director**: Spawn/despawn Utility AI bots, adjust personality ratios, inspect a bot's live Utility AI consideration scores, and monitor OpenTelemetry server/actor health.

---

### 5. Bot-End Architecture (C# + Utility AI)

#### 5.1 Dual-Mode Execution via `IGameCommandGateway`
Bots never mutate database rows or actor fields directly. Every `BotPlayerActor` interacts with the game through `IGameCommandGateway`:
- **In-Process Mode (Default Production)**: `AkkaGameCommandGateway` sends typed command messages (`ConstructOrUpgradeBuildingCommand`, `TrainUnitsCommand`, `SendTroopsCommand`, `CreateMarketOfferCommand`) directly to the target `PlayerActor` / `VillageActor`. Zero HTTP/JSON overhead allows **2,000+ intelligent bots** on a single node using `< 5%` CPU.
- **External Network Mode (Load Testing)**: `SignalRHttpGameCommandGateway` connects over WebSockets/REST using a bot JWT to stress-test the full Kestrel + SignalR + API pipeline.
- **Strict Fog-of-War & Rule Parity**: Bots only know what their `PlayerViewSnapshot` exposes (their own villages, public map tiles, alliance shared reports, and their own scout/battle reports).

#### 5.2 Utility AI Architecture in C#

Utility AI evaluates a set of candidate `BotAction`s on each decision tick and scores each action $a$ in $[0.0, 1.0]$ by multiplying normalized **Considerations** passed through parameterized **Response Curves** (Linear, Polynomial, Logistic, Logit), compensated by the geometric mean modification factor so adding more considerations doesn't artificially depress scores:

$$\text{Score}(a) = \text{Weight}_{\text{personality}}(a) \times \left(\prod_{i=1}^{n} c_i\right)^{\text{ModificationFactor}}$$

```mermaid
flowchart LR
    Percept["Bot Perception Snapshot\n(Resources, Queues, Threats, Map Targets)"]
    
    subgraph Scorers["Utility AI Action Scorers"]
        S1["UpgradeResourceField\n(Crop Deficit / RoI Curve)"]
        S2["UpgradeInfrastructure\n(Warehouse Cap / Prerequisite Curve)"]
        S3["TrainMilitary\n(Off/Def Ratio & Threat Curve)"]
        S4["SendFarmRaid\n(Expected Loot / Distance / Risk Curve)"]
        S5["DefendOrEvade\n(Incoming Attack Power vs Garrison)"]
        S6["FoundOrConquerVillage\n(CP Ready & Settlers/Chief Ready)"]
    end

    Percept --> Scorers
    Scorers --> Selector["Weighted Top-K Selector + Human Delay Jitter"]
    Selector --> Gateway["IGameCommandGateway"]
```

#### 5.3 Bot Archetypes (Personalities) & Human-Like Realism
1. **The Raider (`Teuton / Hun` bias)**: High weight on `SendFarmRaid` and `TrainOffense`; builds Barracks/Stable early, maintains active farm lists of nearby oases and inactive villages, uses Gold Club evasion or manual troop dodging when attacked by superior forces.
2. **The Simmer / Economy Builder (`Egyptian / Gaul` bias)**: Prioritizes resource fields, bonus buildings (Sawmill, Bakery, Waterworks), Crannies/Trappers, Town Hall celebrations, and rapid Culture Point expansion to settle 9c/15c croppers.
3. **The Anvil / Defender (`Roman / Spartan / Gaul` bias)**: Trains high infantry/cavalry defense ratios, upgrades Walls & Hospital, responds to `AllianceIncomingAttackAlert` by sending timed reinforcements to allied villages.
4. **Circadian Activity & Anti-Robotic Jitter**:
   - Each bot is assigned a simulated timezone and sleep window (e.g., `01:00–07:30` local time) where it does not issue routine actions.
   - Incoming attack alerts trigger a human reaction delay (e.g., `30s–180s` log-normal distribution) before the bot evaluates `DefendOrEvade`.

---

## Part II: Terravia Renatus Domain Mechanics & Systems Specification

Below is the complete, self-contained inventory of game systems, mathematical formulas, and domain rules implemented across `Terravia.Domain`, `Terravia.Actors`, and `Terravia.Infrastructure`.

### 1. Core Tribes, Units & Buildings Catalog

| Domain Area | Terravia Domain Modules & Contracts | Domain Rules, Formulas & Mechanics Specification |
| :--- | :--- | :--- |
| **9 Tribes (7 Playable + 2 NPC)** | `IGameCatalog`, `TribeDefinition`, `UnitDefinition` (`tribes.json`, `units.json`) | **Playable**: `1` Romans (`u1..u10`), `2` Teutons (`u11..u20`), `3` Gauls (`u21..u30`), `6` Huns (`u51..u60`), `7` Egyptians (`u61..u70`), `8` Spartans (`u71..u80`), `9` Vikings (`u81..u90`).<br>**NPC**: `4` Nature/Oasis animals (`u31..u40`), `5` Archons (`u41..u50`).<br>Formula: Unit IDs for tribe $T$ are $(T-1)\times 10 + 1 \dots (T-1)\times 10 + 10$ (plus Gaul Traps `u99`). |
| **50 Building Types (`BuildingTypeId 1..50`)** | `IBuildingRuleCatalog`, `BuildingDefinition` (`buildings.json`) | **Resources (`1..4`)**: Woodcutter, Clay Pit, Iron Mine, Cropland (`Slot 1..18`).<br>**Bonus (`5..9, 45`)**: Sawmill, Brickyard, Iron Foundry, Grain Mill, Bakery, **Waterworks (`45`, Egyptians)**.<br>**Storage (`10, 11, 38, 39`)**: Warehouse, Granary, Great Warehouse, Great Granary.<br>**Military (`12..14, 16, 19..22, 29, 30, 36, 37, 41, 46, 48, 49`)**: Blacksmith (`12`), Armoury (`13`), Tournament Square (`14`), Rally Point (`16`, `Slot 39`), Barracks (`19`), Stable (`20`), Workshop (`21`), Academy (`22`), Great Barracks (`29`), Great Stable (`30`), **Trapper (`36`, Gauls)**, Hero's Mansion (`37`), **Horse Drinking Trough (`41`, Romans)**, **Hospital (`46`)**, **Big Hospital (`48`, Spartans/Vikings)**, **Great Workshop (`49`)**.<br>**Walls (`31..33, 42, 43, 47, 50` on `Slot 40`)**: City Wall (Romans), Earth Wall (Teutons), Palisade (Gauls), Stone Wall (`42`, Egyptians), Makeshift Wall (`43`, Huns), Defensive Wall (`47`, Spartans), Barricade (`50`, Vikings).<br>**Civic/Expansion (`15, 17, 18, 23..28, 34, 35, 40, 44`)**: Main Building (`15`), Marketplace (`17`), Embassy (`18`), Cranny (`23`), Town Hall (`24`), Residence (`25`), Palace (`26`), Treasury (`27`), Trade Office (`28`), Stonemason's Lodge (`34`), **Brewery (`35`, Teutons)**, World Wonder (`40` on `Slot 99`), **Command Center (`44`, Huns — 3 expansion slots)**. |
| **Build Queue & Master Builder** | `IConstructionCalculator`, `VillageConstructionService`, `VillageQueueItems` | Roman simultaneous resource field (`Slot 1..18`) + inner building (`Slot 19..40, 99`) construction; Plus Subscription extra waiting-loop slot (`IsWaitingLoop = true`); Master Builder reserved slots (`IsMasterBuilder = true`, deducts Gold + resources upon slot activation); Main Building (`15` >= 10) timed 1-level demolition (`floor(buildTime / 2)`) + 10-Gold instant complete demolition to Level 0 with Alliance Founder Embassy (`18`) safeguard. |

### 2. Economy, Villages, Oases & Starvation

| Domain Area | Terravia Domain Modules & Contracts | Domain Rules, Formulas & Mechanics Specification |
| :--- | :--- | :--- |
| **Village Resource Engine** | `ResourceProductionCalculator`, `VillageActor` | 12 village field topologies (`Slot 1..18` distributions, including `9c` `FieldTopologyId = 1` and `15c` `FieldTopologyId = 6`). Production combines base field output + bonus buildings (`5%`–`25%` each) + annexed Oasis bonuses (`25%`/`50%` scaled by Hero's Mansion and Egyptian Waterworks `45`) + Hero resource production + Plus `+25%` resource bonuses, minus troop crop upkeep (reduced by Roman Horse Drinking Trough `41` at Levels `10/15/20` and Diet Artifacts). |
| **Oases & Annexation** | `OasisActor`, `OasisAnnexationService` | Unoccupied oases spawn/regenerate Nature troops (`u31..u40`); players can raid oases, capture animals using Hero Cages, or annex up to 3 oases per village via Hero's Mansion (`37` at levels `10, 15, 20`) within a `7x7` radius when oasis loyalty reaches `0%`. |
| **Starvation System** | `StarvationCalculator`, `VillageActor` | When village `Crop < 0` and net crop production is negative, troops starve one by one (`Crop` refilled by the starved unit's crop training cost) in priority order: foreign reinforcements first, then home troops from the largest stack. |
| **Celebrations & Mead Festival** | `CelebrationService`, `VillageActor` | Town Hall (`24`) Small Celebration (up to `500` CP) and Great Celebration (up to `2000` CP + `+5%` chief loyalty reduction bonus in attacks, `-5%` enemy chief effectiveness). Teuton Brewery (`35`) hosts 72-hour **Mead Festival** (`+1%` attack power per Brewery level, while enemy/own chiefs have halved persuasion and catapults aim at random targets). |

### 3. Combat, Movements, Traps, Hospital & Conquest

| Domain Area | Terravia Domain Modules & Contracts | Domain Rules, Formulas & Mechanics Specification |
| :--- | :--- | :--- |
| **Combat Resolution & War Simulator** | `IBattleSimulator`, `BattleEngine`, `MovementSchedulerActor` | **Attack Types**: `1` Scout/Spy (Resources & Troops or Defenses & Wall), `2` Reinforcement, `3` Normal Attack, `4` Raid, `5` Settler Founding.<br>**Combat Math**: Calculates Infantry vs. Cavalry attack ratios, applies Blacksmith/Armoury upgrades, Wall defense multiplier & flat defense bonus, Residence/Palace/Command Center defense, Morale bonus (population ratio), Brewery bonus, Hero off/def % bonus + Hero Item bonuses, and Alliance Metallurgy bonus.<br>**Casualty Curve**: Deterministic $\sigma(x)$ formula (`pow(x, 1.5)` for $x \le 1$, `2 - pow(x, -1.5)` for $x > 1$) with Immensity exponent reduction for large battles (`> 1000` units). |
| **Rams, Catapults & Chiefs/Senators** | `SiegeAndConquestCalculator`, `BattleEngine` | **Rams**: Early-phase wall reduction + final wall demolition.<br>**Catapults**: Single or dual target (`PrimarySiegeTarget`, `SecondarySiegeTarget` at Rally Point Lvl 20 with `>= 20` catapults); modified by Stonemason's Lodge (`34`), Architect Artifacts (`4x/3x/5x`), and Confusion Artifacts / Brewery random targeting.<br>**Conquest (Loyalty)**: Senators/Chiefs/Chieftains/Tribal Chiefs/Nomarchs/Ephors/Jarls reduce `Village.Loyalty` when defender has no standing Residence/Palace/Command Center and attacker has sufficient Culture Points; captures village at `0%` loyalty. |
| **Gaul Trapper & Prisoners** | `TrapperService`, `VillageTroopStacks` (`StackType.Trapped`) | Trapper (`36`, up to `400` traps `u99` per Level 20 building) captures attacking troops before combat. Normal attacks that win release trapped troops (`25%` trap repair / casualties); village owner or troop owner can release/dissolve prisoners. |
| **Hospital & Healing Queue** | `HospitalHealingService`, `VillageTroopStacks` (`StackType.Hospital`), `VillageQueueItems` (`QueueCategory.Healing`) | Hospital (`46`) and Big Hospital (`48`, Spartans/Vikings) save a percentage of battle casualties as wounded troops, which can be healed back into active service via the `Healing` queue at reduced cost and time. |
| **Troop Evasion & Farm Lists** | `EvasionAndFarmListService`, `FarmLists` | Capital troop auto-evasion when attacked (troops walk out for `180s` at `EvasionSpeed` and return); Gold Club **Farm Lists** (`FarmLists` + `FarmListTargets` tables) for batch raid dispatching. |

### 4. Hero System (Items, Adventures & Silver Auction House)

| Domain Area | Terravia Domain Modules & Contracts | Domain Rules, Formulas & Mechanics Specification |
| :--- | :--- | :--- |
| **Hero Stats & Progression** | `HeroProgressionService`, `HeroUnitTemplate` (`hero-templates.json`), `PlayerActor` | Hero attributes: Fighting Strength (`Attack`), Off-Bonus (`OffenseBonusPoints`, `0.2%`/pt), Def-Bonus (`DefenseBonusPoints`, `0.2%`/pt), Regeneration (`RegenPoints`), and **Resource Production (`ResourcePoints`, balanced all-four or single-resource mode)**. Revival in any village with Hero's Mansion. |
| **Equippable Items & Consumables** | `HeroEquipmentCatalog` (`hero-items.json`), `PlayerActor` | **Slots**: Helmet (XP / CP / Cavalry / Infantry speed), Right Hand (Tribe-specific unit weapons, Tier 1–3), Left Hand (Shields, Maps for return speed, Pennants/Standards for troop speed between own/allied villages, Thief Pouches for cranny plundering, Archon Horns), Body Armor (Damage reduction / Regen / Fighting strength), Boots (Speed after `> 20` tiles / Regen), Horse (Base speed `14/17/20`), Consumables (Ointments, Scrolls, Buckets of Water, Tablets of Law, Artworks, Bandages `25%/33%`, Cages). |
| **Hero Adventures** | `HeroAdventureGenerator`, `PlayerActor` | Periodic adventure spawning around player villages (`Normal` vs. `Hard` difficulty), expiration timers, hero travel movement, damage/XP calculation, and loot rolls (Equipment, Consumables, Resources, Silver, Stray Troops). |
| **Silver Auction House** | `MarketAndAuctionActor`, `HeroAuctions` | Player-to-player item auctions using **Silver** currency with proxy/max-bid auto-bidding (`MaxBid`), bid escrow, anti-sniping extensions, and Gold-to-Silver / Silver-to-Gold exchange. |

### 5. Marketplace, Alliances, Alliance Bonuses & Social

| Domain Area | Terravia Domain Modules & Contracts | Domain Rules, Formulas & Mechanics Specification |
| :--- | :--- | :--- |
| **Marketplace, Trade Routes & NPC** | `MarketAndAuctionActor`, `MarketOffers`, `TradeRoutes` | Direct resource sending, Open Market Offers (with alliance-only filter and max travel time), Plus **Trade Routes** (for recurring scheduled deliveries), and 3-Gold **NPC Merchant** instant `1:1` resource redistribution. |
| **Alliances, Diplomacy & Forums** | `AllianceActor`, `AllianceDiplomacy`, `AllianceForums` | Embassy capacity (`3` members per Embassy level up to `60`); granular member permissions (assign ranks, kick, change description, diplomacy, invite, forum mod, mass IGM); Confederacy / NAP / War diplomacy; internal/public/allied Forums with Polls. |
| **Alliance Bonuses** | `AllianceBonusService`, `AllianceActor` | 4 Alliance-wide upgradable bonuses (Levels 1–5) unlocked via daily member resource donations:<br>1. **Recruitment** (faster troop training)<br>2. **Philosophy** (higher Culture Point output)<br>3. **Metallurgy** (higher troop offense & defense)<br>4. **Commerce** (higher merchant capacity) |
| **Messages, Reports & Global Chat** | `MessagingAndChatService`, `SignalRBridgeActor` | Private messages with BBCode, archive, notes, and friend list; categorized Battle/Trade/Reinforcement/Scout Reports with attack filters; Alliance Chat + **Global Server Chat** featuring live Polls, 1-click Battle Report sharing, and Moderator Mutes. |

### 6. Endgame (Archons, Artifacts, World Wonders), Quests & Admin

| Domain Area | Terravia Domain Modules & Contracts | Domain Rules, Formulas & Mechanics Specification |
| :--- | :--- | :--- |
| **Artifacts & World Wonders** | `ArchonDirectorActor`, `ArtifactAndWonderService` | Scheduled spawning of Archon Capital, World Wonder villages, and Artifacts (Small = village scope, Large = account scope, Unique = strongest account scope; Treasury Lvl 10 for Small, Lvl 20 for Large/Unique, 24h activation delay): **Architect's Secret, Boots of Titan, Hawk's Eyes, Diet Control, Academic Advancement, Great Storage Blueprint, Rivals' Confusion, Artifact of the Fool (randomizes every 24h)**, plus **13 WW Building Plans** (`1` plan required for WW Lvl 1–49, `2nd` plan in alliance required for Lvl 50–100). Automated Archon attacks on WW levels (`5, 10, ..., 95, 96, 97, 98, 99`) and automatic Archon WW construction up to Lvl 100. |
| **Quests, Rankings, Medals & Milestones** | `QuestAndRankingService`, `WorldCoordinatorActor` | Taskmaster tutorial quests (`Standard` & `Extended` variants, configurable in `QuestConfig`); Top-10 weekly medals (Climbers, Attackers, Defenders, Robbers + Combined) for players and alliances; **Special Medals** (Artifact Owner, WW Owner, WW Winner, Hero Lvl 100, Great Warehouse/Granary, Wall Master); **Server Milestones** (race-condition-free "First player to..."); **Graphical Statistics** (`PlayerStatisticsHistory`). |
| **Account Protection, Vacation & Sitters** | `AccountSecurityService`, `PlayerActor` | Beginner's Protection timer; **Vacation Mode** (validates 9 conditions: no outgoing/incoming troops, no active queues, no WW/Artifacts, etc.); **2 Sitters** per account with granular bitmask permissions (`Sitter1PermMask`, `Sitter2PermMask`: raid, attack, reinforce, send resources, buy/spend Gold, chat/IGM); 72-hour account deletion countdown. |
| **Gold Shop & Admin Security Suite** | `AntiCheatAndModerationService`, `GameAuditLogs`, `MultiAccountSessions` | Gold packages & Promo/Voucher codes (`GoldPromoCodes`), Gold financial audit ledger (`GameAuditLogs`), Multi-Account Detection (`MultiAccountSessions`), 7-day Push Protection (`ResourceTransferLogs`, `PushOverrides`), Registration Blocklist (`RegistrationBlocks`), IP & Account Ban Manager (`AccountBans`, `IpBans`), World Heatmap, Maintenance Mode (`ServerConfig.IsMaintenanceMode`), and OpenTelemetry structured diagnostics. |

---

## Part III: 4-Phase Implementation Roadmap

| Phase | Milestone Focus | Key Deliverables |
| :--- | :--- | :--- |
| **Phase 1: Core Engine, 3 Classic Tribes & Utility AI Bots** | Playable Vertical Slice | • Solution scaffolding (`server/src/Terravia.Domain`, `Actors`, `Infrastructure`, `BotAI`, `WebAPI` + `client/apps/game-ui`, `admin-ui`).<br>• SQL Server schema (EF Core migrations + Dapper TVP batch flush).<br>• `WorldCoordinatorActor`, `VillageActor`, `PlayerActor`, `MovementSchedulerActor`.<br>• Romans, Teutons, Gauls (`u1..u30`), core buildings (`1..41`), Resource/Build/Training/Research queues, Battle Engine & Conquest, Marketplace.<br>• `BotPlayerActor` + Utility AI Personalities (Raider, Builder, Defender).<br>• SvelteKit + Phaser v4 Full-Canvas `game-ui` (`UIScene`, `ResourceFieldsScene`, `VillageCenterScene`, `WorldMapScene`, Rally Point, Reports). |
| **Phase 2: All 7 Playable Tribes, Hero & Alliance Systems** | Full Mid-Game Depth | • Huns, Egyptians, Spartans, Vikings (`u51..u90`) + additional buildings (`42..50`: Command Center, Waterworks, Hospital & Healing Queue, Great Workshop, faction Walls, Brewery Mead Festival).<br>• Hero Equipment, Consumables, Adventures, and Silver Auction House.<br>• Alliances, Diplomacy, Forums, and Alliance Bonuses (Recruitment, Philosophy, Metallurgy, Commerce). |
| **Phase 3: Endgame (Archons, Artifacts, World Wonders) & Social** | Full Round Lifecycle | • `ArchonDirectorActor`: Automated Artifact spawning (all 8 types + WW Building Plans), Archon WW construction, and Archon retaliatory waves.<br>• Global Server Chat with Polls & Report Sharing.<br>• Weekly Top-10 Medals, Special Medals, Server Milestones, Graphical Statistics, and Taskmaster Quests. |
| **Phase 4: Live-Ops AdminUI, Anti-Cheat & Cluster Readiness** | Production Hardening | • Complete `admin-ui`: Multi-Account Detection (MAD) graph, Push-Protection dashboard, World Heatmap, Quest Editor, Gold Promo codes, IP/Account Bans.<br>• Utility AI Bot Director dashboard in `admin-ui` + External SignalR/HTTP Load-Test Bot Runner.<br>• Optional `Akka.Cluster.Sharding` provider toggle for multi-node horizontal scaling. |
