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
    /// Bakes every Tile set below the active scene's List root into static Back and Top render layers.
    /// </summary>
    internal static class TownTilemapBakeUtility
    {
        private const string OutputRootFolder = "Assets/Res/Tile/Out";
        private const string TileSetListName = "List";
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

        [MenuItem("Tools/Map/Bake All Tiles")]
        private static void BakeAllTiles()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Bake All Tiles", "Exit Play Mode before baking Tilemaps.", "OK");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            Transform tileSetList = FindTileSetList(activeScene);
            if (tileSetList == null)
            {
                EditorUtility.DisplayDialog(
                    "Bake All Tiles",
                    $"No root GameObject named {TileSetListName} was found in the active scene.",
                    "OK");
                return;
            }

            if (tileSetList.childCount == 0)
            {
                EditorUtility.DisplayDialog("Bake All Tiles", $"{TileSetListName} does not contain any Tile sets.", "OK");
                return;
            }

            try
            {
                for (int childIndex = 0; childIndex < tileSetList.childCount; childIndex++)
                    BakeTileSet(tileSetList.GetChild(childIndex));

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Selection.activeObject = AssetDatabase.LoadAssetAtPath<DefaultAsset>(OutputRootFolder);
                EditorGUIUtility.PingObject(Selection.activeObject);
                Debug.Log($"[TownTilemapBakeUtility] Baked {tileSetList.childCount} Tile sets to {OutputRootFolder}.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Bake All Tiles", exception.Message, "OK");
            }
        }

        private static Transform FindTileSetList(Scene scene)
        {
            GameObject[] rootObjects = scene.GetRootGameObjects();
            for (int i = 0; i < rootObjects.Length; i++)
            {
                if (rootObjects[i].name.Equals(TileSetListName, StringComparison.OrdinalIgnoreCase))
                    return rootObjects[i].transform;
            }

            return null;
        }

        private static void BakeTileSet(Transform tileSet)
        {
            List<TilemapRenderer> backRenderers = FindRenderers(tileSet, BakeLayer.Back);
            List<TilemapRenderer> topRenderers = FindRenderers(tileSet, BakeLayer.Top);
            if (backRenderers.Count == 0 && topRenderers.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Tile set {tileSet.name} does not contain Tilemaps below a GameObject whose name starts with Back or Top.");
            }

            string outputFolder = $"{OutputRootFolder}/{tileSet.name}";
            EnsureOutputFolder(outputFolder);
            BakedLayer backLayer = BakeLayerTexture(outputFolder, tileSet.name, BakeLayer.Back, backRenderers);
            BakedLayer topLayer = BakeLayerTexture(outputFolder, tileSet.name, BakeLayer.Top, topRenderers);
            CreateOrUpdatePrefab(outputFolder, tileSet.name, backLayer, topLayer);
        }

        private static List<TilemapRenderer> FindRenderers(Transform tileSet, BakeLayer bakeLayer)
        {
            TilemapRenderer[] allRenderers = tileSet.GetComponentsInChildren<TilemapRenderer>();
            List<TilemapRenderer> result = new();
            for (int i = 0; i < allRenderers.Length; i++)
            {
                TilemapRenderer renderer = allRenderers[i];
                if (renderer == null || !MatchesLayer(renderer.transform, tileSet, bakeLayer))
                    continue;

                Tilemap tilemap = renderer.GetComponent<Tilemap>();
                if (tilemap != null && tilemap.cellBounds.size != Vector3Int.zero)
                    result.Add(renderer);
            }

            result.Sort(CompareRenderers);
            return result;
        }

        private static bool MatchesLayer(Transform transform, Transform tileSet, BakeLayer bakeLayer)
        {
            for (Transform current = transform; current != null && current != tileSet; current = current.parent)
            {
                if (current.name.StartsWith("Back", StringComparison.OrdinalIgnoreCase))
                    return bakeLayer == BakeLayer.Back;
                if (current.name.StartsWith("Top", StringComparison.OrdinalIgnoreCase))
                    return bakeLayer == BakeLayer.Top;
            }

            return false;
        }

        private static int CompareRenderers(TilemapRenderer left, TilemapRenderer right)
        {
            int leftLayer = SortingLayer.GetLayerValueFromID(left.sortingLayerID);
            int rightLayer = SortingLayer.GetLayerValueFromID(right.sortingLayerID);
            int layerComparison = leftLayer.CompareTo(rightLayer);
            return layerComparison != 0 ? layerComparison : left.sortingOrder.CompareTo(right.sortingOrder);
        }

        private static BakedLayer BakeLayerTexture(
            string outputFolder,
            string tileSetName,
            BakeLayer bakeLayer,
            List<TilemapRenderer> renderers)
        {
            if (renderers.Count == 0)
                return null;

            float pixelsPerUnit = GetHighestPixelsPerUnit(renderers);
            float bakePixelsPerUnit = pixelsPerUnit * PixelBakeScale;
            List<TilePixel> tiles = CollectTiles(renderers, out Bounds bounds);
            if (tiles.Count == 0)
                return null;

            int width = Mathf.RoundToInt(bounds.size.x * bakePixelsPerUnit);
            int height = Mathf.RoundToInt(bounds.size.y * bakePixelsPerUnit);
            if (width <= 0 || height <= 0)
                throw new InvalidOperationException("The selected Tilemaps have no renderable bounds.");

            string layerName = bakeLayer.ToString();
            string texturePath = $"{outputFolder}/{tileSetName}_{layerName}.png";

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

        private static List<TilePixel> CollectTiles(List<TilemapRenderer> renderers, out Bounds bounds)
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

                    ValidateTile(tilemap, position, sprite);
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

        private static float GetHighestPixelsPerUnit(List<TilemapRenderer> renderers)
        {
            float highestPixelsPerUnit = 0f;
            for (int i = 0; i < renderers.Count; i++)
            {
                Tilemap tilemap = renderers[i].GetComponent<Tilemap>();
                BoundsInt cellBounds = tilemap.cellBounds;
                foreach (Vector3Int position in cellBounds.allPositionsWithin)
                {
                    Sprite sprite = tilemap.GetSprite(position);
                    if (sprite != null && sprite.pixelsPerUnit > highestPixelsPerUnit)
                        highestPixelsPerUnit = sprite.pixelsPerUnit;
                }
            }

            if (highestPixelsPerUnit > 0f)
                return highestPixelsPerUnit;

            throw new InvalidOperationException("The selected Tilemaps do not contain a Sprite Tile that can define the output pixel scale.");
        }

        private static void ValidateTile(Tilemap tilemap, Vector3Int position, Sprite sprite)
        {
            if (sprite.pixelsPerUnit <= 0f)
                throw new InvalidOperationException($"Tile {position} on {tilemap.name} does not have a valid Pixels Per Unit value.");

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
                int destinationX = Mathf.RoundToInt((tile.WorldMin.x - worldMin.x) * pixelsPerUnit);
                int destinationY = Mathf.RoundToInt((tile.WorldMin.y - worldMin.y) * pixelsPerUnit);
                int destinationWidth = Mathf.RoundToInt(tileWidth * pixelsPerUnit / tile.Sprite.pixelsPerUnit);
                int destinationHeight = Mathf.RoundToInt(tileHeight * pixelsPerUnit / tile.Sprite.pixelsPerUnit);
                for (int y = 0; y < destinationHeight; y++)
                {
                    int targetY = destinationY + y;
                    if (targetY < 0 || targetY >= height)
                        continue;

                    int sourcePixelY = sourceY + Mathf.Min(
                        tileHeight - 1,
                        Mathf.FloorToInt(y * tile.Sprite.pixelsPerUnit / pixelsPerUnit));
                    for (int x = 0; x < destinationWidth; x++)
                    {
                        int targetX = destinationX + x;
                        if (targetX < 0 || targetX >= width)
                            continue;

                        int sourcePixelX = sourceX + Mathf.Min(
                            tileWidth - 1,
                            Mathf.FloorToInt(x * tile.Sprite.pixelsPerUnit / pixelsPerUnit));
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

        private static void CreateOrUpdatePrefab(
            string outputFolder,
            string tileSetName,
            BakedLayer backLayer,
            BakedLayer topLayer)
        {
            GameObject root = new($"{tileSetName}_Baked");
            try
            {
                if (backLayer != null)
                    CreateLayerObject(
                        root.transform,
                        outputFolder,
                        tileSetName,
                        backLayer,
                        BakeLayer.Back,
                        BackDepth,
                        BackSortingOrder);
                if (topLayer != null)
                    CreateLayerObject(
                        root.transform,
                        outputFolder,
                        tileSetName,
                        topLayer,
                        BakeLayer.Top,
                        TopDepth,
                        TopSortingOrder);

                PrefabUtility.SaveAsPrefabAsset(root, $"{outputFolder}/{tileSetName}_Baked.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateLayerObject(
            Transform parent,
            string outputFolder,
            string tileSetName,
            BakedLayer bakedLayer,
            BakeLayer bakeLayer,
            float depth,
            int sortingOrder)
        {
            string meshPath = $"{outputFolder}/{tileSetName}_{bakedLayer.Name}.asset";
            string materialPath = $"{outputFolder}/{tileSetName}_{bakedLayer.Name}.mat";
            Mesh mesh = CreateOrUpdateMesh(meshPath, bakedLayer.Bounds.size);
            Material material = CreateOrUpdateMaterial(materialPath, bakedLayer.Texture, bakeLayer);

            GameObject layerObject = new($"{tileSetName}_{bakedLayer.Name}");
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

        private static void EnsureOutputFolder(string outputFolder)
        {
            if (AssetDatabase.IsValidFolder(outputFolder))
                return;

            string[] folders = outputFolder.Split('/');
            string current = folders[0];
            for (int i = 1; i < folders.Length; i++)
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
