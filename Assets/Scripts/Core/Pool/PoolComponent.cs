using System.Collections.Generic;
using CrystalMagic.Game.Config;
using UnityEngine;

namespace CrystalMagic.Core
{
    /// <summary>
    /// Manages pooled GameObject instances.
    /// </summary>
    public class PoolComponent : GameComponent<PoolComponent>
    {
        private readonly struct PoolCapacitySettings
        {
            public PoolCapacitySettings(int initialSize, int maxSize)
            {
                InitialSize = initialSize;
                MaxSize = maxSize;
            }

            public int InitialSize { get; }
            public int MaxSize { get; }
        }

        private readonly Dictionary<string, GameObjectPool> _pools = new();
        private readonly Dictionary<int, string> _prefabInstanceToPoolName = new();
        private readonly Dictionary<int, string> _objectInstanceToPoolName = new();
        private readonly Dictionary<int, string> _objectInstanceToOwnerKey = new();
        private readonly Dictionary<string, HashSet<GameObject>> _ownerObjects = new();
        private readonly Dictionary<string, GameObject> _resourcePoolPrefabs = new();

        private Transform _poolContainer;

        public override int Priority => 12;

        public override void Initialize()
        {
            base.Initialize();
            GameObject containerObj = new GameObject("[ObjectPoolContainer]");
            _poolContainer = containerObj.transform;
        }

        public GameObject Get(string assetPath)
        {
            GameObjectPool pool = GetOrCreateResourcePool(assetPath, null);
            if (pool == null)
            {
                Debug.LogError($"[PoolComponent] Failed to create pool for '{assetPath}'");
                return null;
            }

            GameObject obj = pool.Get();
            if (obj != null)
                _objectInstanceToPoolName[obj.GetInstanceID()] = assetPath;

            return obj;
        }

        public GameObject Get(string assetPath, string ownerKey)
        {
            GameObject obj = Get(assetPath);
            TrackOwnerObject(ownerKey, obj);
            return obj;
        }

        public GameObject Get(GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogError("[PoolComponent] Cannot get from null prefab");
                return null;
            }

            PoolCapacitySettings settings = ResolvePoolCapacity(prefab);
            GameObjectPool pool = GetOrCreatePrefabPool(prefab, settings, out string poolName);

            GameObject obj = pool.Get();
            if (obj != null)
                _objectInstanceToPoolName[obj.GetInstanceID()] = poolName;

            return obj;
        }

        public GameObject Get(GameObject prefab, string ownerKey)
        {
            GameObject obj = Get(prefab);
            TrackOwnerObject(ownerKey, obj);
            return obj;
        }

        public void EnsurePool(GameObject prefab, int maxSize, int initialSize = 0)
        {
            if (prefab == null)
            {
                Debug.LogError("[PoolComponent] Cannot ensure pool from null prefab");
                return;
            }

            PoolCapacitySettings settings = CreateExplicitCapacity(initialSize, maxSize);
            GetOrCreatePrefabPool(prefab, settings, out _);
        }

