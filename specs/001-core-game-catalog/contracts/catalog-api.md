# REST & SignalR Catalog Contracts (`Terravia.WebAPI` & `client/packages/game-contracts`)

**Feature**: `001-core-game-catalog`
**Date**: 2026-11-06

## 1. Hash-Cached Game Catalog Endpoint (`GET /api/game/catalog`)

Serves the complete immutable round balance catalog to `client/apps/game-ui` (`BootAndPreloadScene`) and `client/apps/admin-ui`.

- **Method & Path**: `GET /api/game/catalog`
- **Authentication**: Public / Optional JWT (required at boot before or after login)
- **Caching Headers**:
  - `ETag: "<sha256-content-hash>"`
  - `Cache-Control: public, max-age=3600, must-revalidate`
  - Returns `304 Not Modified` when request `If-None-Match` matches `ETag`.

### Response Payload Schema (`GameCatalogDto` in `client/packages/game-contracts`)

```json
{
  "version": "1.0.0",
  "contentHash": "sha256:8f4e2b...",
  "serverSpeed": 1.0,
  "storageMultiplier": 1.0,
  "allowAllTribeRomanQueue": false,
  "greatWorkshopEnabled": true,
  "maxMasterBuilderSlots": 3,
  "tribes": [
    {
      "tribeId": 1,
      "name": "Romans",
      "isPlayable": true,
      "merchantCapacity": 500,
      "merchantSpeed": 16,
      "crannyMultiplier": 1.0,
      "wallBuildingTypeId": 31,
      "hasDualBuildQueue": true,
      "canBuildResidenceOrPalace": true,
      "commandBuildingTypeIds": [25, 26],
      "hospitalBuildingTypeId": 46,
      "unitIds": [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]
    }
  ],
  "units": [
    {
      "unitTypeId": 1,
      "tribeId": 1,
      "tribeUnitSlot": 1,
      "name": "Legionnaire",
      "category": "Infantry",
      "tacticalRoles": ["Infantry"],
      "trainingBuildingTypeId": 19,
      "alternateGreatTrainingBuildingTypeId": 29,
      "offense": 40,
      "defenseInfantry": 35,
      "defenseCavalry": 50,
      "cost": { "wood": 120, "clay": 100, "iron": 150, "crop": 30 },
      "upkeepCropPerHour": 1,
      "speedTilesPerHour": 6,
      "baseTrainingSeconds": 1600,
      "carryCapacity": 50,
      "horseDrinkingTroughThresholdLevel": null,
      "researchPrerequisites": []
    }
  ],
  "heroTemplates": [
    {
      "unitTypeId": 1,
      "baseAttack": 50.0,
      "attackPerPoint": 54.0,
      "baseDefenseInfantry": 60.0,
      "defenseInfantryPerPoint": 49.0,
      "baseDefenseCavalry": 85.0,
      "defenseCavalryPerPoint": 62.5
    }
  ],
  "buildings": [
    {
      "buildingTypeId": 1,
      "name": "Woodcutter",
      "category": "Resources",
      "slotPlacement": "ResourceField1To18",
      "maxLevel": 20,
      "nonCapitalMaxLevel": 10,
      "capitalConstraint": "Any",
      "allowedTribeIds": [],
      "deniedTribeIds": [],
      "minLevelRequirements": [],
      "exactLevelRequirements": [],
      "isUniquePerVillage": false,
      "rebuildAtLevel": null,
      "isAccountWideUnique": false,
      "requiresGreatStorageQualification": false,
      "canFinishInstantlyWithGold": true,
      "levels": [
        {
          "level": 1,
          "cost": { "wood": 40, "clay": 100, "iron": 50, "crop": 60 },
          "population": 2,
          "culturePoints": 1,
          "baseTimeSeconds": 260,
          "attributeValue": 5.0
        }
      ]
    }
  ]
}
```

---

## 2. SignalR Real-Time Village Construction & Demolition Delta (`GameHub`)

When a construction/demolition command is accepted or a timer completes in `VillageActor`, `SignalRBridgeActor` broadcasts `OnVillageConstructionDelta` to subscribed clients (`village:{villageId}` and `player:{playerId}`):

```typescript
export interface VillageConstructionDeltaDto {
  villageId: number;
  serverTimeMs: number;
  resources: {
    wood: number;
    clay: number;
    iron: number;
    crop: number;
    warehouseCap: number;
    granaryCap: number;
    netCropPerHour: number;
  };
  population: number;
  culturePointsPerDay: number;
  updatedSlots: Array<{
    slotId: number;
    buildingTypeId: number;
    level: number;
  }>;
  constructionQueue: Array<{
    queueItemId: number;
    slotId: number;
    buildingTypeId: number;
    targetLevel: number;
    isWaitingLoop: boolean;
    isMasterBuilder: boolean;
    startedAtMs: number;
    finishesAtMs: number;
  }>;
  activeDemolition: {
    slotId: number;
    buildingTypeId: number;
    targetLevel: number;
    finishesAtMs: number;
  } | null;
}
```
