using System.Collections.Generic;
using CrystalMagic.Game.Unit;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

namespace CrystalMagic.Core
{
    internal static class DungeonSceneVisualUtility
    {
        private static readonly Dictionary<string, Mesh> s_meshCache = new();

        public static void ApplyEnvironmentVisual(
            EntityManager entityManager,
            Entity entity,
            string prefabName,
            string materialPath,
            string ownerKey,
            float3 size)
        {
            ApplyNonUniformScale(entityManager, entity, size);
            if (string.IsNullOrWhiteSpace(materialPath))
                return;

            Material material = ResourceComponent.Instance?.Load<Material>(materialPath, ownerKey);
            Mesh mesh = GetPrefabMesh(prefabName, ownerKey);
            ApplySharedMaterial(entityManager, entity, material, mesh);
        }

        public static void ApplySpriteVisual(
            EntityManager entityManager,
            Entity entity,
            string spritePath,
            string spriteName,
            bool flippedX,
            string ownerKey,
            DungeonSceneRuntimeRoot runtimeRoot)
        {
            if (string.IsNullOrWhiteSpace(spritePath))
            {
                HideVisual(entityManager, entity);
                return;
            }

            string spriteReference = string.IsNullOrWhiteSpace(spriteName)
                ? spritePath
                : $"{spritePath}|{spriteName}";
            Sprite sprite = ResourceComponent.Instance?.LoadSprite(spriteReference, ownerKey);
            if (sprite == null || sprite.texture == null)
            {
                HideVisual(entityManager, entity);
                return;
            }

            Mesh mesh = CreateSpriteQuadMesh(sprite, flippedX);
            Material material = CreateSpriteMaterial(sprite.texture, sprite.name);
            if (material == null)
            {
                Object.Destroy(mesh);
                HideVisual(entityManager, entity);
                return;
            }

            ApplySharedMaterial(entityManager, entity, material, mesh);
            runtimeRoot?.TrackRuntimeAssets(mesh, material);
        }

        public static void ApplyNonUniformScale(EntityManager entityManager, Entity entity, float3 size)
        {
            PostTransformMatrix matrix = new()
            {
                Value = float4x4.Scale(size),
            };

            if (entityManager.HasComponent<PostTransformMatrix>(entity))
                entityManager.SetComponentData(entity, matrix);
            else
                entityManager.AddComponentData(entity, matrix);
        }

        public static void ApplySharedMaterial(EntityManager entityManager, Entity entity, Material material, Mesh mesh)
        {
            if (material == null || mesh == null || !entityManager.HasComponent<MaterialMeshInfo>(entity))
                return;

            entityManager.SetSharedComponentManaged(entity, new RenderMeshArray(new[] { material }, new[] { mesh }));
            entityManager.SetComponentData(entity, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
        }

        public static void HideVisual(EntityManager entityManager, Entity entity)
        {
            if (entityManager.HasComponent<MaterialMeshInfo>(entity))
                entityManager.RemoveComponent<MaterialMeshInfo>(entity);
        }

        public static Mesh GetPrefabMesh(string prefabName, string ownerKey)
        {
            if (string.IsNullOrWhiteSpace(prefabName))
                return null;

            if (s_meshCache.TryGetValue(prefabName, out Mesh cachedMesh) && cachedMesh != null)
                return cachedMesh;

            GameObject prefab = ResourceComponent.Instance?.Load<GameObject>(AssetPathHelper.GetEnvironmentPrefabAsset(prefabName), ownerKey);
            Mesh mesh = prefab != null ? prefab.GetComponent<MeshFilter>()?.sharedMesh : null;
            if (mesh != null)
                s_meshCache[prefabName] = mesh;

            return mesh;
        }

        private static Mesh CreateSpriteQuadMesh(Sprite sprite, bool flippedX)
        {
            float pixelsPerUnit = Mathf.Max(0.0001f, sprite.pixelsPerUnit);
            Vector2 size = sprite.rect.size / pixelsPerUnit;
            Vector2 pivot = sprite.pivot / pixelsPerUnit;
            float minimumX = -pivot.x;
            float minimumY = -pivot.y;
            float maximumX = minimumX + size.x;
            float maximumY = minimumY + size.y;

            Vector2[] spriteUvs = sprite.uv;
            Vector2[] uvs = spriteUvs != null && spriteUvs.Length >= 4
                ? new[] { spriteUvs[0], spriteUvs[1], spriteUvs[2], spriteUvs[3] }
                : new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(1f, 0f),
                };
            if (flippedX)
            {
                (uvs[0], uvs[3]) = (uvs[3], uvs[0]);
                (uvs[1], uvs[2]) = (uvs[2], uvs[1]);
            }

            Mesh mesh = new()
            {
                name = $"{sprite.name}SpriteMesh",
                vertices = new[]
                {
                    new Vector3(minimumX, minimumY, 0f),
                    new Vector3(minimumX, maximumY, 0f),
                    new Vector3(maximumX, maximumY, 0f),
                    new Vector3(maximumX, minimumY, 0f),
                },
                uv = uvs,
                triangles = new[] { 0, 1, 2, 0, 2, 3 },
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateSpriteMaterial(Texture2D texture, string spriteName)
        {
            Shader shader = Shader.Find("CrystalMagic/TransparentSpriteMesh")
                ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Texture");
            if (shader == null)
            {
                Debug.LogError("[DungeonSceneVisualUtility] No compatible sprite shader is available for an obstacle.");
                return null;
            }

            Material material = new(shader)
            {
                name = $"{spriteName}SpriteMaterial",
                mainTexture = texture,
                enableInstancing = true,
            };
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
            return material;
        }
    }
}