        public void EnsurePool(string assetPath, int maxSize, int initialSize = 0)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                Debug.LogError("[PoolComponent] Cannot ensure pool from empty asset path");
                return;
            }

            PoolCapacitySettings settings = CreateExplicitCapacity(initialSize, maxSize);
            GetOrCreateResourcePool(assetPath, settings);
        }

        public void Release(GameObject obj)
        {
            if (obj == null)
                return;

            obj.SetActive(false);
            RemoveOwnerObject(obj);

            int objectInstanceId = obj.GetInstanceID();
            if (_objectInstanceToPoolName.TryGetValue(objectInstanceId, out string mappedPoolName) &&
                _pools.TryGetValue(mappedPoolName, out GameObjectPool mappedPool))
            {
                mappedPool.Return(obj);
                return;
            }

            string poolName = obj.name;
            if (_pools.TryGetValue(poolName, out GameObjectPool foundPool))
            {
                foundPool.Return(obj);
            }
            else
            {
                foreach (KeyValuePair<string, GameObjectPool> kvp in _pools)
                {
                    if (!kvp.Key.StartsWith(poolName))
                        continue;

                    kvp.Value.Return(obj);
                    return;
                }

                Debug.LogWarning($"[PoolComponent] Object '{poolName}' pool not found, destroying object");
                Object.Destroy(obj);
            }
        }

        public void ReleaseOwner(string ownerKey)
        {
            if (string.IsNullOrWhiteSpace(ownerKey) || !_ownerObjects.TryGetValue(ownerKey, out HashSet<GameObject> objects))
                return;

            GameObject[] snapshot = new GameObject[objects.Count];
            objects.CopyTo(snapshot);
            _ownerObjects.Remove(ownerKey);

            for (int i = 0; i < snapshot.Length; i++)
                Release(snapshot[i]);
        }

        public void DestroyPool(string poolName)
        {
            if (!_pools.TryGetValue(poolName, out GameObjectPool pool))
                return;

            pool.Clear();
            _pools.Remove(poolName);

            if (_resourcePoolPrefabs.TryGetValue(poolName, out GameObject prefab))
            {
                ResourceComponent.Instance.Unload(prefab);
                _resourcePoolPrefabs.Remove(poolName);
            }

            Debug.Log($"[PoolComponent] Destroyed pool: {poolName}");
        }

        public void ClearAllPools()
        {
            foreach (GameObjectPool pool in _pools.Values)
                pool.Clear();

            foreach (GameObject prefab in _resourcePoolPrefabs.Values)
                ResourceComponent.Instance.Unload(prefab);

            _pools.Clear();
            _prefabInstanceToPoolName.Clear();
            _objectInstanceToPoolName.Clear();
            _objectInstanceToOwnerKey.Clear();
            _ownerObjects.Clear();
            _resourcePoolPrefabs.Clear();
            Debug.Log("[PoolComponent] Cleared all pools");
        }

        public override void Cleanup()
        {
            ClearAllPools();
            if (_poolContainer != null)
                Object.Destroy(_poolContainer.gameObject);
            base.Cleanup();
        }

        private GameObjectPool GetOrCreatePrefabPool(GameObject prefab, PoolCapacitySettings settings, out string poolName)
        {
            int prefabInstanceId = prefab.GetInstanceID();
            if (!_prefabInstanceToPoolName.TryGetValue(prefabInstanceId, out poolName))
            {
                poolName = GeneratePoolName(prefab, prefabInstanceId);
                _prefabInstanceToPoolName[prefabInstanceId] = poolName;
            }

            if (_pools.TryGetValue(poolName, out GameObjectPool pool))
            {
                pool.EnsureCapacity(settings.InitialSize, settings.MaxSize);
                return pool;
            }

            pool = new GameObjectPool(prefab, settings.InitialSize, settings.MaxSize, _poolContainer);
            _pools[poolName] = pool;
            Debug.Log($"[PoolComponent] Auto-created pool '{poolName}' from prefab '{prefab.name}' (Instance ID: {prefabInstanceId})");
            return pool;
        }

        private GameObjectPool GetOrCreateResourcePool(string assetPath, PoolCapacitySettings? overrideSettings)
        {
            if (_pools.TryGetValue(assetPath, out GameObjectPool pool))
            {
                if (overrideSettings.HasValue)
                    pool.EnsureCapacity(overrideSettings.Value.InitialSize, overrideSettings.Value.MaxSize);
                return pool;
            }

            GameObject prefab = TryLoadPrefab(assetPath);
            if (prefab == null)
            {
                Debug.LogError($"[PoolComponent] Cannot find resource for '{assetPath}'");
                return null;
            }

            PoolCapacitySettings settings = overrideSettings ?? ResolvePoolCapacity(prefab);
            pool = new GameObjectPool(prefab, settings.InitialSize, settings.MaxSize, _poolContainer);
            _pools[assetPath] = pool;
            _resourcePoolPrefabs[assetPath] = prefab;

            Debug.Log($"[PoolComponent] Created pool '{assetPath}' from resource");
            return pool;
        }

        private GameObject TryLoadPrefab(string assetPath)
        {
            GameObject prefab = ResourceComponent.Instance.Load<GameObject>(assetPath);
            if (prefab != null)
                return prefab;

            Debug.LogWarning($"[PoolComponent] Cannot find resource for '{assetPath}'");
            return null;
        }

        private static string GeneratePoolName(GameObject prefab, int instanceId)
        {
            return $"{prefab.name}_{instanceId}";
        }

        private static PoolCapacitySettings ResolvePoolCapacity(GameObject prefab)
        {
            PoolPreset preset = prefab != null ? prefab.GetComponent<PoolPreset>() : null;
            return CreateTierCapacity(preset != null ? preset.Tier : PoolPresetTier.Medium);
        }

        private static PoolCapacitySettings CreateTierCapacity(PoolPresetTier tier)
        {
            GameConfig config = ConfigComponent.Instance.Get<GameConfig>();
            return tier switch
            {
                PoolPresetTier.Single => new PoolCapacitySettings(0, Mathf.Max(1, config.SinglePoolMaxSize)),
                PoolPresetTier.Small => new PoolCapacitySettings(0, Mathf.Max(1, config.SmallPoolMaxSize)),
                PoolPresetTier.Large => new PoolCapacitySettings(0, Mathf.Max(1, config.LargePoolMaxSize)),
                _ => new PoolCapacitySettings(0, Mathf.Max(1, config.MediumPoolMaxSize)),
            };
        }

        private static PoolCapacitySettings CreateExplicitCapacity(int initialSize, int maxSize)
        {
            int normalizedInitialSize = Mathf.Max(0, initialSize);
            int normalizedMaxSize = Mathf.Max(1, maxSize);
            if (normalizedInitialSize > normalizedMaxSize)
                normalizedMaxSize = normalizedInitialSize;

            return new PoolCapacitySettings(normalizedInitialSize, normalizedMaxSize);
        }

        private void TrackOwnerObject(string ownerKey, GameObject obj)
        {
            if (string.IsNullOrWhiteSpace(ownerKey) || obj == null)
                return;

            int instanceId = obj.GetInstanceID();
            _objectInstanceToOwnerKey[instanceId] = ownerKey;

            if (!_ownerObjects.TryGetValue(ownerKey, out HashSet<GameObject> objects))
            {
                objects = new HashSet<GameObject>();
                _ownerObjects[ownerKey] = objects;
            }

            objects.Add(obj);
        }

        private void RemoveOwnerObject(GameObject obj)
        {
            if (obj == null)
                return;

            int instanceId = obj.GetInstanceID();
            if (!_objectInstanceToOwnerKey.TryGetValue(instanceId, out string ownerKey))
                return;

            _objectInstanceToOwnerKey.Remove(instanceId);
            if (!_ownerObjects.TryGetValue(ownerKey, out HashSet<GameObject> objects))
                return;

            objects.Remove(obj);
            if (objects.Count == 0)
                _ownerObjects.Remove(ownerKey);
        }
    }
}
