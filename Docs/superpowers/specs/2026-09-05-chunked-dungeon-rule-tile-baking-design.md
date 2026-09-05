# Chunked Dungeon RuleTile Baking Design

## Goal

Render a 200 by 200 generated open-field dungeon without blocking a frame by replacing the single full-map RuleTile bake with independently rendered map chunks.

## Problem

`DungeonRuleTileVisualBuilder.Build` resolves every RuleTile and passes the complete map to `DungeonRuleTileBakeComposer.Compose`. At the current source pixel density, the compositor allocates very large textures and executes all per-pixel blends inside one coroutine update. The outer coroutine cannot yield until the full bake returns, which makes the editor appear frozen.

## Design

The existing RuleTile resolution and `DungeonRuleTileBakeComposer` remain the source of truth for each tile's sprite, height projection, colour, and back/top layer selection. After resolution, `DungeonRuleTileVisualBuilder` groups sprites by a fixed 32 by 32 cell chunk coordinate. It bakes one group at a time with the existing compositor.

Each baked group creates at most two renderer GameObjects: a back renderer and a top renderer. Their existing baked world offsets continue to place the texture correctly relative to `terrainVisual.WorldOrigin`. Renderer names include the chunk coordinate to keep the runtime hierarchy inspectable.

`DungeonRuleTileVisualBuilder.Build` becomes an iterator. It holds the temporary RuleTile resolver grid while it processes chunks, yields once after every chunk, and destroys the resolver grid in `finally` when the iterator ends. `DungeonSceneRuntimeBuilder.BuildCurrentDungeonSceneCoroutine` yields that iterator before spawning obstacle sprites.

## Constraints

- Chunk size is exactly 32 map cells on each axis.
- Preserve native RuleTile pixel density; do not downscale source sprites.
- Preserve back/top layer separation, tile height projection, and renderer sort order.
- Do not introduce persistent generated assets or alter terrain data JSON.
- Do not run Unity or automated tests for this change; use focused static verification only, per user direction.

## Failure Handling

An empty chunk creates no renderer. A missing resource continues to be skipped by existing RuleTile resolution logic. The temporary resolver grid is always destroyed when the iterator completes or is disposed.
