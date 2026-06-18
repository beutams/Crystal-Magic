using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using CrystalMagic.Game.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.Game.Unit
{
    public static class WorldDropSpawnUtility
    {
        private static readonly FixedString128Bytes DropPrefabName = "Drop";
        private static readonly Dictionary<string, Material> DropMaterials = new();
        private static bool s_loggedMissingDropPrefab;

        public static bool CanSpawnDrop(EntityManager entityManager)
        {
            if (EntitySpawnRegistryUtility.TryGetDropPrefab(entityManager, DropPrefabName, out _))
                return true;

            LogMissingDropPrefabOnce();
            return false;
        }

        public static bool TrySpawnDrop(EntityManager entityManager, DropRewardType dropType, int itemId, int amount, float3 position)
        {
            if (amount <= 0)
                return false;

            if (!EntitySpawnRegistryUtility.TryInstantiateDrop(entityManager, DropPrefabName, out Entity dropEntity))
            {
                LogMissingDropPrefabOnce();
                return false;
            }

            SetOrAddComponentData(entityManager, dropEntity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            SetOrAddComponentData(entityManager, dropEntity, new WorldDropComponent
            {
                DropType = dropType,
                ItemId = itemId,
                Amount = amount,
            });

            ApplyDropVisual(entityManager, dropEntity, dropType, itemId);
            return true;
        }

        private static void SetOrAddComponentData<T>(EntityManager entityManager, Entity entity, T value)
            where T : unmanaged, IComponentData
        {
            if (entityManager.HasComponent<T>(entity))
                entityManager.SetComponentData(entity, value);
            else
                entityManager.AddComponentData(entity, value);
        }

        private static void ApplyDropVisual(EntityManager entityManager, Entity dropEntity, DropRewardType dropType, int itemId)
        {
            if (!entityManager.HasComponent<MaterialMeshInfo>(dropEntity))
                return;

            RenderMeshArray renderMeshArray = entityManager.GetSharedComponentManaged<RenderMeshArray>(dropEntity);
            UnityObjectRef<Mesh>[] meshReferences = renderMeshArray.MeshReferences;
            UnityObjectRef<Material>[] materialReferences = renderMeshArray.MaterialReferences;
            Mesh dropMesh = meshReferences != null && meshReferences.Length > 0 ? meshReferences[0].Value : null;
            Material baseMaterial = materialReferences != null && materialReferences.Length > 0 ? materialReferences[0].Value : null;
            Material dropMaterial = GetOrCreateDropMaterial(dropType, itemId, baseMaterial);
            if (dropMaterial == null || dropMesh == null)
                return;

            entityManager.SetSharedComponentManaged(dropEntity, new RenderMeshArray(new[] { dropMaterial }, new[] { dropMesh }));
            entityManager.SetComponentData(dropEntity, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
        }

        private static Material GetOrCreateDropMaterial(DropRewardType dropType, int itemId, Material baseMaterial)
        {
            string iconPath = AssetPathHelper.GetImageAsset(GetIconPath(dropType, itemId));
            if (string.IsNullOrWhiteSpace(iconPath) || baseMaterial == null)
                return null;

            if (DropMaterials.TryGetValue(iconPath, out Material material) && material != null)
                return material;

            Texture texture = ResourceComponent.Instance.Load<Texture>(iconPath);
            if (texture == null)
                return null;

            material = new Material(baseMaterial);
            material.SetTexture("_BaseMap", texture);
            DropMaterials[iconPath] = material;
            return material;
        }

        private static string GetIconPath(DropRewardType dropType, int itemId)
        {
            if (dropType == DropRewardType.Money)
                return ConfigComponent.Instance.Get<GameConfig>().MoneyIconPath;

            ItemData itemData = DataComponent.Instance.Get<ItemData>(itemId);
            return itemData?.IconPath;
        }

        private static void LogMissingDropPrefabOnce()
        {
            if (s_loggedMissingDropPrefab)
                return;

            s_loggedMissingDropPrefab = true;
            Debug.LogWarning("[WorldDropSpawnUtility] Could not instantiate drop prefab 'Drop' from EntitySpawnRegistry. Make sure EntitySpawnRegistryAuthoring is baked and the prefab exists under the drop prefab directory.");
        }
    }
}
