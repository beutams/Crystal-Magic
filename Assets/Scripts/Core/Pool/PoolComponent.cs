using UnityEngine;
using System.Collections.Generic;

namespace CrystalMagic.Core {
    /// <summary>
    /// 对象池管理组件
    /// 职责：通过完整路径或预制体获取/释放对象，支持自动创建对象池
    /// 
    /// 注意：使用 Get(GameObject prefab) 时，会根据预制体实例管理对象池，
    /// 不同的预制体实例会创建不同的对象池，即使名字相同
    /// </summary>
    public class PoolComponent : GameComponent<PoolComponent>
    {
        private Dictionary<string, GameObjectPool> _pools = new();
        private Dictionary<int, string> _prefabInstanceToPoolName = new();
        private Transform _poolContainer;

        public override int Priority => 12;

        public override void Initialize()
        {
            base.Initialize();

            // 创建池容器
            GameObject containerObj = new GameObject("[ObjectPoolContainer]");
            _poolContainer = containerObj.transform;
        }

        /// <summary>
        /// 从池中获取对象（按路径）
        /// 如果池不存在，自动从资源模块加载创建
        /// </summary>
        public GameObject Get(string assetPath)
        {
            // 获取或创建对象池
            if (!_pools.TryGetValue(assetPath, out GameObjectPool pool))
            {
                pool = CreatePoolFromResource(assetPath);
                if (pool == null)
                {
                    Debug.LogError($"[PoolComponent] Failed to create pool for '{assetPath}'");
                    return null;
                }
            }

            return pool.Get();
        }

        /// <summary>
        /// 从池中获取对象（使用预制体实例）
        /// 如果池不存在，会自动创建
        /// 根据预制体实例的 GetInstanceID 创建唯一的对象池
        /// 这样即使名字相同，不同的预制体实例也会有不同的对象池
        /// </summary>
        public GameObject Get(GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogError("[PoolComponent] Cannot get from null prefab");
                return null;
            }

            // 使用实例 ID 作为唯一标识符
            int prefabInstanceId = prefab.GetInstanceID();
            string poolName = _prefabInstanceToPoolName.ContainsKey(prefabInstanceId)
                ? _prefabInstanceToPoolName[prefabInstanceId]
                : GeneratePoolName(prefab, prefabInstanceId);

            // 获取或创建对象池
            if (!_pools.TryGetValue(poolName, out GameObjectPool pool))
            {
                pool = new GameObjectPool(prefab, initialSize: 0, maxSize: 10, _poolContainer);
                _pools[poolName] = pool;
                _prefabInstanceToPoolName[prefabInstanceId] = poolName;
                Debug.Log($"[PoolComponent] Auto-created pool '{poolName}' from prefab '{prefab.name}' (Instance ID: {prefabInstanceId})");
            }

            return pool.Get();
        }

        /// <summary>
        /// 生成唯一的池名称
        /// </summary>
        private string GeneratePoolName(GameObject prefab, int instanceId)
        {
            return $"{prefab.name}_{instanceId}";
        }

        /// <summary>
        /// 释放对象回到池中
        /// </summary>
        public void Release(GameObject obj)
        {
            if (obj == null)
                return;

            // 归还到池中时设置为 inactive
            obj.SetActive(false);

            string poolName = obj.name;
            if (_pools.TryGetValue(poolName, out GameObjectPool foundPool))
            {
                foundPool.Return(obj);
            }
            else
            {
                // 尝试找到包含该对象名字的第一个池
                foreach (var kvp in _pools)
                {
                    if (kvp.Key.StartsWith(poolName))
                    {
                        kvp.Value.Return(obj);
                        return;
                    }
                }

                Debug.LogWarning($"[PoolComponent] Object '{poolName}' pool not found, destroying object");
                Object.Destroy(obj);
            }
        }

        /// <summary>
        /// 销毁指定对象池
        /// </summary>
        public void DestroyPool(string poolName)
        {
            if (_pools.TryGetValue(poolName, out GameObjectPool pool))
            {
                pool.Clear();
                _pools.Remove(poolName);
                Debug.Log($"[PoolComponent] Destroyed pool: {poolName}");
            }
        }

        /// <summary>
        /// 从资源模块加载资源并创建对象池
        /// </summary>
        private GameObjectPool CreatePoolFromResource(string assetPath)
        {
            GameObject prefab = TryLoadPrefab(assetPath);

            if (prefab == null)
            {
                Debug.LogError($"[PoolComponent] Cannot find resource for '{assetPath}'");
                return null;
            }

            GameObjectPool pool = new GameObjectPool(prefab, initialSize: 0, maxSize: 10, _poolContainer);
            _pools[assetPath] = pool;

            Debug.Log($"[PoolComponent] Created pool '{assetPath}' from resource");
            return pool;
        }

        /// <summary>
        /// 尝试加载预制体
        /// </summary>
        private GameObject TryLoadPrefab(string assetPath)
        {
            var resourceComponent = ResourceComponent.Instance;
            if (resourceComponent == null)
            {
                Debug.LogError("[PoolComponent] ResourceComponent not available");
                return null;
            }

            GameObject prefab = resourceComponent.Load<GameObject>(assetPath);

            if (prefab != null)
                return prefab;

            Debug.LogWarning($"[PoolComponent] Cannot find resource for '{assetPath}'");
            return null;
        }

        /// <summary>
        /// 清空所有对象池
        /// </summary>
        public void ClearAllPools()
        {
            foreach (var pool in _pools.Values)
            {
                pool.Clear();
            }
            _pools.Clear();
            _prefabInstanceToPoolName.Clear();
            Debug.Log("[PoolComponent] Cleared all pools");
        }

        public override void Cleanup()
        {
            ClearAllPools();
            if (_poolContainer != null)
            {
                Object.Destroy(_poolContainer.gameObject);
            }
            base.Cleanup();
        }
    }
}
