# Chunked Dungeon RuleTile Baking Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bake generated RuleTile terrain in frame-yielding 32 by 32-cell chunks so a 200 by 200 map does not freeze the Unity editor.

**Architecture:** Keep temporary Tilemap RuleTile resolution and `DungeonRuleTileBakeComposer` unchanged. Partition resolved sprites by cell chunk in `DungeonRuleTileVisualBuilder`, compose each chunk independently, create uniquely named back/top renderers, and yield after each chunk through the scene-build coroutine.

**Tech Stack:** Unity C#, `IEnumerator`, Tilemap/RuleTile, runtime `Texture2D` composition.

**Spec:** `Docs/superpowers/specs/2026-09-05-chunked-dungeon-rule-tile-baking-design.md`

## Global Constraints

- Use exactly 32 by 32 map-cell chunks.
- Keep source Sprite pixels-per-unit and current back/top sorting values unchanged.
- Do not change JSON data or create persistent generated textures.
- Do not run Unity, .NET, or automated tests; perform only static verification.

---

### Task 1: Expose frame-yielding terrain baking

**Files:**
- Modify: `Assets/Scripts/Core/Runtime/DungeonRuleTileVisualBuilder.cs:350-400`
- Modify: `Assets/Scripts/Core/Runtime/DungeonSceneRuntimeBuilder.cs:64-66`

**Interfaces:**
- Consumes `RuntimeDungeonTerrainVisualData`, existing `ResolveSprites`, and `DungeonRuleTileBakeComposer.Compose`.
- Produces `DungeonRuleTileVisualBuilder.BuildCoroutine(...)` for the outer dungeon scene coroutine.

- [x] **Step 1: Change the visual-builder entry point into an iterator**

Replace `public static void Build(...)` with `public static IEnumerator BuildCoroutine(...)`. Keep the null/empty terrain guard as `yield break`, retain the temporary Grid/Tilemap setup, and keep `Destroy(temporaryGridObject)` inside an iterator-compatible `finally` block.

- [x] **Step 2: Yield the visual-builder iterator from scene setup**

Replace the direct call with:

```csharp
yield return DungeonRuleTileVisualBuilder.BuildCoroutine(
    runtimeRoot,
    sceneData.TerrainVisual,
    resourceOwnerKey);
```

This keeps obstacle spawning after all terrain renderers have been created.

- [x] **Step 3: Static verification**

Run: `rg -n 'BuildCoroutine|DungeonRuleTileVisualBuilder\.Build' Assets/Scripts/Core/Runtime`

Expected: the only scene-build call yields `BuildCoroutine`; no direct call to the removed `Build` method remains.

### Task 2: Partition and bake renderer chunks

**Files:**
- Modify: `Assets/Scripts/Core/Runtime/DungeonRuleTileVisualBuilder.cs:345-470`

**Interfaces:**
- Consumes the resolved `List<ResolvedDungeonTileSprite>` from Task 1.
- Produces chunk-local back/top renderer GameObjects via existing `CreateLayerRenderer`.

- [x] **Step 1: Define deterministic 32-cell chunk grouping**

Add `private const int BakeChunkCellSize = 32`. Group every resolved sprite by:

```csharp
new Vector2Int(
    Mathf.FloorToInt(sprite.Cell.x / (float)BakeChunkCellSize),
    Mathf.FloorToInt(sprite.Cell.y / (float)BakeChunkCellSize));
```

Sort chunk keys by `y`, then `x`, before baking so the generated hierarchy and work order are deterministic.

- [x] **Step 2: Compose one chunk and name its renderers**

For each sorted chunk, call the existing `DungeonRuleTileBakeComposer.Compose(chunkSprites, terrainVisual.CellWorldSize)`. Pass renderer names `DungeonTerrainBack_<x>_<y>` and `DungeonTerrainTop_<x>_<y>` to `CreateLayerRenderer`; preserve the current world offset addition, depth, and sorting order arguments.

- [x] **Step 3: Yield once per chunk**

Append `yield return null;` after both possible renderers are created for each chunk. Empty baked back/top layers must simply skip renderer creation.

- [x] **Step 4: Static verification**

Run: `rg -n 'BakeChunkCellSize|Group.*Chunk|DungeonTerrainBack_|yield return null' Assets/Scripts/Core/Runtime/DungeonRuleTileVisualBuilder.cs`

Expected: each chunk is independently composed, creates distinguishable renderers, and yields before the next chunk.

### Task 3: Review and commit

**Files:**
- Modify: `Assets/Scripts/Core/Runtime/DungeonRuleTileVisualBuilder.cs`
- Modify: `Assets/Scripts/Core/Runtime/DungeonSceneRuntimeBuilder.cs`
- Modify: `Docs/superpowers/specs/2026-09-05-chunked-dungeon-rule-tile-baking-design.md`
- Modify: `Docs/superpowers/plans/2026-09-05-chunked-dungeon-rule-tile-baking.md`

**Interfaces:**
- Consumes the iterator and chunk grouping from Tasks 1-2.
- Produces a non-blocking terrain-bake pipeline.

- [x] **Step 1: Inspect whitespace and direct-call removal**

Run: `git diff --check -- Assets/Scripts/Core/Runtime/DungeonRuleTileVisualBuilder.cs Assets/Scripts/Core/Runtime/DungeonSceneRuntimeBuilder.cs`

Expected: no whitespace errors.

- [x] **Step 2: Review requirement coverage**

Confirm chunk size is 32, every chunk is yielded, empty layers create no renderer, temporary Grid destruction remains in `finally`, and obstacle spawning still occurs after terrain baking.

- [x] **Step 3: Commit only the focused files**

```powershell
git add -- Assets/Scripts/Core/Runtime/DungeonRuleTileVisualBuilder.cs Assets/Scripts/Core/Runtime/DungeonSceneRuntimeBuilder.cs Docs/superpowers/specs/2026-09-05-chunked-dungeon-rule-tile-baking-design.md Docs/superpowers/plans/2026-09-05-chunked-dungeon-rule-tile-baking.md
git commit -m "fix: bake dungeon terrain in chunks"
```
