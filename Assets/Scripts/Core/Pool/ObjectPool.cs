using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Core
{
    /// <summary>
    /// Generic object pool implementation.
    /// </summary>
    public class ObjectPool<T> : IObjectPool<T> where T : class
    {
        private readonly Stack<T> _available;
        private readonly HashSet<T> _inUse;
        private readonly Func<T> _creator;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onReturn;

        private int _maxSize;

        public ObjectPool(
            Func<T> creator,
            int initialSize = 0,
            int maxSize = 10,
            Action<T> onGet = null,
            Action<T> onReturn = null)
        {
            _creator = creator ?? throw new ArgumentNullException(nameof(creator));
            _onGet = onGet;
            _onReturn = onReturn;

            int normalizedInitialSize = Mathf.Max(0, initialSize);
            _maxSize = Mathf.Max(normalizedInitialSize, maxSize);

            _available = new Stack<T>(normalizedInitialSize);
            _inUse = new HashSet<T>();

            for (int i = 0; i < normalizedInitialSize; i++)
            {
                T obj = _creator();
                _available.Push(obj);
            }
        }

        public int Count => _available.Count;
        public int InUseCount => _inUse.Count;
        public int AvailableCount => _available.Count;
        public int MaxSize => _maxSize;
        public int TotalCount => _available.Count + _inUse.Count;

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

            if (obj is IPoolable poolable)
                poolable.OnGetFromPool();

            _onGet?.Invoke(obj);
            return obj;
        }

        public void Return(T obj)
        {
            if (obj == null)
                return;

            if (!_inUse.Remove(obj))
            {
                Debug.LogWarning("[ObjectPool] Object not in pool, skipping return");
                return;
            }

            if (obj is IPoolable poolable)
                poolable.OnReturnToPool();

            _onReturn?.Invoke(obj);
            _available.Push(obj);
        }

        public void EnsureCapacity(int initialSize, int maxSize)
        {
            int normalizedInitialSize = Mathf.Max(0, initialSize);
            int normalizedMaxSize = Mathf.Max(normalizedInitialSize, maxSize);

            if (normalizedMaxSize > _maxSize)
                _maxSize = normalizedMaxSize;

            while (TotalCount < normalizedInitialSize && TotalCount < _maxSize)
            {
                T obj = _creator();
                _available.Push(obj);
            }
        }

        public void Clear()
        {
            _available.Clear();
            _inUse.Clear();
        }
    }
}
