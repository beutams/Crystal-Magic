# Open-Field Obstacle Layer Editor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow each open-field obstacle to combine many draggable sprites across ordered grid layers while retaining its independent collision mask.

**Architecture:** Theme JSON stores primitive data only: an obstacle owns `SpriteLayers`, each layer owns sprite cells by integer grid coordinate. The visual-layout builder transforms those cells using the obstacle's chosen rotation and flip, scene data groups the resulting visual spawns with one collision group, and runtime creates one visual entity per sprite cell with a deterministic layer-depth bias.

**Tech Stack:** Unity Editor IMGUI and `DragAndDrop`, Newtonsoft JSON data tables, Unity ECS runtime scene builder, Entities Graphics sprite meshes.

**Spec:** `Docs/superpowers/specs/2026-09-05-open-field-obstacle-layer-editor-design.md`

## Global Constraints

- Preserve existing Footprint, placement, rotation, flip, spacing and Collision Mask behavior.
- Keep the legacy single `Sprite` field as a data migration fallback only; do not show it in the new editor UI.
- Persist only primitive values and asset path/name references in theme JSON; do not serialize Unity vector structs directly.
- Per the user's request, do not run Unity, dotnet builds, or automated tests; use static source checks only.

---

### Task 1: Define serializable layered-obstacle data

**Files:**
- Modify: `Assets/Scripts/Game/Data/DungeonDefinitionData.cs:97-248`

**Interfaces:**
- Produces `OpenFieldVector2Data` with `ToVector2()` for JSON-safe sorting anchors.
- Produces `OpenFieldObstacleSpriteCellData` (`int X`, `int Y`, `OpenFieldSpriteReferenceData Sprite`).
- Produces `OpenFieldObstacleSpriteLayerData` (`string Name`, `List<OpenFieldObstacleSpriteCellData> Cells`).
- Adds `List<OpenFieldObstacleSpriteLayerData> SpriteLayers` to `OpenFieldObstacleData`.

- [x] **Step 1: Add the JSON-safe vector and sprite-cell types**

```csharp
[Serializable]
public struct OpenFieldVector2Data
{
    public float X;
    public float Y;
    public Vector2 ToVector2() => new(X, Y);
}

[Serializable]
public sealed class OpenFieldObstacleSpriteCellData
{
    public int X;
    public int Y;
    public bool UseObstacleCenter;
    public OpenFieldSpriteReferenceData Sprite = new();
}
```

- [x] **Step 2: Add ordered layers and validation**

```csharp
[Serializable]
public sealed class OpenFieldObstacleSpriteLayerData
{
    public string Name;
    public List<OpenFieldObstacleSpriteCellData> Cells = new();

    public void EnsureValid()
    {
        Name ??= string.Empty;
        Cells ??= new List<OpenFieldObstacleSpriteCellData>();
        // Replace null cells and ensure Sprite is non-null for every cell.
    }
}
```

- [x] **Step 3: Migrate the existing single Sprite only when needed**

```csharp
SpriteLayers ??= new List<OpenFieldObstacleSpriteLayerData>();
if (SpriteLayers.Count == 0 && !string.IsNullOrWhiteSpace(Sprite?.AssetPath))
{
    SpriteLayers.Add(new OpenFieldObstacleSpriteLayerData
    {
        Name = "Layer 1",
        Cells = new List<OpenFieldObstacleSpriteCellData>
        {
            new() { X = 0, Y = 0, UseObstacleCenter = true, Sprite = Sprite },
        },
    });
}
```

Change `VisualSortAnchor` to `OpenFieldVector2Data`; validate every layer without deleting cells outside a temporarily shrunken Footprint.

- [x] **Step 4: Static verification**

Run: `rg -n 'Vector[234] .*;' Assets/Scripts/Game/Data/DungeonDefinitionData.cs`

Expected: no Unity vector field remains in open-field theme data; `OpenFieldSpriteUvData` and `OpenFieldVector2Data` provide conversion methods instead.

### Task 2: Build the layered Sprite grid editor

**Files:**
- Modify: `Assets/Scripts/Game/Data/Editor/DungeonEditorWindow.OpenField.cs:141-263`

**Interfaces:**
- Consumes `OpenFieldObstacleData.SpriteLayers`, `FootprintWidth`, `FootprintHeight`.
- Produces edited layer order and one sprite reference per `(layer, x, y)` slot.

