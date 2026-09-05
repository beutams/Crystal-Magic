# Open Field Visual Theme Design

> Status: approved implementation specification. The old visual-theme data is
> deliberately discarded rather than migrated.

## Goal

Move open-field visuals from the MapTest colour-preview implementation into the
formal Open Field generation path. Terrain appearance, ground variants,
decorations, and obstacles must be configured per theme in the game editor;
the map generator chooses positions and tiles from that configuration.

## Height model

| Terrain category | Logical height | Traversal | Visual responsibility |
| --- | ---: | --- | --- |
| Void | `-1` exactly | Blocked | Black abyss, void cliff wall, void transition layer |
| Ground | `0` exactly | Walkable | One selected Ground Style's 15-tile base and its decorations |
| Obstacle | `+1` to `+N` | Blocked | Obstacle top, obstacle wall, obstacle transition layer |

- Void depth is intentionally no longer variable. Every void cell is at `-1`.
- Obstacle height remains stepped, so a taller obstacle may draw more than one
  wall-height segment.
- The MapTest Organic Terraces implementation has already been changed to use
  fixed `-1` void cells.

## Theme visual configuration

The formal theme configuration owns the visual data. MapTest may later use the
same configuration as a preview, but it is not the source of truth. The old
theme rows are intentionally not migrated: `DungeonThemeDataTable.json` starts
as an empty table and new themes are created in the editor after their assets
exist.

### Terrain layers

`VoidVisual`

- Abyss base tile: a pure black map.
- Void cliff-wall tile.
- Void transition tile.

`ObstacleVisual`

- Obstacle-top tile.
- Obstacle-wall tile.
- Obstacle transition tile.

Void base/wall/transition share one `Void Tilemap`; obstacle top/wall/transition
share one `Obstacle Tilemap`. Each source image is a single-image RuleTile, not
a 15- or 16-slot terrain rule set. The generator chooses which of the three
RuleTiles occupies each Tilemap cell, then each Tilemap is baked as one layer.

A Tilemap can contain only one tile at a grid coordinate. Therefore the three
terrain visuals must be assigned to mutually exclusive cells (or use artwork
whose visual extent reaches into neighbouring cells); they cannot be stacked at
the same coordinate before baking.

### Ground Style list

The theme contains a list of `GroundStyle` entries rather than one global
ground tile grid. Each entry has:

- A name.
- One 15-tile base-ground set.
- A list of decoration definitions.
- A list of allowed obstacle definitions.

Generation first partitions zero-height ground into style regions. A cell's
style determines its base tile, its eligible decoration definitions, and its
eligible obstacle definitions.

All 15-tile and 16-tile sets are Unity `RuleTile` assets. Generation only
writes the appropriate RuleTile into cells selected for that terrain or
decoration; Tilemap RuleTile neighbour matching selects the final sprite. The
project must not add a parallel custom 15/16-slot resolver.

### Decoration definitions

Each decoration definition belongs to one Ground Style and provides:

- A Unity `RuleTile` asset, regardless of whether its art has one, 15, 16, or
  another number of internal rule slots.
- `R`: centre spacing / sampling radius.
- Maximum spread count.
- No visual-type switch. The single shared expansion algorithm determines the
  occupied-cell mask from the centre and the maximum spread.

There is no visual-asset type field such as `Point`, `Patch-15`, or
`Strip-16`. The generator only decides where cells exist and how many are
claimed. Its number of candidate centres is derived from the owning style
region's area and the decoration's `R`; a larger `R` makes fewer patches. A
patch can stop immediately when its maximum spread is zero, which naturally
produces a point decoration. It then writes the entry's RuleTile to the
accepted cells; Unity RuleTile neighbour matching chooses the final sprite.

- Decorations render above their base ground.
- Decorations are non-blocking; obstacle entries own collision.
- Generated decoration cells must not cross into a different Ground Style.
- Decorations with a positive maximum spread keep the existing one-pass
  2x2-support clean-up rule, so isolated points or one-cell-wide artefacts are
  not retained. A zero-spread decoration is deliberately exempt: it is the
  configured single-cell point case.

### Obstacle definitions

Each Ground Style chooses from its allowed obstacle list. An obstacle definition
provides at least:

- A source sprite selected by drag-and-drop in the existing editor workflow;
  the saved data is its sprite path, name, and UV information.
- Width and height in grid cells.
- A collision mask with one boolean per footprint cell.
- Spawn weight, spacing, maximum count, rotation/flip permissions, and visual
  sort anchor.

Obstacle generation runs after ground decorations. The visual footprint may use
non-colliding cells, but every footprint cell marked as colliding must be on
ground and must have a one-cell clearance from void and obstacle terrain. In
practice, its eight surrounding map cells must also be ground; this keeps the
collision cell from directly touching those fixed terrain blockers. No
post-generation flood-fill route repair is performed for obstacles.

## Render and collision order

1. Void abyss base.
2. Void transition and void wall.
3. Ground Style base tile.
4. Ground decorations.
5. Obstacle transition and obstacle wall.
6. Obstacle top.
7. Obstacles and actors, with their visual sort position independent of their
   footprint collision mask.

## End-to-end usage flow

### 1. Prepare visual assets in Unity

1. Import and slice every source image with the project's cell size, point
   filtering, and a consistent pixels-per-unit value.
