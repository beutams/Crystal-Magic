using UnityEngine;
using System;
using System.Collections.Generic;

namespace CrystalMagic.Core {
    /// <summary>
    /// 泛型对象池实现
    /// </summary>
    public class ObjectPool<T> : IObjectPool<T> where T : class
    {
        private Stack<T> _available;
        private HashSet<T> _inUse;
        private Func<T> _creator;
        private Action<T> _onGet;
        private Action<T> _onReturn;

        private int _maxSize;
        private int _initialSize;

        public int Count => _available.Count;

        /// <summary>
        /// 创建对象池
        /// </summary>
        /// <param name="creator">对象创建函数</param>
        /// <param name="initialSize">初始大小</param>
        /// <param name="maxSize">最大大小</param>
        /// <param name="onGet">获取对象时的回调</param>
        /// <param name="onReturn">归还对象时的回调</param>
        public ObjectPool(Func<T> creator, int initialSize = 0, int maxSize = 10, 
            Action<T> onGet = null, Action<T> onReturn = null)
        {
            _creator = creator ?? throw new System.ArgumentNullException(nameof(creator));
            _initialSize = Mathf.Max(1, initialSize);
            _maxSize = Mathf.Max(_initialSize, maxSize);
            _onGet = onGet;
            _onReturn = onReturn;

            _available = new Stack<T>(_initialSize);
            _inUse = new HashSet<T>();

            // 预创建对象
            for (int i = 0; i < _initialSize; i++)
            {
                T obj = _creator();
                _available.Push(obj);
            }
        }

        /// <summary>
        /// 获取对象
        /// </summary>
        public T Get()
        {
            T obj;

            if (_available.Count > 0)
            {
                obj = _available.Pop();
            }
            else if (_inUse.Count < _maxSize)
            {
                obj = _creator();
            }
            else
            {
                Debug.LogWarning($"[ObjectPool] Pool full! Max size: {_maxSize}");
                return null;
            }

            _inUse.Add(obj);

            // 如果实现了 IPoolable 接口，调用其方法
            if (obj is IPoolable poolable)
            {
                poolable.OnGetFromPool();
            }

            _onGet?.Invoke(obj);

            return obj;
        }

        /// <summary>
        /// 归还对象
        /// </summary>
        public void Return(T obj)
        {
            if (obj == null)
                return;

            if (!_inUse.Remove(obj))
            {
                Debug.LogWarning("[ObjectPool] Object not in pool, skipping return");
                return;
            }

            // 如果实现了 IPoolable 接口，调用其方法
            if (obj is IPoolable poolable)
            {
                poolable.OnReturnToPool();
            }

            _onReturn?.Invoke(obj);

            _available.Push(obj);
        }

        /// <summary>
        /// 清空池
        /// </summary>
        public void Clear()
        {
            _available.Clear();
            _inUse.Clear();
        }

        /// <summary>
        /// 获取使用中的对象数量
        /// </summary>
        public int InUseCount => _inUse.Count;

        /// <summary>
        /// 获取池中可用对象数量
        /// </summary>
        public int AvailableCount => _available.Count;
    }
}
