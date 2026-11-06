# SignalR & Shared Client-Server Economy & Expansion Contracts (`Terravia.WebAPI` & `client/packages/game-contracts`)

**Feature**: `002-economy-village-engine`  
**Date**: 2026-11-06  
**Spec**: [spec.md](../spec.md) | **Domain Contracts**: [domain-services.md](./domain-services.md)

## 1. SignalR Hub Commands (`GameHub` Inbound)

Clients (`game-ui`) invoke the following hub methods on `GameHub`, which authenticate the caller (and validate `TerraviaPermissions` for Sitters) before routing the corresponding `*Command` through `IGameCommandGateway`:

| Hub Method | Request Payload (`*Input`) | Routed Domain Command |
| :--- | :--- | :--- |
| `StartCelebration(StartCelebrationInput)` | `{ villageId: number, celebrationType: "Small" \| "Great" }` | `StartTownHallCelebrationCommand` |
| `StartMeadFestival(StartMeadFestivalInput)` | `{ villageId: number }` | `StartBreweryMeadFestivalCommand` |
| `ChangeCapitalVillage(ChangeCapitalInput)` | `{ newCapitalVillageId: number }` | `ChangeCapitalVillageCommand` |
| `AbandonOasis(AbandonOasisInput)` | `{ villageId: number, oasisTileId: number }` | `AbandonAnnexedOasisCommand` |

---

## 2. SignalR Real-Time Broadcast Contracts (`GameHub` Outbound `*Dto`)

Per **Naming & Structural Conventions**, all outbound read projections and SignalR payloads are suffixed with **`Dto`**, while cross-actor / persistence events are suffixed with **`Eto`**.

```typescript
export interface VillageEconomySnapshotDto {
  villageId: number;
  tileId: number;
  name: string;
  fieldTopologyId: number; // 1..12
  isCapital: boolean;
  isArchon: boolean;
  population: number;
  culturePointsPerDay: number; // 0 if isArchon == true
  loyalty: number; // 0..100
  loyaltyRegenPerHour: number; // L_ResidenceOrPalaceOrCC * Speed
  lastUpdatedServerMs: number;

  stocks: {
    wood: number;
    clay: number;
    iron: number;
    crop: number;
    warehouseCap: number;
    granaryCap: number;
  };

  production: {
    grossWoodPerHour: number;
    grossClayPerHour: number;
    grossIronPerHour: number;
    grossCropPerHour: number;
    effectivePopulationUpkeep: number;
    totalTroopUpkeepPerHour: number;
    netCropPerHour: number;
  };

  annexedOases: Array<{
    oasisTileId: number;
    x: number;
    y: number;
    oasisTypeId: number; // 1..12
    loyalty: number;
    woodBonusPercent: number;
    clayBonusPercent: number;
    ironBonusPercent: number;
    cropBonusPercent: number;
  }>;

  celebration: {
    activeCelebrationType: "None" | "Small" | "Great";
    celebrationEndsAtMs: number;
    festivalEndsAtMs: number;
  };

  expansion: {
    unlockedSlots: number; // 0..3
    expansionSlotVillageIds: [number | null, number | null, number | null];
    remainingTrainableSettlerEquivalents: number; // 0..9 (1 Admin = 3, 1 Settler = 1)
  };
}

export interface PlayerCultureSnapshotDto {
  playerId: number;
  accumulatedCulturePoints: number;
  dailyCulturePointsRate: number; // Sum of non-Archon village CP * AlliancePhilosophy + HeroHelmet
  lastCpUpdatedServerMs: number;
  ownedVillageCount: number;
  nextVillageRequiredCp: number; // Strictly monotonic Fast or Standard curve
  capitalVillageId: number;
}
```

---

## 3. Shared 60fps Client-Side Resource & Loyalty Interpolation (`client/packages/game-contracts`)

Per **Constitutional Principle III**, the client renders smooth 60fps resource bars in `TopResourceHud` (`game-ui`) without polling the server every second by running the identical deterministic clamping formula locally inside `client/packages/game-contracts/src/economy.interpolation.ts`:

```typescript
export interface InterpolatedVillageResources {
  wood: number;
  clay: number;
  iron: number;
  crop: number;
  loyalty: number;
  secondsUntilStorageFullOrStarvation: number | null;
}

export function interpolateVillageEconomy(
  snapshot: VillageEconomySnapshotDto,
  currentServerTimeMs: number
): InterpolatedVillageResources {
  const elapsedHours = Math.max(0, (currentServerTimeMs - snapshot.lastUpdatedServerMs) / 3_600_000);

  const clamp = (val: number, min: number, max: number): number =>
    Math.min(max, Math.max(min, val));

  const wood = clamp(
    snapshot.stocks.wood + snapshot.production.grossWoodPerHour * elapsedHours,
    0,
    snapshot.stocks.warehouseCap
  );
  const clay = clamp(
    snapshot.stocks.clay + snapshot.production.grossClayPerHour * elapsedHours,
    0,
    snapshot.stocks.warehouseCap
  );
  const iron = clamp(
    snapshot.stocks.iron + snapshot.production.grossIronPerHour * elapsedHours,
    0,
    snapshot.stocks.warehouseCap
  );
  const crop = clamp(
    snapshot.stocks.crop + snapshot.production.netCropPerHour * elapsedHours,
    0,
    snapshot.stocks.granaryCap
  );
  const loyalty = clamp(
    snapshot.loyalty + snapshot.loyaltyRegenPerHour * elapsedHours,
    0,
    100
  );

  let secondsUntilStorageFullOrStarvation: number | null = null;
  if (snapshot.production.netCropPerHour < 0) {
    secondsUntilStorageFullOrStarvation = Math.max(
      0,
      Math.ceil((crop / -snapshot.production.netCropPerHour) * 3600)
    );
  } else if (snapshot.production.netCropPerHour > 0 && crop < snapshot.stocks.granaryCap) {
    secondsUntilStorageFullOrStarvation = Math.ceil(
      ((snapshot.stocks.granaryCap - crop) / snapshot.production.netCropPerHour) * 3600
    );
  }

  return {
    wood: Math.floor(wood),
    clay: Math.floor(clay),
    iron: Math.floor(iron),
    crop: Math.floor(crop),
    loyalty: Math.floor(loyalty),
    secondsUntilStorageFullOrStarvation
  };
}
```
