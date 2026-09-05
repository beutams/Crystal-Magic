using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace CrystalMagic.Core
{
    /// <summary>
    /// A RuleTile sprite resolved at a generated map cell. The compositor remains independent
    /// from ResourceComponent and Tilemap so it can also be used by edit-mode tests.
    /// </summary>
    public sealed class ResolvedDungeonTileSprite
    {
        public Sprite Sprite;
        public RuntimeDungeonTilemapLayer Layer;
        public RuntimeDungeonTilemapRole Role;
        public Vector2Int Cell;
        public int HeightSteps;
        public Color Color = Color.white;

        public ResolvedDungeonTileSprite()
        {
        }

        public ResolvedDungeonTileSprite(
            Sprite sprite,
            RuntimeDungeonTilemapLayer layer,
            RuntimeDungeonTilemapRole role,
            Vector2Int cell,
            int heightSteps,
            Color color)
        {
            Sprite = sprite;
            Layer = layer;
            Role = role;
            Cell = cell;
            HeightSteps = heightSteps;
            Color = color;
        }
    }

    /// <summary>
    /// Runtime-only textures and world-space bounds for the two terrain draw layers.
    /// The offsets are mesh centres in dungeon-local world space.
    /// </summary>
    public sealed class RuntimeDungeonBakedLayers
    {
        public Texture2D BackTexture;
        public Texture2D TopTexture;
        public Vector2 BackWorldOffset;
        public Vector2 TopWorldOffset;
        public Vector2 BackWorldSize;
        public Vector2 TopWorldSize;
    }

    /// <summary>
    /// Composes resolved RuleTile sprites into a back layer (void/ground/decoration) and a
    /// top layer (obstacle). A vertical step projects to one half of a map cell.
    /// </summary>
    public static class DungeonRuleTileBakeComposer
    {
        private const int TexturePaddingPixels = 1;

        public static RuntimeDungeonBakedLayers Compose(
            IReadOnlyList<ResolvedDungeonTileSprite> resolvedSprites,
            float cellWorldSize)
        {
            RuntimeDungeonBakedLayers result = new();
            if (resolvedSprites == null || resolvedSprites.Count == 0)
                return result;

            float safeCellWorldSize = Mathf.Max(0.01f, cellWorldSize);
            float outputPixelsPerUnit = GetOutputPixelsPerUnit(resolvedSprites);
            int cellPixels = Mathf.Max(1, Mathf.RoundToInt(safeCellWorldSize * outputPixelsPerUnit));
            List<DrawCommand> backDraws = new();
            List<DrawCommand> topDraws = new();

            for (int i = 0; i < resolvedSprites.Count; i++)
            {
                ResolvedDungeonTileSprite resolved = resolvedSprites[i];
                if (resolved?.Sprite == null || resolved.Sprite.texture == null)
                    continue;

                List<DrawCommand> target = resolved.Layer == RuntimeDungeonTilemapLayer.Obstacle
                    ? topDraws
                    : backDraws;
                AddDrawCommands(target, resolved, safeCellWorldSize, outputPixelsPerUnit, cellPixels);
            }

            BakedLayer back = ComposeLayer(backDraws, outputPixelsPerUnit);
            BakedLayer top = ComposeLayer(topDraws, outputPixelsPerUnit);
            if (back != null)
            {
                result.BackTexture = back.Texture;
                result.BackWorldOffset = back.WorldOffset;
                result.BackWorldSize = back.WorldSize;
            }

            if (top != null)
            {
                result.TopTexture = top.Texture;
                result.TopWorldOffset = top.WorldOffset;
                result.TopWorldSize = top.WorldSize;
            }

            return result;
        }

        private static float GetOutputPixelsPerUnit(IReadOnlyList<ResolvedDungeonTileSprite> resolvedSprites)
        {
            float pixelsPerUnit = 1f;
            for (int i = 0; i < resolvedSprites.Count; i++)
            {
                Sprite sprite = resolvedSprites[i]?.Sprite;
                if (sprite != null && sprite.pixelsPerUnit > 0f)
                    pixelsPerUnit = Mathf.Max(pixelsPerUnit, sprite.pixelsPerUnit);
            }

            return pixelsPerUnit;
        }

        private static void AddDrawCommands(
            List<DrawCommand> draws,
            ResolvedDungeonTileSprite resolved,
            float cellWorldSize,
            float outputPixelsPerUnit,
            int cellPixels)
        {
            int baseX = Mathf.RoundToInt(resolved.Cell.x * cellWorldSize * outputPixelsPerUnit);
            int baseY = Mathf.RoundToInt(resolved.Cell.y * cellWorldSize * outputPixelsPerUnit);
            if (resolved.Role == RuntimeDungeonTilemapRole.ObstacleWall)
            {
                int wallSteps = Mathf.Max(1, resolved.HeightSteps);
                int halfCellPixels = Mathf.Max(1, cellPixels / 2);
                for (int step = 0; step < wallSteps; step++)
                    draws.Add(CreateDrawCommand(resolved, baseX, baseY + step * halfCellPixels, outputPixelsPerUnit));
                return;
            }

            int projectedY = baseY + Mathf.RoundToInt(resolved.HeightSteps * cellPixels * 0.5f);
            draws.Add(CreateDrawCommand(resolved, baseX, projectedY, outputPixelsPerUnit));
        }

        private static DrawCommand CreateDrawCommand(
            ResolvedDungeonTileSprite resolved,
            int destinationX,
            int destinationY,
            float outputPixelsPerUnit)
        {
            Sprite sprite = resolved.Sprite;
            Rect sourceRect = sprite.textureRect;
            int sourceWidth = Mathf.Max(1, Mathf.RoundToInt(sourceRect.width));
            int sourceHeight = Mathf.Max(1, Mathf.RoundToInt(sourceRect.height));
            float spritePixelsPerUnit = Mathf.Max(0.01f, sprite.pixelsPerUnit);
            int destinationWidth = Mathf.Max(1, Mathf.RoundToInt(sourceWidth * outputPixelsPerUnit / spritePixelsPerUnit));
            int destinationHeight = Mathf.Max(1, Mathf.RoundToInt(sourceHeight * outputPixelsPerUnit / spritePixelsPerUnit));
            return new DrawCommand(
                sprite,
                destinationX,
                destinationY,
                destinationWidth,
                destinationHeight,
                resolved.Color);
        }

        private static BakedLayer ComposeLayer(IReadOnlyList<DrawCommand> draws, float outputPixelsPerUnit)
        {
            if (draws == null || draws.Count == 0)
                return null;

            int minimumX = int.MaxValue;
            int minimumY = int.MaxValue;
            int maximumX = int.MinValue;
            int maximumY = int.MinValue;
            for (int i = 0; i < draws.Count; i++)
            {
                DrawCommand draw = draws[i];
                minimumX = Mathf.Min(minimumX, draw.DestinationX);
                minimumY = Mathf.Min(minimumY, draw.DestinationY);
                maximumX = Mathf.Max(maximumX, draw.DestinationX + draw.DestinationWidth);
                maximumY = Mathf.Max(maximumY, draw.DestinationY + draw.DestinationHeight);
            }

            int contentWidth = Mathf.Max(1, maximumX - minimumX);
            int contentHeight = Mathf.Max(1, maximumY - minimumY);
            int outputWidth = contentWidth + TexturePaddingPixels * 2;
            int outputHeight = contentHeight + TexturePaddingPixels * 2;
            Color32[] outputPixels = new Color32[outputWidth * outputHeight];
            Dictionary<Texture2D, Color32[]> sourcePixels = new();
            for (int drawIndex = 0; drawIndex < draws.Count; drawIndex++)
            {
                DrawCommand draw = draws[drawIndex];
                Texture2D sourceTexture = draw.Sprite.texture;
                if (!sourcePixels.TryGetValue(sourceTexture, out Color32[] source))
                {
                    source = ReadPixels(sourceTexture);
                    sourcePixels.Add(sourceTexture, source);
                }

                BlendDraw(outputPixels, outputWidth, outputHeight, draw, source, minimumX, minimumY);
            }

            Texture2D texture = new(outputWidth, outputHeight, TextureFormat.RGBA32, false, false)
            {
                name = "DungeonRuleTileBake",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(outputPixels);
            texture.Apply(false, false);
            Vector2 worldSize = new(outputWidth / outputPixelsPerUnit, outputHeight / outputPixelsPerUnit);
            Vector2 worldOffset = new(
                (minimumX + maximumX) * 0.5f / outputPixelsPerUnit,
                (minimumY + maximumY) * 0.5f / outputPixelsPerUnit);
            return new BakedLayer(texture, worldOffset, worldSize);
        }

        private static void BlendDraw(
            Color32[] outputPixels,
            int outputWidth,
            int outputHeight,
            DrawCommand draw,
            Color32[] sourcePixels,
            int minimumX,
            int minimumY)
        {
            Texture2D sourceTexture = draw.Sprite.texture;
            Rect sourceRect = draw.Sprite.textureRect;
            int sourceX = Mathf.RoundToInt(sourceRect.x);
            int sourceY = Mathf.RoundToInt(sourceRect.y);
            int sourceWidth = Mathf.Max(1, Mathf.RoundToInt(sourceRect.width));
            int sourceHeight = Mathf.Max(1, Mathf.RoundToInt(sourceRect.height));
            int destinationX = draw.DestinationX - minimumX + TexturePaddingPixels;
            int destinationY = draw.DestinationY - minimumY + TexturePaddingPixels;
            for (int y = 0; y < draw.DestinationHeight; y++)
            {
                int targetY = destinationY + y;
                if (targetY < 0 || targetY >= outputHeight)
                    continue;

                int sampleY = sourceY + Mathf.Min(
                    sourceHeight - 1,
                    Mathf.FloorToInt(y * sourceHeight / (float)draw.DestinationHeight));
                for (int x = 0; x < draw.DestinationWidth; x++)
                {
                    int targetX = destinationX + x;
                    if (targetX < 0 || targetX >= outputWidth)
                        continue;

                    int sampleX = sourceX + Mathf.Min(
                        sourceWidth - 1,
                        Mathf.FloorToInt(x * sourceWidth / (float)draw.DestinationWidth));
                    Color32 sourceColor = sourcePixels[sampleY * sourceTexture.width + sampleX];
                    int outputIndex = targetY * outputWidth + targetX;
                    outputPixels[outputIndex] = AlphaBlend(outputPixels[outputIndex], sourceColor, draw.Color);
                }
            }
        }

        private static Color32[] ReadPixels(Texture2D source)
        {
            try
            {
                return source.GetPixels32();
            }
            catch (UnityException)
            {
                RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                RenderTexture previous = RenderTexture.active;
                Texture2D readable = null;
                try
                {
                    temporary.filterMode = FilterMode.Point;
                    Graphics.Blit(source, temporary);
                    RenderTexture.active = temporary;
                    readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
                    readable.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
                    readable.Apply(false, false);
                    return readable.GetPixels32();
                }
                finally
                {
                    RenderTexture.active = previous;
                    if (readable != null)
                        UnityEngine.Object.Destroy(readable);
                    RenderTexture.ReleaseTemporary(temporary);
                }
            }
        }

        private static Color32 AlphaBlend(Color32 destination, Color32 source, Color tint)
        {
            float sourceAlpha = source.a * tint.a / 255f;
            if (sourceAlpha <= 0f)
                return destination;

            float destinationAlpha = destination.a / 255f;
            float outputAlpha = sourceAlpha + destinationAlpha * (1f - sourceAlpha);
            float sourceRed = source.r * tint.r / 255f;
            float sourceGreen = source.g * tint.g / 255f;
            float sourceBlue = source.b * tint.b / 255f;
            float destinationWeight = destinationAlpha * (1f - sourceAlpha);
            return new Color(
                (sourceRed * sourceAlpha + destination.r / 255f * destinationWeight) / outputAlpha,
                (sourceGreen * sourceAlpha + destination.g / 255f * destinationWeight) / outputAlpha,
                (sourceBlue * sourceAlpha + destination.b / 255f * destinationWeight) / outputAlpha,
                outputAlpha);
        }

        private sealed class DrawCommand
        {
            public DrawCommand(Sprite sprite, int destinationX, int destinationY, int destinationWidth, int destinationHeight, Color color)
            {
                Sprite = sprite;
                DestinationX = destinationX;
                DestinationY = destinationY;
                DestinationWidth = destinationWidth;
                DestinationHeight = destinationHeight;
                Color = color;
            }

            public Sprite Sprite { get; }
            public int DestinationX { get; }
            public int DestinationY { get; }
            public int DestinationWidth { get; }
            public int DestinationHeight { get; }
            public Color Color { get; }
        }

        private sealed class BakedLayer
        {
            public BakedLayer(Texture2D texture, Vector2 worldOffset, Vector2 worldSize)
            {
                Texture = texture;
                WorldOffset = worldOffset;
                WorldSize = worldSize;
            }

            public Texture2D Texture { get; }
            public Vector2 WorldOffset { get; }
            public Vector2 WorldSize { get; }
        }
    }

    internal static class DungeonRuleTileVisualBuilder
    {
        private const string RuntimeGridName = "__DungeonTerrainTilemaps";
        private const int BackSortingOrder = -32000;
        private const int TopSortingOrder = 32000;

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
        }

        private static Dictionary<RuntimeDungeonTilemapLayer, Tilemap> CreateRuntimeTilemaps(Transform parent)
        {
            return new Dictionary<RuntimeDungeonTilemapLayer, Tilemap>
            {
                { RuntimeDungeonTilemapLayer.Void, CreateRuntimeTilemap(parent, "Void", BackSortingOrder) },
                { RuntimeDungeonTilemapLayer.Ground, CreateRuntimeTilemap(parent, "Ground", BackSortingOrder + 1) },
                { RuntimeDungeonTilemapLayer.Decoration, CreateRuntimeTilemap(parent, "Decoration", BackSortingOrder + 2) },
                { RuntimeDungeonTilemapLayer.Obstacle, CreateRuntimeTilemap(parent, "Obstacle", TopSortingOrder) },
            };
        }

        private static Tilemap CreateRuntimeTilemap(Transform parent, string name, int sortingOrder)
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
                    placement.Role,
                    placement.Cell,
                    placement.HeightSteps,
                    tilemap.GetColor(cell)));
            }

            return resolvedSprites;
        }

        private static void CreateLayerRenderer(
            DungeonSceneRuntimeRoot runtimeRoot,
            string layerName,
            Texture2D texture,
            Vector2 worldOffset,
            Vector2 worldSize,
            float depth,
            int sortingOrder)
        {
            if (texture == null || worldSize.x <= 0f || worldSize.y <= 0f)
                return;

            Mesh mesh = CreateQuadMesh(layerName, worldSize);
            Material material = CreateSpriteMaterial(texture, layerName);
            if (material == null)
            {
                UnityEngine.Object.Destroy(mesh);
                UnityEngine.Object.Destroy(texture);
                return;
            }

            GameObject layerObject = new(layerName);
            layerObject.transform.SetParent(runtimeRoot.transform, false);
            layerObject.transform.localPosition = new Vector3(worldOffset.x, worldOffset.y, depth);
            MeshFilter meshFilter = layerObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = layerObject.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sortingOrder = sortingOrder;
            runtimeRoot.TrackRuntimeAssets(texture, mesh, material);
        }

        private static Mesh CreateQuadMesh(string layerName, Vector2 worldSize)
        {
            float halfWidth = worldSize.x * 0.5f;
            float halfHeight = worldSize.y * 0.5f;
            Mesh mesh = new()
            {
                name = $"{layerName}Mesh",
                vertices = new[]
                {
                    new Vector3(-halfWidth, -halfHeight, 0f),
                    new Vector3(-halfWidth, halfHeight, 0f),
                    new Vector3(halfWidth, halfHeight, 0f),
                    new Vector3(halfWidth, -halfHeight, 0f),
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(1f, 0f),
                },
                triangles = new[] { 0, 1, 2, 0, 2, 3 },
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateSpriteMaterial(Texture2D texture, string layerName)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Texture");
            if (shader == null)
            {
                Debug.LogError("[DungeonRuleTileVisualBuilder] No compatible sprite shader is available for RuleTile terrain.");
                return null;
            }

            Material material = new(shader)
            {
                name = $"{layerName}Material",
                mainTexture = texture,
            };
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
            return material;
        }
    }
}
