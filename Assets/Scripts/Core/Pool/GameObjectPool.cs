using System;
using UnityEngine;

namespace CrystalMagic.Core
{
    /// <summary>
    /// Dedicated pool for GameObject instances.
    /// </summary>
    public class GameObjectPool : IObjectPool<GameObject>
    {
        private readonly ObjectPool<GameObject> _pool;
        private readonly Transform _container;
        private readonly string _prefabName;
        private readonly GameObject _prefab;

        public GameObjectPool(GameObject prefab, int initialSize = 0, int maxSize = 10, Transform container = null)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));

            _prefab = prefab;
            _prefabName = prefab.name;

            GameObject containerObj = new GameObject($"[Pool_{_prefabName}]");
            if (container != null)
                containerObj.transform.SetParent(container, false);

            _container = containerObj.transform;
            _pool = new ObjectPool<GameObject>(
                creator: CreateGameObject,
                initialSize: initialSize,
                maxSize: maxSize,
                onGet: OnGetGameObject,
                onReturn: OnReturnGameObject);
        }

        public int Count => _pool.Count;
        public int InUseCount => _pool.InUseCount;
        public int AvailableCount => _pool.AvailableCount;
        public int MaxSize => _pool.MaxSize;
        public int TotalCount => _pool.TotalCount;

        private GameObject CreateGameObject()
        {
            GameObject obj = GameObject.Instantiate(_prefab, _container);
            obj.name = _prefabName;
            obj.SetActive(false);
            return obj;
        }

        private static void OnGetGameObject(GameObject obj)
        {
            obj.SetActive(true);
        }

        private static void OnReturnGameObject(GameObject obj)
        {
            obj.SetActive(false);
        }

        public GameObject Get()
        {
            return _pool.Get();
        }

        public void Return(GameObject obj)
        {
            _pool.Return(obj);
        }

        public void EnsureCapacity(int initialSize, int maxSize)
        {
            _pool.EnsureCapacity(initialSize, maxSize);
        }

        public void Clear()
        {
            _pool.Clear();
            if (_container != null)
                GameObject.Destroy(_container.gameObject);
        }
    }
}
