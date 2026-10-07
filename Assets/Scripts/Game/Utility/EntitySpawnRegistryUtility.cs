using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;

namespace CrystalMagic.Game.Unit
{
    public static class EntitySpawnRegistryUtility
    {
        public static void GetRegisteredUnitNames(EntityManager entityManager, List<string> destination)
        {
            destination.Clear();

            HashSet<string> names = new(System.StringComparer.Ordinal);
            using NativeArray<Entity> registries = GetRegistries<UnitEntityPrefabRegistryEntry>(entityManager);
            foreach (Entity registry in registries)
            {
                if (!IsRegistryActive(entityManager, registry)) continue;
                foreach (UnitEntityPrefabRegistryEntry entry in entityManager.GetBuffer<UnitEntityPrefabRegistryEntry>(registry, true))
                    if (entityManager.Exists(entry.Prefab) && names.Add(entry.Name.ToString()))
                        destination.Add(entry.Name.ToString());
            }

            destination.Sort(System.StringComparer.Ordinal);
        }

        public static bool TryGetUnitPrefab(EntityManager entityManager, in FixedString128Bytes unitName, out Entity prefab)
        {
            using NativeArray<Entity> registries = GetRegistries<UnitEntityPrefabRegistryEntry>(entityManager);
            foreach (Entity registry in registries)
            {
                if (!IsRegistryActive(entityManager, registry)) continue;
                DynamicBuffer<UnitEntityPrefabRegistryEntry> buffer = entityManager.GetBuffer<UnitEntityPrefabRegistryEntry>(registry, true);
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i].Name.Equals(unitName) && entityManager.Exists(buffer[i].Prefab))
                    {
                        prefab = buffer[i].Prefab;
                        return true;
                    }
                }
            }

            prefab = Entity.Null;
            return false;
        }

        public static bool TryGetProjectilePrefab(EntityManager entityManager, in FixedString128Bytes projectileName, out Entity prefab)
        {
            using NativeArray<Entity> registries = GetRegistries<ProjectileEntityPrefabRegistryEntry>(entityManager);
            foreach (Entity registry in registries)
            {
                if (!IsRegistryActive(entityManager, registry)) continue;
                DynamicBuffer<ProjectileEntityPrefabRegistryEntry> buffer = entityManager.GetBuffer<ProjectileEntityPrefabRegistryEntry>(registry, true);
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i].Name.Equals(projectileName) && entityManager.Exists(buffer[i].Prefab))
                    {
                        prefab = buffer[i].Prefab;
                        return true;
                    }
                }
            }

            prefab = Entity.Null;
            return false;
        }

        public static bool TryInstantiateUnit(EntityManager entityManager, in FixedString128Bytes unitName, out Entity instance)
        {
            if (!TryGetUnitPrefab(entityManager, unitName, out Entity prefab))
            {
                instance = Entity.Null;
                return false;
            }

            instance = entityManager.Instantiate(prefab);
            InitializeInstantiatedEntity(entityManager, instance, unitName);
            return instance != Entity.Null;
        }

        public static bool TryInstantiateProjectile(EntityManager entityManager, in FixedString128Bytes projectileName, out Entity instance)
        {
            if (!TryGetProjectilePrefab(entityManager, projectileName, out Entity prefab))
            {
                instance = Entity.Null;
                return false;
            }

            instance = entityManager.Instantiate(prefab);
            InitializeInstantiatedEntity(entityManager, instance, projectileName);
            return instance != Entity.Null;
        }

        public static bool TryGetDropPrefab(EntityManager entityManager, in FixedString128Bytes dropName, out Entity prefab)
        {
            using NativeArray<Entity> registries = GetRegistries<DropEntityPrefabRegistryEntry>(entityManager);
            foreach (Entity registry in registries)
            {
                if (!IsRegistryActive(entityManager, registry)) continue;
                DynamicBuffer<DropEntityPrefabRegistryEntry> buffer = entityManager.GetBuffer<DropEntityPrefabRegistryEntry>(registry, true);
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i].Name.Equals(dropName) && entityManager.Exists(buffer[i].Prefab))
                    {
                        prefab = buffer[i].Prefab;
                        return true;
                    }
                }
            }

            prefab = Entity.Null;
            return false;
        }

        public static bool TryInstantiateDrop(EntityManager entityManager, in FixedString128Bytes dropName, out Entity instance)
        {
            if (!TryGetDropPrefab(entityManager, dropName, out Entity prefab))
            {
                instance = Entity.Null;
                return false;
            }

            instance = entityManager.Instantiate(prefab);
            InitializeInstantiatedEntity(entityManager, instance, dropName);
            return instance != Entity.Null;
        }

        public static bool TryGetEnvironmentPrefab(EntityManager entityManager, in FixedString128Bytes prefabName, out Entity prefab)
        {
            using NativeArray<Entity> registries = GetRegistries<EnvironmentEntityPrefabRegistryEntry>(entityManager);
            foreach (Entity registry in registries)
            {
                if (!IsRegistryActive(entityManager, registry)) continue;
                DynamicBuffer<EnvironmentEntityPrefabRegistryEntry> buffer = entityManager.GetBuffer<EnvironmentEntityPrefabRegistryEntry>(registry, true);
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i].Name.Equals(prefabName) && entityManager.Exists(buffer[i].Prefab))
                    {
                        prefab = buffer[i].Prefab;
                        return true;
                    }
                }
            }

            prefab = Entity.Null;
            return false;
        }

        public static bool TryInstantiateEnvironment(EntityManager entityManager, in FixedString128Bytes prefabName, out Entity instance)
        {
            if (!TryGetEnvironmentPrefab(entityManager, prefabName, out Entity prefab))
            {
                instance = Entity.Null;
                return false;
            }

            instance = entityManager.Instantiate(prefab);
            InitializeInstantiatedEntity(entityManager, instance, prefabName);
            return instance != Entity.Null;
        }

        public static bool TryGetVfxPrefab(EntityManager entityManager, in FixedString128Bytes prefabName, out Entity prefab)
        {
            using NativeArray<Entity> registries = GetRegistries<VfxEntityPrefabRegistryEntry>(entityManager);
            foreach (Entity registry in registries)
            {
                if (!IsRegistryActive(entityManager, registry)) continue;
                DynamicBuffer<VfxEntityPrefabRegistryEntry> buffer = entityManager.GetBuffer<VfxEntityPrefabRegistryEntry>(registry, true);
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i].Name.Equals(prefabName) && entityManager.Exists(buffer[i].Prefab))
                    {
                        prefab = buffer[i].Prefab;
                        return true;
                    }
                }
            }

            prefab = Entity.Null;
            return false;
        }

        public static bool TryInstantiateVfx(EntityManager entityManager, in FixedString128Bytes prefabName, out Entity instance)
        {
            if (!TryGetVfxPrefab(entityManager, prefabName, out Entity prefab))
            {
                instance = Entity.Null;
                return false;
            }

            instance = entityManager.Instantiate(prefab);
            InitializeInstantiatedEntity(entityManager, instance, prefabName);
            return instance != Entity.Null;
        }

        private static void InitializeInstantiatedEntity(
            EntityManager entityManager,
            Entity entity,
            in FixedString128Bytes prefabName)
        {
            InitializeDestroyFlag(entityManager, entity);
#if UNITY_EDITOR
            if (entity != Entity.Null && entityManager.Exists(entity))
                entityManager.SetName(entity, new FixedString64Bytes(prefabName.ToString()));
#endif
        }

        public static void InitializeDestroyFlag(EntityManager entityManager, Entity entity)
        {
            if (entity == Entity.Null || !entityManager.Exists(entity))
                return;

            if (!entityManager.HasComponent<DestroyEntityFlag>(entity))
                entityManager.AddComponent<DestroyEntityFlag>(entity);

            entityManager.SetComponentEnabled<DestroyEntityFlag>(entity, false);
        }

        public static bool HasRegistry(EntityManager entityManager)
        {
            using EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EntitySpawnRegistrySingleton>());
            using NativeArray<Entity> registries = query.ToEntityArray(Allocator.Temp);
            foreach (Entity registry in registries)
                if (IsRegistryActive(entityManager, registry)) return true;
            return false;
        }

        private static NativeArray<Entity> GetRegistries<T>(EntityManager entityManager) where T : unmanaged, IBufferElementData
        {
            // Each SubScene bakes its own registry. Multiple loaded scenes are valid;
            // look up the requested prefab across their live registries.
            using EntityQuery query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<EntitySpawnRegistrySingleton>(), ComponentType.ReadOnly<T>());
            return query.ToEntityArray(Allocator.Temp);
        }

        private static bool IsRegistryActive(EntityManager entityManager, Entity registry)
        {
            if (!entityManager.HasComponent<SceneTag>(registry)) return true;
            Entity section = entityManager.GetSharedComponent<SceneTag>(registry).SceneEntity;
            if (!entityManager.Exists(section)) return false;
            // Live baking removes old scene content later than its metadata. Do not
            // instantiate prefabs from a scene whose unload has already been requested.
            if (entityManager.HasComponent<SceneEntityReference>(section))
            {
                Entity scene = entityManager.GetComponentData<SceneEntityReference>(section).SceneEntity;
                return entityManager.Exists(scene) && entityManager.HasComponent<RequestSceneLoaded>(scene);
            }
            return entityManager.HasComponent<RequestSceneLoaded>(section);
        }
    }
}
