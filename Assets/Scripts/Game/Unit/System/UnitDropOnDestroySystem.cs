using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Unit;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(DestroyEntitySystem))]
partial class UnitDropOnDestroySystem : SystemBase
{
    private const string DropPrefabName = "Drop";
    private const float DefaultPickupRadius = 0.45f;
    private static readonly System.Collections.Generic.Dictionary<string, Material> s_dropMaterials = new();
    private static Mesh s_dropMesh;
    private static Material s_dropBaseMaterial;
    private static bool s_loggedMissingDropPrefab;

    protected override void OnUpdate()
    {
        foreach ((EnabledRefRO<DestroyEntityFlag> destroyFlag,
                  RefRO<UnitDropComponent> unitDrop,
                  RefRO<LocalTransform> transform,
                  Entity entity) in
                 SystemAPI.Query<EnabledRefRO<DestroyEntityFlag>, RefRO<UnitDropComponent>, RefRO<LocalTransform>>()
                     .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                     .WithEntityAccess())
        {
            if (!destroyFlag.ValueRO || unitDrop.ValueRO.DropDataId < 0)
                continue;

            DropData dropData = DataComponent.Instance.Get<DropData>(unitDrop.ValueRO.DropDataId);
            if (dropData == null)
                continue;

            dropData.EnsureValid();
            Unity.Mathematics.Random random = CreateRandom(entity, transform.ValueRO.Position);
            for (int i = 0; i < dropData.Entries.Count; i++)
            {
                DropEntryData entry = dropData.Entries[i];
                if (entry == null || !IsValidEntry(entry))
                    continue;

                float chance = math.clamp(entry.Chance, 0f, 1f);
                if (chance <= 0f || random.NextFloat() > chance)
                    continue;

                int minQuantity = math.max(0, entry.MinQuantity);
                int maxQuantity = math.max(minQuantity, entry.MaxQuantity);
                int quantity = maxQuantity > minQuantity
                    ? random.NextInt(minQuantity, maxQuantity + 1)
                    : minQuantity;
                if (quantity <= 0)
                    continue;

                SpawnDropEntity(entry, quantity, transform.ValueRO.Position);
            }
        }
    }

    private static bool IsValidEntry(DropEntryData entry)
    {
        if (entry == null)
            return false;

        return entry.DropType switch
        {
            DropRewardType.Money => true,
            _ => entry.ItemId >= 0,
        };
    }

    private void SpawnDropEntity(DropEntryData entry, int quantity, float3 position)
    {
        if (!EntitySpawnRegistryUtility.TryInstantiateDrop(EntityManager, new FixedString128Bytes(DropPrefabName), out Entity dropEntity))
        {
            LogMissingDropPrefabOnce();
            return;
        }

        SetOrAddComponentData(
            dropEntity,
            LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));

        SetOrAddComponentData(
            dropEntity,
            new WorldDropComponent
            {
                DropType = entry.DropType,
                ItemId = entry.ItemId,
                Amount = quantity,
                PickupRadius = DefaultPickupRadius,
            });

        EnsureDestroyFlagDisabled(dropEntity);
        ApplyDropVisual(dropEntity, entry);
    }

    private static Unity.Mathematics.Random CreateRandom(Entity entity, float3 position)
    {
        uint seed = math.hash(new int4(
            entity.Index,
            entity.Version,
            (int)math.round(position.x * 100f),
            (int)math.round(position.y * 100f)));
        if (seed == 0)
            seed = 1u;
        return new Unity.Mathematics.Random(seed);
    }

    private void EnsureDestroyFlagDisabled(Entity entity)
    {
        if (!EntityManager.HasComponent<DestroyEntityFlag>(entity))
            EntityManager.AddComponent<DestroyEntityFlag>(entity);

        EntityManager.SetComponentEnabled<DestroyEntityFlag>(entity, false);
    }

    private void SetOrAddComponentData<T>(Entity entity, T value)
        where T : unmanaged, IComponentData
    {
        if (EntityManager.HasComponent<T>(entity))
            EntityManager.SetComponentData(entity, value);
        else
            EntityManager.AddComponentData(entity, value);
    }

    private void ApplyDropVisual(Entity dropEntity, DropEntryData entry)
    {
        if (!EntityManager.HasComponent<MaterialMeshInfo>(dropEntity))
            return;

        Material dropMaterial = GetOrCreateDropMaterial(entry);
        Mesh dropMesh = GetDropMesh();
        if (dropMaterial == null || dropMesh == null)
            return;

        EntityManager.SetSharedComponentManaged(dropEntity, new RenderMeshArray(new[] { dropMaterial }, new[] { dropMesh }));
        EntityManager.SetComponentData(dropEntity, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
    }

    private Material GetOrCreateDropMaterial(DropEntryData entry)
    {
        string iconPath = GetIconPath(entry);
        if (string.IsNullOrWhiteSpace(iconPath))
            return null;

        if (s_dropMaterials.TryGetValue(iconPath, out Material material) && material != null)
            return material;

        s_dropBaseMaterial ??= ResourceComponent.Instance.Load<Material>("Assets/Res/Material/Drop.mat");
        if (s_dropBaseMaterial == null)
            return null;

        Texture texture = ResourceComponent.Instance.Load<Texture>(iconPath);
        if (texture == null)
            return null;

        material = new Material(s_dropBaseMaterial);
        material.SetTexture("_BaseMap", texture);
        s_dropMaterials[iconPath] = material;
        return material;
    }

    private static string GetIconPath(DropEntryData entry)
    {
        if (entry == null)
            return null;

        if (entry.DropType == DropRewardType.Money)
            return ConfigComponent.Instance?.Get<GameConfig>()?.MoneyIconPath;

        ItemData itemData = DataComponent.Instance.Get<ItemData>(entry.ItemId);
        return itemData?.IconPath;
    }

    private Mesh GetDropMesh()
    {
        if (s_dropMesh != null)
            return s_dropMesh;

        GameObject dropPrefab = ResourceComponent.Instance.Load<GameObject>(AssetPathHelper.GetDropPrefabAsset(DropPrefabName));
        s_dropMesh = dropPrefab != null ? dropPrefab.GetComponent<MeshFilter>()?.sharedMesh : null;
        return s_dropMesh;
    }

    private static void LogMissingDropPrefabOnce()
    {
        if (s_loggedMissingDropPrefab)
            return;

        s_loggedMissingDropPrefab = true;
        Debug.LogWarning(
            $"[UnitDropOnDestroySystem] Could not instantiate drop prefab '{DropPrefabName}' from EntitySpawnRegistry. " +
            "Make sure EntitySpawnRegistryAuthoring is baked and the prefab exists under the drop prefab directory.");
    }
}
