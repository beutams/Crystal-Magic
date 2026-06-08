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

        public static void ApplySceneObjectMaterial(EntityManager entityManager, Entity entity, string prefabName, string materialPath)
        {
            if (string.IsNullOrWhiteSpace(materialPath))
                return;

            Material material = ResourceComponent.Instance?.Load<Material>(materialPath);
            Mesh mesh = GetPrefabMesh(prefabName, ownerKey: string.Empty);
            ApplySharedMaterial(entityManager, entity, material, mesh);
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
    }
}
