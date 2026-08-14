using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace CrystalMagic.Editor.Map
{
    /// <summary>
    /// Bakes authoring Tilemaps into the two static town render layers used at runtime.
    /// </summary>
    internal static class TownTilemapBakeUtility
    {
        private const string OutputFolder = "Assets/Res/Map/Town/Generated";
        private const string PrefabPath = OutputFolder + "/TownMap_Baked.prefab";
        private const float BackDepth = 0.1f;
        private const float TopDepth = -0.1f;
        private const int BackSortingOrder = -100;
        private const int TopSortingOrder = 100;
        private const int PixelBakeScale = 4;

        private enum BakeLayer
        {
            Back,
            Top,
        }

        [MenuItem("Tools/Map/Bake Town Tilemaps")]
        private static void BakeActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Bake Town Tilemaps", "Exit Play Mode before baking the town Tilemaps.", "OK");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            List<TilemapRenderer> backRenderers = FindRenderers(activeScene, BakeLayer.Back);
            List<TilemapRenderer> topRenderers = FindRenderers(activeScene, BakeLayer.Top);
            if (backRenderers.Count == 0 && topRenderers.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Bake Town Tilemaps",
                    "No Tilemaps were found under a root GameObject whose name starts with Back or Top.",
                    "OK");
                return;
            }

            EnsureOutputFolder();
            try
            {
                BakedLayer backLayer = BakeLayerTexture(BakeLayer.Back, backRenderers);
                BakedLayer topLayer = BakeLayerTexture(BakeLayer.Top, topRenderers);
                CreateOrUpdatePrefab(backLayer, topLayer);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                Debug.Log($"[TownTilemapBakeUtility] Baked town map to {OutputFolder}.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Bake Town Tilemaps", exception.Message, "OK");
            }
        }

        private static List<TilemapRenderer> FindRenderers(Scene scene, BakeLayer bakeLayer)
        {
            TilemapRenderer[] allRenderers = UnityEngine.Object.FindObjectsByType<TilemapRenderer>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            List<TilemapRenderer> result = new();
            for (int i = 0; i < allRenderers.Length; i++)
            {
                TilemapRenderer renderer = allRenderers[i];
                if (renderer == null || renderer.gameObject.scene != scene || !MatchesLayer(renderer.transform, bakeLayer))
                    continue;

                Tilemap tilemap = renderer.GetComponent<Tilemap>();
                if (tilemap != null && tilemap.cellBounds.size != Vector3Int.zero)
                    result.Add(renderer);
            }

            result.Sort(CompareRenderers);
            return result;
        }

        private static bool MatchesLayer(Transform transform, BakeLayer bakeLayer)
        {
            Transform root = transform;
            while (root.parent != null)
                root = root.parent;

            string prefix = bakeLayer == BakeLayer.Back ? "Back" : "Top";
            return root.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareRenderers(TilemapRenderer left, TilemapRenderer right)
        {
            int leftLayer = SortingLayer.GetLayerValueFromID(left.sortingLayerID);
            int rightLayer = SortingLayer.GetLayerValueFromID(right.sortingLayerID);
            int layerComparison = leftLayer.CompareTo(rightLayer);
            return layerComparison != 0 ? layerComparison : left.sortingOrder.CompareTo(right.sortingOrder);
        }

        private static BakedLayer BakeLayerTexture(BakeLayer bakeLayer, List<TilemapRenderer> renderers)
        {
            if (renderers.Count == 0)
                return null;

            float pixelsPerUnit = GetPixelsPerUnit(renderers);
            float bakePixelsPerUnit = pixelsPerUnit * PixelBakeScale;
            List<TilePixel> tiles = CollectTiles(renderers, pixelsPerUnit, out Bounds bounds);
            if (tiles.Count == 0)
                return null;

            int width = Mathf.RoundToInt(bounds.size.x * bakePixelsPerUnit);
            int height = Mathf.RoundToInt(bounds.size.y * bakePixelsPerUnit);
            if (width <= 0 || height <= 0)
                throw new InvalidOperationException("The selected Tilemaps have no renderable bounds.");

            string layerName = bakeLayer.ToString();
            string texturePath = $"{OutputFolder}/Town_{layerName}.png";

            Texture2D texture = ComposeLayer(tiles, bounds.min, width, height, bakePixelsPerUnit);
            try
            {
                File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureTextureImporter(texturePath);
            Texture2D importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (importedTexture == null)
                throw new InvalidOperationException($"Failed to import baked {layerName} texture: {texturePath}");

            return new BakedLayer(layerName, bounds, importedTexture);
        }

        private static List<TilePixel> CollectTiles(
            List<TilemapRenderer> renderers,
            float pixelsPerUnit,
            out Bounds bounds)
        {
            List<TilePixel> result = new();
            bool hasBounds = false;
            Vector2 min = default;
            Vector2 max = default;
            for (int rendererIndex = 0; rendererIndex < renderers.Count; rendererIndex++)
            {
                Tilemap tilemap = renderers[rendererIndex].GetComponent<Tilemap>();
                BoundsInt cellBounds = tilemap.cellBounds;
                foreach (Vector3Int position in cellBounds.allPositionsWithin)
                {
                    Sprite sprite = tilemap.GetSprite(position);
                    if (sprite == null)
                        continue;

                    ValidateTile(tilemap, position, sprite, pixelsPerUnit);
                    Vector3 cellMin = tilemap.CellToWorld(position);
                    Vector3 cellMax = tilemap.CellToWorld(position + new Vector3Int(1, 1, 0));
                    Vector2 cellWorldMin = new(Mathf.Min(cellMin.x, cellMax.x), Mathf.Min(cellMin.y, cellMax.y));
                    Vector2 spriteWorldSize = new(
                        sprite.textureRect.width / sprite.pixelsPerUnit,
                        sprite.textureRect.height / sprite.pixelsPerUnit);
                    Vector2 spriteWorldMax = cellWorldMin + spriteWorldSize;
                    if (!hasBounds)
                    {
                        min = cellWorldMin;
                        max = spriteWorldMax;
                        hasBounds = true;
                    }
                    else
                    {
                        min = Vector2.Min(min, cellWorldMin);
                        max = Vector2.Max(max, spriteWorldMax);
                    }

                    result.Add(new TilePixel(sprite, cellWorldMin, tilemap.GetColor(position)));
                }
            }

            if (!hasBounds)
            {
                bounds = default;
                return result;
            }

            bounds = new Bounds(
                new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, 0f),
                new Vector3(max.x - min.x, max.y - min.y, 0f));
            return result;
        }

        private static float GetPixelsPerUnit(List<TilemapRenderer> renderers)
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                Tilemap tilemap = renderers[i].GetComponent<Tilemap>();
                BoundsInt cellBounds = tilemap.cellBounds;
                foreach (Vector3Int position in cellBounds.allPositionsWithin)
                {
                    Sprite sprite = tilemap.GetSprite(position);
                    if (sprite != null && sprite.pixelsPerUnit > 0f)
                        return sprite.pixelsPerUnit;
                }
            }

            throw new InvalidOperationException("The selected Tilemaps do not contain a Sprite Tile that can define the output pixel scale.");
        }

        private static void ValidateTile(Tilemap tilemap, Vector3Int position, Sprite sprite, float pixelsPerUnit)
        {
            if (!Mathf.Approximately(sprite.pixelsPerUnit, pixelsPerUnit))
            {
                throw new InvalidOperationException(
                    $"Tile {position} on {tilemap.name} uses a different Pixels Per Unit value. All town tiles must use {pixelsPerUnit}.");
            }

            Vector3 cellOrigin = tilemap.CellToWorld(position);
            Vector3 cellX = tilemap.CellToWorld(position + Vector3Int.right) - cellOrigin;
            Vector3 cellY = tilemap.CellToWorld(position + Vector3Int.up) - cellOrigin;
            if (!Mathf.Approximately(cellX.y, 0f) || !Mathf.Approximately(cellY.x, 0f))
            {
                throw new InvalidOperationException(
                    $"Tilemap {tilemap.name} is rotated or sheared. Pixel baking supports axis-aligned Tilemaps only.");
            }

            Rect textureRect = sprite.textureRect;
            if (textureRect.width <= 0f || textureRect.height <= 0f)
            {
                throw new InvalidOperationException(
                    $"Tile {position} on {tilemap.name} does not have a valid source Sprite rectangle.");
            }

            if (sprite.packingRotation != SpritePackingRotation.None)
                throw new InvalidOperationException($"Tile {position} on {tilemap.name} uses a rotated Sprite Atlas entry, which pixel baking does not support.");

            if (tilemap.GetTransformMatrix(position) != Matrix4x4.identity)
                throw new InvalidOperationException($"Tile {position} on {tilemap.name} uses a per-tile transform, which pixel baking does not support.");
        }

        private static Texture2D ComposeLayer(
            List<TilePixel> tiles,
            Vector3 worldMin,
            int width,
            int height,
            float pixelsPerUnit)
        {
            Color32[] output = new Color32[width * height];
            Dictionary<Texture2D, Color32[]> sourcePixels = new();
            for (int tileIndex = 0; tileIndex < tiles.Count; tileIndex++)
            {
                TilePixel tile = tiles[tileIndex];
                Texture2D source = tile.Sprite.texture;
                if (!sourcePixels.TryGetValue(source, out Color32[] pixels))
                {
                    pixels = ReadPixels(source);
                    sourcePixels.Add(source, pixels);
                }

                Rect sourceRect = tile.Sprite.textureRect;
                int sourceX = Mathf.RoundToInt(sourceRect.x);
                int sourceY = Mathf.RoundToInt(sourceRect.y);
                int tileWidth = Mathf.RoundToInt(sourceRect.width);
                int tileHeight = Mathf.RoundToInt(sourceRect.height);
                int pixelScale = Mathf.RoundToInt(pixelsPerUnit / tile.Sprite.pixelsPerUnit);
                if (pixelScale <= 0 || !Mathf.Approximately(pixelScale * tile.Sprite.pixelsPerUnit, pixelsPerUnit))
                    throw new InvalidOperationException($"Tile {tile.Sprite.name} cannot be represented without pixel interpolation.");

                int destinationX = Mathf.RoundToInt((tile.WorldMin.x - worldMin.x) * pixelsPerUnit);
                int destinationY = Mathf.RoundToInt((tile.WorldMin.y - worldMin.y) * pixelsPerUnit);
                for (int y = 0; y < tileHeight * pixelScale; y++)
                {
                    int targetY = destinationY + y;
                    if (targetY < 0 || targetY >= height)
                        continue;

                    int sourcePixelY = sourceY + y / pixelScale;
                    for (int x = 0; x < tileWidth * pixelScale; x++)
                    {
                        int targetX = destinationX + x;
                        if (targetX < 0 || targetX >= width)
                            continue;

                        int sourcePixelX = sourceX + x / pixelScale;
                        Color32 sourceColor = pixels[sourcePixelY * source.width + sourcePixelX];
                        int outputIndex = targetY * width + targetX;
                        output[outputIndex] = AlphaBlend(output[outputIndex], sourceColor, tile.Color);
                    }
                }
            }

            Texture2D texture = new(width, height, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(output);
            texture.Apply(false, false);
            return texture;
        }

        private static Color32[] ReadPixels(Texture2D source)
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
                    UnityEngine.Object.DestroyImmediate(readable);
                RenderTexture.ReleaseTemporary(temporary);
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

        private static void ConfigureTextureImporter(string texturePath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException($"No texture importer is available for {texturePath}");

            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        private static void CreateOrUpdatePrefab(BakedLayer backLayer, BakedLayer topLayer)
        {
            GameObject root = new("TownMap_Baked");
            try
            {
                if (backLayer != null)
                    CreateLayerObject(root.transform, backLayer, BakeLayer.Back, BackDepth, BackSortingOrder);
                if (topLayer != null)
                    CreateLayerObject(root.transform, topLayer, BakeLayer.Top, TopDepth, TopSortingOrder);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateLayerObject(
            Transform parent,
            BakedLayer bakedLayer,
            BakeLayer bakeLayer,
            float depth,
            int sortingOrder)
        {
            string meshPath = $"{OutputFolder}/Town_{bakedLayer.Name}.asset";
            string materialPath = $"{OutputFolder}/Town_{bakedLayer.Name}.mat";
            Mesh mesh = CreateOrUpdateMesh(meshPath, bakedLayer.Bounds.size);
            Material material = CreateOrUpdateMaterial(materialPath, bakedLayer.Texture, bakeLayer);

            GameObject layerObject = new($"Town_{bakedLayer.Name}");
            layerObject.transform.SetParent(parent, false);
            layerObject.transform.position = new Vector3(bakedLayer.Bounds.center.x, bakedLayer.Bounds.center.y, depth);
            MeshFilter meshFilter = layerObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = layerObject.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sortingOrder = sortingOrder;
        }

        private static Mesh CreateOrUpdateMesh(string meshPath, Vector3 size)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, meshPath);
            }

            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            mesh.Clear();
            mesh.name = Path.GetFileNameWithoutExtension(meshPath);
            mesh.vertices = new[]
            {
                new Vector3(-halfWidth, -halfHeight, 0f),
                new Vector3(-halfWidth, halfHeight, 0f),
                new Vector3(halfWidth, halfHeight, 0f),
                new Vector3(halfWidth, -halfHeight, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f),
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material CreateOrUpdateMaterial(string materialPath, Texture2D texture, BakeLayer bakeLayer)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                throw new InvalidOperationException("The URP Unlit shader is required to bake a town map for Entities Graphics.");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.name = Path.GetFileNameWithoutExtension(materialPath);
            material.shader = shader;
            material.mainTexture = texture;
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.01f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", 1f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = bakeLayer == BakeLayer.Top
                ? (int)RenderQueue.Transparent
                : (int)RenderQueue.Geometry;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureOutputFolder()
        {
            if (AssetDatabase.IsValidFolder(OutputFolder))
                return;

            string current = "Assets";
            string[] folders = { "Res", "Map", "Town", "Generated" };
            for (int i = 0; i < folders.Length; i++)
            {
                string next = $"{current}/{folders[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, folders[i]);
                current = next;
            }
        }

        private sealed class BakedLayer
        {
            public BakedLayer(string name, Bounds bounds, Texture2D texture)
            {
                Name = name;
                Bounds = bounds;
                Texture = texture;
            }

            public string Name { get; }
            public Bounds Bounds { get; }
            public Texture2D Texture { get; }
        }

        private readonly struct TilePixel
        {
            public TilePixel(Sprite sprite, Vector2 worldMin, Color color)
            {
                Sprite = sprite;
                WorldMin = worldMin;
                Color = color;
            }

            public Sprite Sprite { get; }
            public Vector2 WorldMin { get; }
            public Color Color { get; }
        }
    }
}
