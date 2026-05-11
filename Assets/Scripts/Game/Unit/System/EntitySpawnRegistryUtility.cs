using Unity.Collections;
using Unity.Entities;

namespace CrystalMagic.Game.Unit
{
    public static class EntitySpawnRegistryUtility
    {
        public static bool TryGetUnitPrefab(EntityManager entityManager, in FixedString128Bytes unitName, out Entity prefab)
        {
            if (!TryGetRegistryEntity(entityManager, out Entity registryEntity) ||
                !entityManager.HasBuffer<UnitEntityPrefabRegistryEntry>(registryEntity))
            {
                prefab = Entity.Null;
                return false;
            }

            DynamicBuffer<UnitEntityPrefabRegistryEntry> buffer = entityManager.GetBuffer<UnitEntityPrefabRegistryEntry>(registryEntity);
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Name.Equals(unitName))
                {
                    prefab = buffer[i].Prefab;
                    return prefab != Entity.Null;
                }
            }

            prefab = Entity.Null;
            return false;
        }

        public static bool TryGetProjectilePrefab(EntityManager entityManager, in FixedString128Bytes projectileName, out Entity prefab)
        {
            if (!TryGetRegistryEntity(entityManager, out Entity registryEntity) ||
                !entityManager.HasBuffer<ProjectileEntityPrefabRegistryEntry>(registryEntity))
            {
                prefab = Entity.Null;
                return false;
            }

            DynamicBuffer<ProjectileEntityPrefabRegistryEntry> buffer = entityManager.GetBuffer<ProjectileEntityPrefabRegistryEntry>(registryEntity);
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Name.Equals(projectileName))
                {
                    prefab = buffer[i].Prefab;
                    return prefab != Entity.Null;
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
            return instance != Entity.Null;
        }

        public static bool TryGetDropPrefab(EntityManager entityManager, in FixedString128Bytes dropName, out Entity prefab)
        {
            if (!TryGetRegistryEntity(entityManager, out Entity registryEntity) ||
                !entityManager.HasBuffer<DropEntityPrefabRegistryEntry>(registryEntity))
            {
                prefab = Entity.Null;
                return false;
            }

            DynamicBuffer<DropEntityPrefabRegistryEntry> buffer = entityManager.GetBuffer<DropEntityPrefabRegistryEntry>(registryEntity);
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Name.Equals(dropName))
                {
                    prefab = buffer[i].Prefab;
                    return prefab != Entity.Null;
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
            return instance != Entity.Null;
        }

        private static bool TryGetRegistryEntity(EntityManager entityManager, out Entity registryEntity)
        {
            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EntitySpawnRegistrySingleton>());
            if (query.IsEmptyIgnoreFilter)
            {
                registryEntity = Entity.Null;
                return false;
            }

            registryEntity = query.GetSingletonEntity();
            return registryEntity != Entity.Null;
        }
    }
}
