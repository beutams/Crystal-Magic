# Runtime RuleTile Tilemaps Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Replace CPU-baked dungeon terrain textures with four persistent runtime RuleTile Tilemaps.

**Architecture:** Build a Grid under `DungeonSceneRuntimeRoot`, populate its Void, Ground, Decoration, and Obstacle Tilemaps from `RuntimeDungeonTerrainVisualData.Placements`, and rely on `TilemapRenderer.Mode.Chunk` for rendering.

**Tech Stack:** Unity C#, Tilemap, RuleTile.

**Spec:** `Docs/superpowers/specs/2026-09-05-runtime-rule-tilemaps-design.md`

## Global Constraints

- No pixel compositing or Read/Write texture dependency.
- Keep terrain behind units and obstacles in front.
- Do not run Unity or automated tests.

---

### Task 1: Replace baked terrain with runtime Tilemaps

**Files:**
- Modify: `Assets/Scripts/Core/Runtime/DungeonRuleTileVisualBuilder.cs`
- Modify: `Assets/Scripts/Core/Runtime/DungeonSceneRuntimeBuilder.cs`

**Interfaces:**
- Consumes `RuntimeDungeonTerrainVisualData.Placements`, `WorldOrigin`, and `CellWorldSize`.
- Produces a Grid owned by `DungeonSceneRuntimeRoot` with four populated Tilemaps.

- [x] **Step 1: Create the persistent Grid and visible Tilemaps**

`Build` creates `__DungeonTerrainTilemaps` under the runtime root, positions it at `WorldOrigin`, configures its cell size, and creates four Chunk-mode TilemapRenderers with the preserved sorting orders.

- [x] **Step 2: Populate RuleTiles directly**

`PopulateRuleTiles` loads each RuleTile once per asset path, sets its generated cells, and refreshes all Tilemaps.

- [x] **Step 3: Restore the scene builder's direct visual call**

`DungeonSceneRuntimeBuilder` calls `DungeonRuleTileVisualBuilder.Build` then yields once before obstacle spawning.

- [x] **Step 4: Static verification**

`rg` confirms no runtime call to `DungeonRuleTileBakeComposer.Compose`, and `git diff --check` reports no whitespace errors.
