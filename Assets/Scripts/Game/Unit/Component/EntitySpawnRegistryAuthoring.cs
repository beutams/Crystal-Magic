using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CrystalMagic.Game.Unit
{
    public struct EntitySpawnRegistrySingleton : IComponentData
    {
    }

    public struct UnitEntityPrefabRegistryEntry : IBufferElementData
    {
        public FixedString128Bytes Name;
        public Entity Prefab;
    }

    public struct ProjectileEntityPrefabRegistryEntry : IBufferElementData
    {
        public FixedString128Bytes Name;
        public Entity Prefab;
    }

    public struct DropEntityPrefabRegistryEntry : IBufferElementData
    {
        public FixedString128Bytes Name;
        public Entity Prefab;
    }

    public class EntitySpawnRegistryAuthoring : MonoBehaviour
    {
        [SerializeField, HideInInspector] private List<GameObject> _unitPrefabs = new();
        [SerializeField, HideInInspector] private List<GameObject> _projectilePrefabs = new();
        [SerializeField, HideInInspector] private List<GameObject> _dropPrefabs = new();

        public IReadOnlyList<GameObject> UnitPrefabs => _unitPrefabs;
        public IReadOnlyList<GameObject> ProjectilePrefabs => _projectilePrefabs;
        public IReadOnlyList<GameObject> DropPrefabs => _dropPrefabs;

#if UNITY_EDITOR
        private static string UnitFolder => CrystalMagic.Core.AssetPathHelper.GetUnitPrefabDirectory();
        private static string ProjectileFolder => CrystalMagic.Core.AssetPathHelper.GetProjectilePrefabDirectory();
        private static string DropFolder => CrystalMagic.Core.AssetPathHelper.GetDropPrefabDirectory();

        private void Reset() => SyncPrefabLists();

        private void OnValidate() => SyncPrefabLists();

        [ContextMenu("Sync Prefab Lists")]
        private void SyncPrefabLists()
        {
            SyncPrefabsFromFolder(UnitFolder, _unitPrefabs);
            SyncPrefabsFromFolder(ProjectileFolder, _projectilePrefabs);
            SyncPrefabsFromFolder(DropFolder, _dropPrefabs);
        }

        private static void SyncPrefabsFromFolder(string folder, List<GameObject> target)
        {
            target ??= new List<GameObject>();
            target.Clear();

            if (!AssetDatabase.IsValidFolder(folder))
                return;

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab != null)
                    target.Add(prefab);
            }

            target.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.Ordinal));
        }
#endif

        private sealed class Baker : Baker<EntitySpawnRegistryAuthoring>
        {
            public override void Bake(EntitySpawnRegistryAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent<EntitySpawnRegistrySingleton>(entity);

                DynamicBuffer<UnitEntityPrefabRegistryEntry> unitBuffer = AddBuffer<UnitEntityPrefabRegistryEntry>(entity);
                DynamicBuffer<ProjectileEntityPrefabRegistryEntry> projectileBuffer = AddBuffer<ProjectileEntityPrefabRegistryEntry>(entity);
                DynamicBuffer<DropEntityPrefabRegistryEntry> dropBuffer = AddBuffer<DropEntityPrefabRegistryEntry>(entity);

                AddUnitEntries(GetBakePrefabs(authoring.UnitPrefabs, UnitFolder), unitBuffer);
                AddProjectileEntries(GetBakePrefabs(authoring.ProjectilePrefabs, ProjectileFolder), projectileBuffer);
                AddDropEntries(GetBakePrefabs(authoring.DropPrefabs, DropFolder), dropBuffer);
            }

            private void AddUnitEntries(IReadOnlyList<GameObject> prefabs, DynamicBuffer<UnitEntityPrefabRegistryEntry> buffer)
            {
                HashSet<string> usedNames = new(StringComparer.Ordinal);
                for (int i = 0; i < prefabs.Count; i++)
                {
                    GameObject prefab = prefabs[i];
                    if (prefab == null || !usedNames.Add(prefab.name))
                        continue;

                    buffer.Add(new UnitEntityPrefabRegistryEntry
                    {
                        Name = new FixedString128Bytes(prefab.name),
                        Prefab = GetEntity(prefab, TransformUsageFlags.Dynamic),
                    });
                }
            }

            private void AddProjectileEntries(IReadOnlyList<GameObject> prefabs, DynamicBuffer<ProjectileEntityPrefabRegistryEntry> buffer)
            {
                HashSet<string> usedNames = new(StringComparer.Ordinal);
                for (int i = 0; i < prefabs.Count; i++)
                {
                    GameObject prefab = prefabs[i];
                    if (prefab == null || !usedNames.Add(prefab.name))
                        continue;

                    buffer.Add(new ProjectileEntityPrefabRegistryEntry
                    {
                        Name = new FixedString128Bytes(prefab.name),
                        Prefab = GetEntity(prefab, TransformUsageFlags.Dynamic),
                    });
                }
            }

            private void AddDropEntries(IReadOnlyList<GameObject> prefabs, DynamicBuffer<DropEntityPrefabRegistryEntry> buffer)
            {
                HashSet<string> usedNames = new(StringComparer.Ordinal);
                for (int i = 0; i < prefabs.Count; i++)
                {
                    GameObject prefab = prefabs[i];
                    if (prefab == null || !usedNames.Add(prefab.name))
                        continue;

                    buffer.Add(new DropEntityPrefabRegistryEntry
                    {
                        Name = new FixedString128Bytes(prefab.name),
                        Prefab = GetEntity(prefab, TransformUsageFlags.Dynamic),
                    });
                }
            }

            private static IReadOnlyList<GameObject> GetBakePrefabs(IReadOnlyList<GameObject> fallbackPrefabs, string folder)
            {
#if UNITY_EDITOR
                List<GameObject> prefabs = new();
                if (AssetDatabase.IsValidFolder(folder))
                {
                    string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
                    for (int i = 0; i < guids.Length; i++)
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                        if (prefab != null)
                        {
                            prefabs.Add(prefab);
                        }
                    }

                    prefabs.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.Ordinal));
                }

                if (prefabs.Count > 0)
                {
                    return prefabs;
                }
#endif
                return fallbackPrefabs ?? Array.Empty<GameObject>();
            }
        }
    }
}
