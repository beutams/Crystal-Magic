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
            List<TilemapRenderer> obstacleRenderers = BuildObstacleGroups(
                runtimeRoot,
                grid.transform,
                terrainVisual,
                resourceOwnerKey);
            if (obstacleRenderers.Count > 0)
            {
                DungeonTilemapVisibilityController visibilityController =
                    gridObject.AddComponent<DungeonTilemapVisibilityController>();
                visibilityController.Initialize(obstacleRenderers);
            }
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

        private static List<TilemapRenderer> BuildObstacleGroups(
            DungeonSceneRuntimeRoot runtimeRoot,
            Transform gridParent,
            RuntimeDungeonTerrainVisualData terrainVisual,
            string resourceOwnerKey)
        {
            List<ObstacleRenderGroup> groups = GetObstacleRenderGroups(terrainVisual.Placements);
            if (groups.Count == 0)
                return new List<TilemapRenderer>();

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
            List<TilemapRenderer> renderers = new(groups.Count);
            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                ObstacleRenderGroup group = groups[groupIndex];
                float anchorWorldY = gridParent.TransformPoint(
                    new Vector3(0f, group.LowestCellY * cellWorldSize, 0f)).y;
                int sortingOrder = Mathf.RoundToInt(-anchorWorldY * WorldSortingPrecision);
                Tilemap tilemap = CreateRuntimeTilemap(
                    gridParent,
                    $"ObstacleMountain_{group.MountainIndex}",
                    sortingOrder);

                for (int placementIndex = 0; placementIndex < group.Placements.Count; placementIndex++)
                {
                    RuntimeDungeonRuleTilePlacement placement = group.Placements[placementIndex];
                    if (!resolvedByCell.TryGetValue(placement.Cell, out ResolvedDungeonTileSprite resolved))
                        continue;

                    Tile runtimeTile = GetOrCreateResolvedObstacleTile(
                        runtimeRoot,
                        tileAssets,
                        resolved);
                    tilemap.SetTile(new Vector3Int(placement.Cell.x, placement.Cell.y, 0), runtimeTile);
                }

                tilemap.RefreshAllTiles();
                renderers.Add(tilemap.GetComponent<TilemapRenderer>());
            }

            return renderers;
        }

        private static List<ObstacleRenderGroup> GetObstacleRenderGroups(
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

            Dictionary<Vector2Int, int> mountainByCell = GetObstacleMountainIndices(finalTiles);
            Dictionary<int, ObstacleRenderGroup> groupsByMountain = new();
            foreach (RuntimeDungeonRuleTilePlacement placement in finalTiles.Values)
            {
                int mountainIndex = mountainByCell[placement.Cell];
                if (!groupsByMountain.TryGetValue(mountainIndex, out ObstacleRenderGroup group))
                {
                    group = new ObstacleRenderGroup(mountainIndex);
                    groupsByMountain.Add(mountainIndex, group);
                }

                group.Add(placement);
            }

            List<ObstacleRenderGroup> groups = new(groupsByMountain.Values);
            groups.Sort((left, right) => left.MountainIndex.CompareTo(right.MountainIndex));
            return groups;
        }

        private static Dictionary<Vector2Int, int> GetObstacleMountainIndices(
            IReadOnlyDictionary<Vector2Int, RuntimeDungeonRuleTilePlacement> finalTiles)
        {
            List<Vector2Int> cells = new(finalTiles.Keys);
            cells.Sort((left, right) =>
            {
                int xComparison = left.x.CompareTo(right.x);
                return xComparison != 0 ? xComparison : left.y.CompareTo(right.y);
            });

            Dictionary<Vector2Int, int> mountainByCell = new();
            Queue<Vector2Int> pendingCells = new();
            int mountainIndex = 0;
            for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
            {
                Vector2Int startCell = cells[cellIndex];
                if (mountainByCell.ContainsKey(startCell))
                    continue;

                mountainByCell.Add(startCell, mountainIndex);
                pendingCells.Enqueue(startCell);
                while (pendingCells.Count > 0)
                {
                    Vector2Int cell = pendingCells.Dequeue();
                    AddConnectedObstacleCell(finalTiles, mountainByCell, pendingCells, cell + Vector2Int.up, mountainIndex);
                    AddConnectedObstacleCell(finalTiles, mountainByCell, pendingCells, cell + Vector2Int.right, mountainIndex);
                    AddConnectedObstacleCell(finalTiles, mountainByCell, pendingCells, cell + Vector2Int.down, mountainIndex);
                    AddConnectedObstacleCell(finalTiles, mountainByCell, pendingCells, cell + Vector2Int.left, mountainIndex);
                }

                mountainIndex++;
            }

            return mountainByCell;
        }

        private static void AddConnectedObstacleCell(
            IReadOnlyDictionary<Vector2Int, RuntimeDungeonRuleTilePlacement> finalTiles,
            IDictionary<Vector2Int, int> mountainByCell,
            Queue<Vector2Int> pendingCells,
            Vector2Int cell,
            int mountainIndex)
        {
            if (!finalTiles.ContainsKey(cell) || mountainByCell.ContainsKey(cell))
                return;

            mountainByCell.Add(cell, mountainIndex);
            pendingCells.Enqueue(cell);
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

        private sealed class ObstacleRenderGroup
        {
            public ObstacleRenderGroup(int mountainIndex)
            {
                MountainIndex = mountainIndex;
                LowestCellY = int.MaxValue;
            }

            public int MountainIndex { get; }
            public int LowestCellY { get; private set; }
            public List<RuntimeDungeonRuleTilePlacement> Placements { get; } = new();

            public void Add(RuntimeDungeonRuleTilePlacement placement)
            {
                Placements.Add(placement);
                LowestCellY = Mathf.Min(LowestCellY, placement.Cell.y);
            }
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

    /// <summary>
    /// Keeps static obstacle Tilemaps outside the gameplay camera frustum disabled so URP does not
    /// schedule one Tilemap culling job for every sorting group on every frame.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    internal sealed class DungeonTilemapVisibilityController : MonoBehaviour
    {
        private const float BoundsPadding = 1f;

        private readonly Plane[] _frustumPlanes = new Plane[6];
        private VisibilityEntry[] _entries = Array.Empty<VisibilityEntry>();
        private Camera _lastCamera;
        private Matrix4x4 _lastViewProjection;
        private bool _hasLastViewProjection;

        public void Initialize(IReadOnlyList<TilemapRenderer> renderers)
        {
            if (renderers == null || renderers.Count == 0)
            {
                _entries = Array.Empty<VisibilityEntry>();
                return;
            }

            _entries = new VisibilityEntry[renderers.Count];
            for (int index = 0; index < renderers.Count; index++)
            {
                TilemapRenderer renderer = renderers[index];
                Bounds bounds = renderer != null ? renderer.bounds : default;
                bounds.Expand(new Vector3(BoundsPadding * 2f, BoundsPadding * 2f, 2f));
                _entries[index] = new VisibilityEntry(renderer, bounds);
            }

            _hasLastViewProjection = false;
        }

        private void LateUpdate()
        {
            if (_entries.Length == 0)
                return;

            Camera camera = CameraComponent.Instance != null ? CameraComponent.Instance.Current : Camera.main;
            if (camera == null)
            {
                SetAllVisible();
                _hasLastViewProjection = false;
                return;
            }

            Matrix4x4 viewProjection = camera.projectionMatrix * camera.worldToCameraMatrix;
            if (_hasLastViewProjection && camera == _lastCamera && viewProjection == _lastViewProjection)
                return;

            GeometryUtility.CalculateFrustumPlanes(viewProjection, _frustumPlanes);
            for (int index = 0; index < _entries.Length; index++)
            {
                TilemapRenderer renderer = _entries[index].Renderer;
                if (renderer == null)
                    continue;

                bool visible = GeometryUtility.TestPlanesAABB(_frustumPlanes, _entries[index].Bounds);
                if (renderer.enabled != visible)
                    renderer.enabled = visible;
            }

            _lastCamera = camera;
            _lastViewProjection = viewProjection;
            _hasLastViewProjection = true;
        }

        private void OnDisable()
        {
            SetAllVisible();
            _hasLastViewProjection = false;
        }

        private void SetAllVisible()
        {
            for (int index = 0; index < _entries.Length; index++)
            {
                TilemapRenderer renderer = _entries[index].Renderer;
                if (renderer != null)
                    renderer.enabled = true;
            }
        }

        private readonly struct VisibilityEntry
        {
            public VisibilityEntry(TilemapRenderer renderer, Bounds bounds)
            {
                Renderer = renderer;
                Bounds = bounds;
            }

            public TilemapRenderer Renderer { get; }
            public Bounds Bounds { get; }
        }
    }
}
