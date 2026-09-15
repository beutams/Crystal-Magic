using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CrystalMagic.Core
{
    /// <summary>
    /// A RuleTile sprite resolved from the complete terrain context before it is
    /// written into a sortable runtime Tilemap.
    /// </summary>
    public sealed class ResolvedDungeonTileSprite
    {
        public Sprite Sprite;
        public RuntimeDungeonTilemapLayer Layer;
        public Vector2Int Cell;
        public Color Color = Color.white;
        public Matrix4x4 Transform = Matrix4x4.identity;

        public ResolvedDungeonTileSprite()
        {
        }

        public ResolvedDungeonTileSprite(
            Sprite sprite,
            RuntimeDungeonTilemapLayer layer,
            Vector2Int cell,
            Color color,
            Matrix4x4 transform)
        {
            Sprite = sprite;
            Layer = layer;
            Cell = cell;
            Color = color;
            Transform = transform;
        }
    }

    internal static class DungeonRuleTileVisualBuilder
    {
        private const string RuntimeGridName = "__DungeonTerrainTilemaps";
        private const int BackSortingOrder = -32000;
        private const float WorldSortingPrecision = 100f;

        public static void Build(
            DungeonSceneRuntimeRoot runtimeRoot,
            RuntimeDungeonTerrainVisualData terrainVisual,
            string resourceOwnerKey)
        {
            if (runtimeRoot == null || terrainVisual?.Placements == null || terrainVisual.Placements.Count == 0)
                return;

            GameObject gridObject = new(RuntimeGridName);
            gridObject.transform.SetParent(runtimeRoot.transform, false);
            gridObject.transform.localPosition = new Vector3(
                terrainVisual.WorldOrigin.x,
                terrainVisual.WorldOrigin.y,
                0f);
            Grid grid = gridObject.AddComponent<Grid>();
            grid.cellSize = Vector3.one * Mathf.Max(0.01f, terrainVisual.CellWorldSize);
            Dictionary<RuntimeDungeonTilemapLayer, Tilemap> tilemaps = CreateRuntimeTilemaps(grid.transform);
            PopulateRuleTiles(terrainVisual.Placements, tilemaps, resourceOwnerKey);
            BuildObstacleColumns(runtimeRoot, grid.transform, terrainVisual, resourceOwnerKey);
        }

        private static Dictionary<RuntimeDungeonTilemapLayer, Tilemap> CreateRuntimeTilemaps(Transform parent)
        {
            return new Dictionary<RuntimeDungeonTilemapLayer, Tilemap>
            {
                { RuntimeDungeonTilemapLayer.Void, CreateRuntimeTilemap(parent, "Void", BackSortingOrder) },
                { RuntimeDungeonTilemapLayer.Ground, CreateRuntimeTilemap(parent, "Ground", BackSortingOrder + 1) },
                { RuntimeDungeonTilemapLayer.Decoration, CreateRuntimeTilemap(parent, "Decoration", BackSortingOrder + 2) },
                { RuntimeDungeonTilemapLayer.Boundary, CreateRuntimeTilemap(parent, "Boundary", BackSortingOrder + 3) },
            };
        }

        private static void BuildObstacleColumns(
            DungeonSceneRuntimeRoot runtimeRoot,
            Transform gridParent,
            RuntimeDungeonTerrainVisualData terrainVisual,
            string resourceOwnerKey)
        {
            List<ObstacleColumn> columns = GetObstacleColumns(terrainVisual.Placements);
            if (columns.Count == 0)
                return;

            Tilemap ruleContext = CreateRuntimeTilemap(gridParent, "__ObstacleRuleContext", 0);
            ruleContext.GetComponent<TilemapRenderer>().enabled = false;
            Dictionary<RuntimeDungeonTilemapLayer, Tilemap> contextTilemaps = new()
            {
                { RuntimeDungeonTilemapLayer.Obstacle, ruleContext },
            };
            List<ResolvedDungeonTileSprite> resolvedTiles = ResolveSprites(
                terrainVisual.Placements,
                contextTilemaps,
                resourceOwnerKey);
            Dictionary<Vector2Int, ResolvedDungeonTileSprite> resolvedByCell = new();
            for (int index = 0; index < resolvedTiles.Count; index++)
            {
                ResolvedDungeonTileSprite resolved = resolvedTiles[index];
                if (resolved != null && resolved.Layer == RuntimeDungeonTilemapLayer.Obstacle)
                    resolvedByCell[resolved.Cell] = resolved;
            }

            float cellWorldSize = Mathf.Max(0.01f, terrainVisual.CellWorldSize);
            Dictionary<ResolvedObstacleTileKey, Tile> tileAssets = new();
            for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                ObstacleColumn column = columns[columnIndex];
                float anchorWorldY = gridParent.TransformPoint(
                    new Vector3(0f, column.LowestCellY * cellWorldSize, 0f)).y;
                int sortingOrder = Mathf.RoundToInt(-anchorWorldY * WorldSortingPrecision);
                Tilemap tilemap = CreateRuntimeTilemap(
                    gridParent,
                    $"ObstacleColumn_x{column.X}_y{column.LowestCellY}",
                    sortingOrder);

                for (int placementIndex = 0; placementIndex < column.Placements.Count; placementIndex++)
                {
                    RuntimeDungeonRuleTilePlacement placement = column.Placements[placementIndex];
                    if (!resolvedByCell.TryGetValue(placement.Cell, out ResolvedDungeonTileSprite resolved))
                        continue;

                    Tile runtimeTile = GetOrCreateResolvedObstacleTile(
                        runtimeRoot,
                        tileAssets,
                        resolved);
                    tilemap.SetTile(new Vector3Int(placement.Cell.x, placement.Cell.y, 0), runtimeTile);
                }

                tilemap.RefreshAllTiles();
            }
        }

        private static List<ObstacleColumn> GetObstacleColumns(
            IReadOnlyList<RuntimeDungeonRuleTilePlacement> placements)
        {
            Dictionary<Vector2Int, RuntimeDungeonRuleTilePlacement> finalTiles = new();
            for (int index = 0; index < placements.Count; index++)
            {
                RuntimeDungeonRuleTilePlacement placement = placements[index];
                if (placement == null || placement.Layer != RuntimeDungeonTilemapLayer.Obstacle ||
                    string.IsNullOrWhiteSpace(placement.RuleTilePath))
                {
                    continue;
                }

                finalTiles[placement.Cell] = placement;
            }

            Dictionary<int, List<RuntimeDungeonRuleTilePlacement>> tilesByX = new();
            foreach (RuntimeDungeonRuleTilePlacement placement in finalTiles.Values)
            {
                if (!tilesByX.TryGetValue(placement.Cell.x, out List<RuntimeDungeonRuleTilePlacement> column))
                {
                    column = new List<RuntimeDungeonRuleTilePlacement>();
                    tilesByX.Add(placement.Cell.x, column);
                }

                column.Add(placement);
            }

            List<int> xs = new(tilesByX.Keys);
            xs.Sort();
            List<ObstacleColumn> columns = new();
            for (int xIndex = 0; xIndex < xs.Count; xIndex++)
            {
                int x = xs[xIndex];
                List<RuntimeDungeonRuleTilePlacement> columnTiles = tilesByX[x];
                columnTiles.Sort((left, right) => left.Cell.y.CompareTo(right.Cell.y));

                ObstacleColumn currentColumn = null;
                int previousY = int.MinValue;
                for (int tileIndex = 0; tileIndex < columnTiles.Count; tileIndex++)
                {
                    RuntimeDungeonRuleTilePlacement placement = columnTiles[tileIndex];
                    if (currentColumn == null || placement.Cell.y != previousY + 1)
                    {
                        currentColumn = new ObstacleColumn(x, placement.Cell.y);
                        columns.Add(currentColumn);
                    }

                    currentColumn.Placements.Add(placement);
                    previousY = placement.Cell.y;
                }
            }

            return columns;
        }

        private static Tile GetOrCreateResolvedObstacleTile(
            DungeonSceneRuntimeRoot runtimeRoot,
            IDictionary<ResolvedObstacleTileKey, Tile> tileAssets,
            ResolvedDungeonTileSprite resolved)
        {
            ResolvedObstacleTileKey key = new(resolved.Sprite, resolved.Color, resolved.Transform);
            if (tileAssets.TryGetValue(key, out Tile runtimeTile))
                return runtimeTile;

            runtimeTile = ScriptableObject.CreateInstance<Tile>();
            runtimeTile.name = "RuntimeResolvedObstacleTile";
            runtimeTile.sprite = resolved.Sprite;
            runtimeTile.color = resolved.Color;
            runtimeTile.transform = resolved.Transform;
            tileAssets.Add(key, runtimeTile);
            runtimeRoot.TrackRuntimeAsset(runtimeTile);
            return runtimeTile;
        }

        private sealed class ObstacleColumn
        {
            public ObstacleColumn(int x, int lowestCellY)
            {
                X = x;
                LowestCellY = lowestCellY;
            }

            public int X { get; }
            public int LowestCellY { get; }
            public List<RuntimeDungeonRuleTilePlacement> Placements { get; } = new();
        }

        private readonly struct ResolvedObstacleTileKey : IEquatable<ResolvedObstacleTileKey>
        {
            public ResolvedObstacleTileKey(Sprite sprite, Color color, Matrix4x4 transform)
            {
                Sprite = sprite;
                Color = color;
                Transform = transform;
            }

            private Sprite Sprite { get; }
            private Color Color { get; }
            private Matrix4x4 Transform { get; }

            public bool Equals(ResolvedObstacleTileKey other)
            {
                return Sprite == other.Sprite &&
                       Color.Equals(other.Color) &&
                       Transform.Equals(other.Transform);
            }

            public override bool Equals(object obj)
            {
                return obj is ResolvedObstacleTileKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Sprite != null ? Sprite.GetInstanceID() : 0;
                    hash = hash * 397 ^ Color.GetHashCode();
                    return hash * 397 ^ Transform.GetHashCode();
                }
            }
        }

        private static Tilemap CreateRuntimeTilemap(
            Transform parent,
            string name,
            int sortingOrder)
        {
            GameObject mapObject = new(name);
            mapObject.transform.SetParent(parent, false);
            Tilemap tilemap = mapObject.AddComponent<Tilemap>();
            TilemapRenderer renderer = mapObject.AddComponent<TilemapRenderer>();
            renderer.mode = TilemapRenderer.Mode.Chunk;
            renderer.sortingOrder = sortingOrder;
            return tilemap;
        }

        private static void PopulateRuleTiles(
            IReadOnlyList<RuntimeDungeonRuleTilePlacement> placements,
            IReadOnlyDictionary<RuntimeDungeonTilemapLayer, Tilemap> tilemaps,
            string resourceOwnerKey)
        {
            ResourceComponent resourceComponent = ResourceComponent.Instance;
            if (resourceComponent == null)
            {
                Debug.LogError("[DungeonRuleTileVisualBuilder] ResourceComponent is unavailable while building RuleTile Tilemaps.");
                return;
            }

            Dictionary<string, RuleTile> ruleTiles = new(StringComparer.Ordinal);
            for (int index = 0; index < placements.Count; index++)
            {
                RuntimeDungeonRuleTilePlacement placement = placements[index];
                if (placement == null || string.IsNullOrWhiteSpace(placement.RuleTilePath) ||
                    !tilemaps.TryGetValue(placement.Layer, out Tilemap tilemap))
                {
                    continue;
                }

                if (!ruleTiles.TryGetValue(placement.RuleTilePath, out RuleTile ruleTile))
                {
                    ruleTile = resourceComponent.Load<RuleTile>(placement.RuleTilePath, resourceOwnerKey);
                    ruleTiles.Add(placement.RuleTilePath, ruleTile);
                }

                if (ruleTile == null)
                {
                    Debug.LogWarning($"[DungeonRuleTileVisualBuilder] Failed to load RuleTile: {placement.RuleTilePath}");
                    continue;
                }

                tilemap.SetTile(new Vector3Int(placement.Cell.x, placement.Cell.y, 0), ruleTile);
            }

            foreach (Tilemap tilemap in tilemaps.Values)
                tilemap.RefreshAllTiles();
        }

        private static List<ResolvedDungeonTileSprite> ResolveSprites(
            IReadOnlyList<RuntimeDungeonRuleTilePlacement> placements,
            IReadOnlyDictionary<RuntimeDungeonTilemapLayer, Tilemap> tilemaps,
            string resourceOwnerKey)
        {
            List<ResolvedDungeonTileSprite> resolvedSprites = new();
            ResourceComponent resourceComponent = ResourceComponent.Instance;
            if (resourceComponent == null)
            {
                Debug.LogError("[DungeonRuleTileVisualBuilder] ResourceComponent is unavailable while resolving RuleTiles.");
                return resolvedSprites;
            }

            Dictionary<string, RuleTile> ruleTiles = new(StringComparer.Ordinal);
            for (int i = 0; i < placements.Count; i++)
            {
                RuntimeDungeonRuleTilePlacement placement = placements[i];
                if (placement == null || string.IsNullOrWhiteSpace(placement.RuleTilePath) ||
                    !tilemaps.TryGetValue(placement.Layer, out Tilemap tilemap))
                {
                    continue;
                }

                if (!ruleTiles.TryGetValue(placement.RuleTilePath, out RuleTile ruleTile))
                {
                    ruleTile = resourceComponent.Load<RuleTile>(placement.RuleTilePath, resourceOwnerKey);
                    ruleTiles.Add(placement.RuleTilePath, ruleTile);
                }

                if (ruleTile == null)
                {
                    Debug.LogWarning($"[DungeonRuleTileVisualBuilder] Failed to load RuleTile: {placement.RuleTilePath}");
                    continue;
                }

                tilemap.SetTile(new Vector3Int(placement.Cell.x, placement.Cell.y, 0), ruleTile);
            }

            foreach (Tilemap tilemap in tilemaps.Values)
                tilemap.RefreshAllTiles();

            for (int i = 0; i < placements.Count; i++)
            {
                RuntimeDungeonRuleTilePlacement placement = placements[i];
                if (placement == null || string.IsNullOrWhiteSpace(placement.RuleTilePath) ||
                    !tilemaps.TryGetValue(placement.Layer, out Tilemap tilemap) ||
                    !ruleTiles.ContainsKey(placement.RuleTilePath))
                {
                    continue;
                }

                Vector3Int cell = new(placement.Cell.x, placement.Cell.y, 0);
                Sprite sprite = tilemap.GetSprite(cell);
                if (sprite == null)
                {
                    Debug.LogWarning($"[DungeonRuleTileVisualBuilder] RuleTile has no resolved Sprite at {placement.Cell}: {placement.RuleTilePath}");
                    continue;
                }

                resolvedSprites.Add(new ResolvedDungeonTileSprite(
                    sprite,
                    placement.Layer,
                    placement.Cell,
                    tilemap.GetColor(cell),
                    tilemap.GetTransformMatrix(cell)));
            }

            return resolvedSprites;
        }

    }
}
