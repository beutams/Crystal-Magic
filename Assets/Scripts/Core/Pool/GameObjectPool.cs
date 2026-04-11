using UnityEngine;
using System;
using System.Collections.Generic;

namespace CrystalMagic.Core {
    /// <summary>
    /// GameObject 对象池
    /// 专用于管理 GameObject 实例
    /// </summary>
    public class GameObjectPool : IObjectPool<GameObject>
    {
        private ObjectPool<GameObject> _pool;
        private Transform _container;
        private string _prefabName;
        private GameObject _prefab;

        public int Count => _pool.Count;
        public int InUseCount => _pool.InUseCount;
        public int AvailableCount => _pool.AvailableCount;

        /// <summary>
        /// 创建 GameObject 对象池
        /// </summary>
        public GameObjectPool(GameObject prefab, int initialSize = 0, int maxSize = 10, 
            Transform container = null)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));

            _prefab = prefab;
            _prefabName = prefab.name;

            // 创建容器
            if (container == null)
            {
                GameObject containerObj = new GameObject($"[Pool_{_prefabName}]");
                container = containerObj.transform;
            }
            _container = container;

            // 创建对象池
            _pool = new ObjectPool<GameObject>(
                creator: CreateGameObject,
                initialSize: initialSize,
                maxSize: maxSize,
                onGet: OnGetGameObject,
                onReturn: OnReturnGameObject
            );
        }

        /// <summary>
        /// 创建 GameObject
        /// </summary>
        private GameObject CreateGameObject()
        {
            GameObject obj = GameObject.Instantiate(_prefab, _container);
            obj.name = _prefabName;
            obj.SetActive(false);
            return obj;
        }

        /// <summary>
        /// GameObject 被获取时的处理
        /// </summary>
        private void OnGetGameObject(GameObject obj)
        {
            obj.SetActive(true);
        }

        /// <summary>
        /// GameObject 被归还时的处理
        /// </summary>
        private void OnReturnGameObject(GameObject obj)
        {
            obj.SetActive(false);
        }

        /// <summary>
        /// 获取 GameObject
        /// </summary>
        public GameObject Get()
        {
            return _pool.Get();
        }

        /// <summary>
        /// 归还 GameObject
        /// </summary>
        public void Return(GameObject obj)
        {
            _pool.Return(obj);
        }

        /// <summary>
        /// 清空池
        /// </summary>
        public void Clear()
        {
            _pool.Clear();
            if (_container != null)
            {
                GameObject.Destroy(_container.gameObject);
            }
        }
    }
}