- [x] **Step 1: Replace the one-Sprite field with a Sprite Layers section**

Call `DrawObstacleSpriteLayers(obstacle)` after Footprint editing. Add an `Add Layer` button that appends `new OpenFieldObstacleSpriteLayerData { Name = $"Layer {count + 1}" }`. Each layer header includes name, Up, Down and Delete buttons; list order is back-to-front.

- [x] **Step 2: Draw a drop-capable grid for each layer**

```csharp
private void DrawObstacleSpriteLayerGrid(
    OpenFieldObstacleData obstacle,
    OpenFieldObstacleSpriteLayerData layer)
{
    for (int y = obstacle.FootprintHeight - 1; y >= 0; y--)
    for (int x = 0; x < obstacle.FootprintWidth; x++)
        DrawSpriteDropCell(layer, x, y);
}
```

`DrawSpriteDropCell` uses a fixed `Rect`, shows `AssetPreview.GetAssetPreview` or the Sprite texture, and handles `EventType.DragUpdated` / `EventType.DragPerform`. Accept exactly one dragged `Sprite`; dropping replaces the layer's cell at that coordinate. A clear button removes the matching cell.

- [x] **Step 3: Reuse JSON-safe Sprite assignment**

Extract the existing `DrawSpriteReference` assignment body into `SetSpriteReference(OpenFieldSpriteReferenceData, Sprite)`. Both any retained object field and the drag cell must set `AssetPath`, `SpriteName`, UV values and `HasSpriteUv` consistently.

- [x] **Step 4: Convert sorting-anchor editing**

Display `obstacle.VisualSortAnchor.ToVector2()` with `EditorGUILayout.Vector2Field`, then copy the resulting `x/y` into `OpenFieldVector2Data`. Do not store the Unity `Vector2` in the data model.

- [x] **Step 5: Static verification**

Run: `rg -n 'DrawSpriteReference\("Sprite"|SpriteLayers|DragPerform|VisualSortAnchor' Assets/Scripts/Game/Data/Editor/DungeonEditorWindow.OpenField.cs`

Expected: obstacle UI uses the layer-grid methods, supports drop events, and uses the primitive sorting-anchor carrier.

### Task 3: Transform layered cells in the pure visual layout

**Files:**
- Modify: `Assets/Scripts/Game/OpenField/OpenFieldDungeonVisualLayoutBuilder.cs:70-105, 788-931`

**Interfaces:**
- Produces `OpenFieldObstacleVisualSpritePlacement` containing a Sprite reference, transformed local cell and layer index.
- Extends `OpenFieldObstaclePlacement` with `IReadOnlyList<OpenFieldObstacleVisualSpritePlacement> VisualSprites`.

- [x] **Step 1: Add transformed visual-sprite placement data**

```csharp
public sealed class OpenFieldObstacleVisualSpritePlacement
{
    public OpenFieldSpriteReferenceData Sprite { get; }
    public Vector2Int LocalCell { get; }
    public int LayerIndex { get; }
}
```

Store this list on each `OpenFieldObstaclePlacement`; remove the shortcut property that exposes only `Obstacle.Sprite`.

- [x] **Step 2: Share coordinate transformation with Collision Mask**

Extract the `(x, y)` rotation and flip switch from `TransformObstacleMask` into a helper accepting width, height, turns and flip. Use it for both mask cells and sprite cells so a rotated/flipped combination cannot separate visuals from their colliders.

- [x] **Step 3: Build valid visual cells for every ordered layer**

For each layer index and sprite cell: skip null/empty sprite references and source cells outside the original Footprint, transform its local coordinate, and append the result to `VisualSprites`. Do not add visual cells to the Footprint occupancy set; Footprint occupancy remains defined by the existing width/height mask.

- [x] **Step 4: Static verification**

Run: `rg -n 'VisualSprites|OpenFieldObstacleVisualSpritePlacement|Transform.*Cell' Assets/Scripts/Game/OpenField/OpenFieldDungeonVisualLayoutBuilder.cs`

Expected: both collision cells and visual cells use the same transform helper and each obstacle placement can expose multiple visual Sprite cells.

### Task 4: Carry visual cells through scene data and ECS rendering

