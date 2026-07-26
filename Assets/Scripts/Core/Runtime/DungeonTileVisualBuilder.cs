using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CrystalMagic.Core
{
    internal static class DungeonTileVisualBuilder
    {
        private const int MaxTilesPerMesh = 8000;
        private const float TileOverlapPixels = 0.5f;

        public static void Build(
            DungeonSceneRuntimeRoot runtimeRoot,
            RuntimeDungeonSceneData sceneData,
            string resourceOwnerKey)
        {
            if (runtimeRoot == null || sceneData?.TileSpawns == null || sceneData.TileSpawns.Count == 0)
                return;

            Dictionary<string, List<RuntimeDungeonTileSpawnData>> spawnsByTexture = new(StringComparer.Ordinal);
            for (int i = 0; i < sceneData.TileSpawns.Count; i++)
            {
                RuntimeDungeonTileSpawnData spawn = sceneData.TileSpawns[i];
                if (spawn == null || string.IsNullOrWhiteSpace(spawn.SpritePath))
                    continue;

                if (!spawnsByTexture.TryGetValue(spawn.SpritePath, out List<RuntimeDungeonTileSpawnData> spawns))
                {
                    spawns = new List<RuntimeDungeonTileSpawnData>();
                    spawnsByTexture.Add(spawn.SpritePath, spawns);
                }

                spawns.Add(spawn);
            }

            foreach (KeyValuePair<string, List<RuntimeDungeonTileSpawnData>> pair in spawnsByTexture)
                BuildTextureBatches(runtimeRoot, pair.Key, pair.Value, resourceOwnerKey);
        }

        private static void BuildTextureBatches(
            DungeonSceneRuntimeRoot runtimeRoot,
            string texturePath,
            List<RuntimeDungeonTileSpawnData> spawns,
            string resourceOwnerKey)
        {
            Texture2D texture = ResourceComponent.Instance?.Load<Texture2D>(texturePath, resourceOwnerKey);
            if (texture == null)
            {
                Debug.LogError($"[DungeonTileVisualBuilder] Failed to load tile texture: {texturePath}");
                return;
            }

            Material material = CreateTileMaterial(texture);
            if (material == null)
            {
                Debug.LogError($"[DungeonTileVisualBuilder] No compatible sprite shader is available for: {texturePath}");
                return;
            }

            runtimeRoot.TrackRuntimeAsset(material);
            Dictionary<string, Rect> uvCache = new(StringComparer.Ordinal);
            HashSet<string> missingSprites = new(StringComparer.Ordinal);
            int batchCount = 0;
            for (int start = 0; start < spawns.Count; start += MaxTilesPerMesh)
            {
                int count = Mathf.Min(MaxTilesPerMesh, spawns.Count - start);
                Mesh mesh = BuildMesh(spawns, start, count, texture, texturePath, uvCache, missingSprites);
                if (mesh == null)
                    continue;

                GameObject batchObject = new($"DungeonTileBatch_{batchCount++}");
                batchObject.transform.SetParent(runtimeRoot.transform, false);
                MeshFilter meshFilter = batchObject.AddComponent<MeshFilter>();
                MeshRenderer meshRenderer = batchObject.AddComponent<MeshRenderer>();
                meshFilter.sharedMesh = mesh;
                meshRenderer.sharedMaterial = material;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                runtimeRoot.TrackRuntimeAsset(mesh);
            }
        }

        private static Mesh BuildMesh(
            List<RuntimeDungeonTileSpawnData> spawns,
            int start,
            int count,
            Texture2D texture,
            string texturePath,
            Dictionary<string, Rect> uvCache,
            HashSet<string> missingSprites)
        {
            List<Vector3> vertices = new(count * 4);
            List<Vector2> uvs = new(count * 4);
            List<int> triangles = new(count * 6);

            for (int i = 0; i < count; i++)
            {
                RuntimeDungeonTileSpawnData spawn = spawns[start + i];
                string spriteKey = spawn?.SpriteName ?? string.Empty;
                if (!uvCache.TryGetValue(spriteKey, out Rect uvRect)
                    && !TryResolveUv(spawn, out uvRect))
                {
                    if (missingSprites.Add(spriteKey))
                        Debug.LogWarning($"[DungeonTileVisualBuilder] Tile '{spawn?.SpriteName}' has no sprite UV data: {texturePath}");
                    continue;
                }

                uvCache[spriteKey] = uvRect;

                float cellWorldSize = Mathf.Max(0.01f, spawn.CellWorldSize);
                float spritePixelWidth = Mathf.Max(1f, texture.width * uvRect.width);
                float spritePixelHeight = Mathf.Max(1f, texture.height * uvRect.height);
                float overlap = cellWorldSize * TileOverlapPixels / Mathf.Min(spritePixelWidth, spritePixelHeight);
                float halfSize = cellWorldSize * 0.5f + overlap;
                Vector3 position = spawn.WorldPosition;
                int vertexIndex = vertices.Count;
                vertices.Add(new Vector3(position.x - halfSize, position.y - halfSize, position.z));
                vertices.Add(new Vector3(position.x - halfSize, position.y + halfSize, position.z));
                vertices.Add(new Vector3(position.x + halfSize, position.y + halfSize, position.z));
                vertices.Add(new Vector3(position.x + halfSize, position.y - halfSize, position.z));
                uvs.Add(new Vector2(uvRect.xMin, uvRect.yMin));
                uvs.Add(new Vector2(uvRect.xMin, uvRect.yMax));
                uvs.Add(new Vector2(uvRect.xMax, uvRect.yMax));
                uvs.Add(new Vector2(uvRect.xMax, uvRect.yMin));
                triangles.Add(vertexIndex);
                triangles.Add(vertexIndex + 1);
                triangles.Add(vertexIndex + 2);
                triangles.Add(vertexIndex);
                triangles.Add(vertexIndex + 2);
                triangles.Add(vertexIndex + 3);
            }

            if (vertices.Count == 0)
                return null;

            Mesh mesh = new()
            {
                name = $"DungeonTileMesh_{System.IO.Path.GetFileNameWithoutExtension(texturePath)}",
                indexFormat = IndexFormat.UInt32,
            };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateTileMaterial(Texture2D texture)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Texture");
            if (shader == null)
                return null;

            Material material = new(shader)
            {
                name = $"DungeonTileMaterial_{texture.name}",
                mainTexture = texture,
            };
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
            return material;
        }

        private static bool TryResolveUv(RuntimeDungeonTileSpawnData spawn, out Rect uvRect)
        {
            if (spawn != null && spawn.UvRect.z > 0f && spawn.UvRect.w > 0f)
            {
                uvRect = new Rect(spawn.UvRect.x, spawn.UvRect.y, spawn.UvRect.z, spawn.UvRect.w);
                return true;
            }

#if UNITY_EDITOR
            if (spawn != null && !string.IsNullOrWhiteSpace(spawn.SpritePath) && !string.IsNullOrWhiteSpace(spawn.SpriteName))
            {
                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(spawn.SpritePath);
                for (int i = 0; i < assets.Length; i++)
                {
                    if (assets[i] is not Sprite sprite || !string.Equals(sprite.name, spawn.SpriteName, StringComparison.Ordinal))
                        continue;

                    Texture2D texture = sprite.texture;
                    if (texture != null)
                    {
                        Rect textureRect = sprite.textureRect;
                        uvRect = new Rect(
                            textureRect.x / texture.width,
                            textureRect.y / texture.height,
                            textureRect.width / texture.width,
                            textureRect.height / texture.height);
                        return true;
                    }
                }
            }
#endif

            uvRect = default;
            return false;
        }
    }
}