2. Create single-image RuleTiles for void base/wall/transition and obstacle
   top/wall/transition. Create one Unity RuleTile asset for every ground base
   and decoration layer; its internal rule count is unrestricted.
3. Configure every ground-base and decoration RuleTile,
   regardless of its internal tile count. Configure its tiling rules in Unity's
   RuleTile inspector/Tile Palette using the supplied images.
4. Create each obstacle's visual asset (sprite or prefab) and decide its grid
   footprint and per-cell collision mask.

### 2. Configure an Open Field theme

In the game's Open Field theme editor, the designer picks an asset in an object
field for the six terrain-layer tiles, then adds Ground Styles. For every
Ground Style, the designer assigns its base `RuleTile`, decoration entries, and
allowed obstacle entries.

The data stores runtime-loadable asset paths, not Unity editor-only object
references. The object picker writes the selected tile, RuleTile, or sprite's
asset path into the JSON. At runtime, `ResourceComponent` resolves that path
and tracks it under the dungeon scene owner.

### 3. Generate semantic map data

1. The formal Open Field terrain generator creates the terrain field.
2. It writes a height-step map: void is always `-1`, ground is `0`, and
   obstacles are positive steps.
3. It validates walkability, places spawn/exit/interest points and content,
   then retries the seed if the required points are not mutually reachable.

The current formal `OpenFieldDungeonLayout` only distinguishes `Void`,
`Ground`, and `Obstacle`; it must be extended to retain the height-step value
before stepped obstacle walls can be rendered in the actual game.

### 4. Resolve Tilemaps, then bake the terrain

The formal scene builder creates a temporary runtime `Grid` and Tilemaps for:

1. One Void Tilemap containing void base, transition, and wall cells.
2. Ground base and ground-decoration Tilemaps.
3. One Obstacle Tilemap containing obstacle transition, wall, and top cells.

For every generated cell, the builder writes the selected layer RuleTile into
the appropriate Tilemap cells. Unity RuleTile neighbour evaluation chooses the
actual sprite. No custom 15/16 sprite resolver participates in this stage.

The baking stage reads those already-resolved sprites and composites a Back
texture and a Top texture for the runtime mesh renderer. It does not choose
RuleTile variants itself. It applies the established straight-up elevation
projection (`x` remains fixed; positive height shifts upward) while composing:
void is exactly one step down, and an exposed obstacle edge receives one wall
segment for every positive height step. The temporary Grid is then released;
only the two baked layers and dynamic obstacle visuals remain in the play
scene.

### 5. Populate ground and decorations

1. Find four-way connected zero-height ground regions.
2. Place Ground Style seeds and expand them fairly until every ground cell has
   one style.
3. Apply the one-pass no-2x2-support cleanup to remove narrow style spikes.
4. Write each style's base RuleTile into the ground Tilemap.
5. For every decoration entry, derive centre count from its R and the style's
   area; grow a compact occupied-cell patch up to its maximum spread only
   within its owning Ground Style, reserve accepted cells, then write its
   RuleTile into the decoration Tilemap.

### 6. Populate obstacles

1. Choose obstacle candidates from the Ground Style's allowed list.
2. Validate collision-mask cells against the map boundary, already-reserved
   cells, and the one-cell void/obstacle clearance ring.
3. Instantiate its dragged-sprite visual and create collision only for
   footprint cells marked true in its collision mask.
4. Do not run a global connectivity repair pass; obstacle spacing and the local
   terrain-clearance rule keep them out of terrain-constrained passages.

### 7. Build final play scene

1. Generate terrain collision from void and obstacle cells.
2. Add the obstacles' individual collision cells.
3. Spawn player, exits, encounters, and treasure through the existing runtime
   scene/ECS path.
4. Render obstacles and actors with their visual sort anchor; this is separate
   from their collision footprint.

## Required rendering migration

The current formal path uses `OpenFieldDungeonSceneDataBuilder.ResolveTerrainTile`
to choose one sprite from a custom 3x3 grid, and `DungeonTileVisualBuilder`
batches those sprite quads into meshes. That terrain-specific path must be
replaced by the runtime Tilemap layers above. Existing ECS spawning for player,
monsters, exits, treasure, and non-tile environment objects remains in place.

## Required future integration points

- `OpenFieldDungeonVisualData`: replace the current fixed `Void / Ground /
  Obstacle` 3x3 grids with the terrain-layer data and Ground Style list above.
- `DungeonEditorWindow.OpenField`: expose the new lists and their tile/mask
  editing UI.
- `OpenFieldDungeonSceneDataBuilder`: select terrain, ground, decoration, and
  obstacle visuals from the theme data and emit them into runtime scene data.
- Runtime scene renderer: resolve temporary Tilemaps, bake their Unity-resolved
  sprites into the Back/Top mesh layers, and create colliders from each
  obstacle collision mask.
- `OpenFieldMapTestDemo`: optionally consume the same theme data for preview;
  it must not become a parallel source of visual configuration.

## Explicit non-migration

- `DungeonThemeDataTable.json` will be rewritten to an empty `Rows` table.
- The MapTest persistent height-map cache is obsolete and will be removed.
- The old fixed 3 x 3 terrain-grid data, preview window, and custom tile
  resolver are deleted with the migration. Source sprites and RuleTile assets
  are not deleted.
