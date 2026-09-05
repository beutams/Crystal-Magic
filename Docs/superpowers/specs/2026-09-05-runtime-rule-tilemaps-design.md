# Runtime RuleTile Tilemaps Design

## Goal

Render generated open-field terrain directly with runtime Tilemaps instead of CPU-compositing RuleTile pixels into textures.

## Design

`DungeonRuleTileVisualBuilder` creates a persistent `__DungeonTerrainTilemaps` Grid under the dungeon runtime root. It creates Void, Ground, Decoration, and Obstacle Tilemaps, loads each configured RuleTile once, assigns it to its generated cell, and refreshes the Tilemaps. The runtime root owns the Grid, so normal scene cleanup destroys all Tilemaps.

Void, Ground, and Decoration render behind all unit sprites at consecutive orders beginning at -32000. Obstacle renders in front at 32000, preserving the prior two-layer visual ordering. TilemapRenderer uses Unity's chunk mode for GPU rendering.

## Constraints

- No `DungeonRuleTileBakeComposer.Compose` call in the runtime dungeon build path.
- No runtime `Texture2D.GetPixels32`, generated texture, or Read/Write texture requirement.
- Preserve existing RuleTile placement data, cell size, world origin, resource-owner tracking, and terrain layer roles.
- Do not run Unity or automated tests; use static checks only, per user direction.