**Files:**
- Modify: `Assets/Scripts/Core/Runtime/RuntimeDataComponent.cs:260-272`
- Modify: `Assets/Scripts/Core/Runtime/OpenFieldDungeonSceneDataBuilder.cs:118-140`
- Modify: `Assets/Scripts/Core/Runtime/DungeonSceneRuntimeBuilder.cs:90-155`

**Interfaces:**
- Produces `RuntimeDungeonObstacleVisualSpawnData` with Sprite reference, world position, sort anchor, rotation, flip and layer depth index.
- Changes `RuntimeDungeonObstacleSpawnData` to own `List<RuntimeDungeonObstacleVisualSpawnData> Visuals` plus one `CollisionCells` list.

- [x] **Step 1: Split visual spawn information from the collision group**

```csharp
public sealed class RuntimeDungeonObstacleVisualSpawnData
{
    public string SpritePath;
    public string SpriteName;
    public Vector3 WorldPosition;
    public float SortAnchorWorldY;
    public int RotationQuarterTurns;
    public bool FlippedX;
    public int LayerIndex;
}
```

Keep `CollisionCells` only on `RuntimeDungeonObstacleSpawnData` so an obstacle still creates each collider once.

- [x] **Step 2: Convert every transformed visual cell to world data**

In `AddObstacleSpawns`, create one obstacle group per layout placement. Convert `placement.Origin + visual.LocalCell` with the same cell-to-world helper used by terrain, apply the converted sorting anchor and preserve the placement rotation/flip. Copy the transformed collision cells once.

- [x] **Step 3: Spawn all visual entities before the one collision pass**

Replace the single visual spawn in `SpawnObstacles` with a loop over `obstacle.Visuals`. Apply the sprite material/mesh for each visual and set its z position from its normal sort anchor minus a fixed `LayerIndex` depth epsilon, so later layers are visibly in front without overriding normal character sorting. Leave the existing collision-cell loop outside that visual loop.

- [x] **Step 4: Static verification**

Run: `rg -n 'ObstacleVisualSpawnData|obstacle\.Visuals|CollisionCells' Assets/Scripts/Core/Runtime/RuntimeDataComponent.cs Assets/Scripts/Core/Runtime/OpenFieldDungeonSceneDataBuilder.cs Assets/Scripts/Core/Runtime/DungeonSceneRuntimeBuilder.cs`

Expected: visual generation loops per sprite cell while collision creation loops only once per obstacle group.

### Task 5: Review changed surfaces and commit

**Files:**
- Modify: `Assets/Scripts/Game/Data/DungeonDefinitionData.cs`
- Modify: `Assets/Scripts/Game/Data/Editor/DungeonEditorWindow.OpenField.cs`
- Modify: `Assets/Scripts/Game/OpenField/OpenFieldDungeonVisualLayoutBuilder.cs`
- Modify: `Assets/Scripts/Core/Runtime/RuntimeDataComponent.cs`
- Modify: `Assets/Scripts/Core/Runtime/OpenFieldDungeonSceneDataBuilder.cs`
- Modify: `Assets/Scripts/Core/Runtime/DungeonSceneRuntimeBuilder.cs`

**Interfaces:**
- Consumes all interfaces from Tasks 1-4.
- Produces a JSON-safe, layered obstacle authoring and spawn pipeline.

- [x] **Step 1: Inspect the focused diff and whitespace**

Run: `git diff --check -- <the six files above>`

Expected: no whitespace errors. Do not stage unrelated scene, resource, or user changes.

- [x] **Step 2: Review requirement coverage**

Confirm the diff covers: grid drag/drop, layer ordering, same-cell layering, per-cell collision, old single-Sprite migration, rotation/flip alignment, JSON-safe vectors and one-time collider spawning.

- [x] **Step 3: Commit only the implementation files**

```bash
git add -- Assets/Scripts/Game/Data/DungeonDefinitionData.cs \
  Assets/Scripts/Game/Data/Editor/DungeonEditorWindow.OpenField.cs \
  Assets/Scripts/Game/OpenField/OpenFieldDungeonVisualLayoutBuilder.cs \
  Assets/Scripts/Core/Runtime/RuntimeDataComponent.cs \
  Assets/Scripts/Core/Runtime/OpenFieldDungeonSceneDataBuilder.cs \
  Assets/Scripts/Core/Runtime/DungeonSceneRuntimeBuilder.cs
git commit -m "feat: add layered open field obstacles"
```
