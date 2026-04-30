using UnityEngine;

namespace CrystalMagic.Core
{
    /// <summary>
    /// 泛型单例基类（MonoBehaviour 版本）
    /// </summary>
    public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
    {
        private static T _instance;
        private static readonly object _lockObject = new object();

        protected bool InitializeSingletonInstance(T instance)
        {
            if (_instance == null)
            {
                _instance = instance;
                DontDestroyOnLoad(gameObject);
                return true;
            }

            if (_instance != instance)
            {
                Destroy(gameObject);
                return false;
            }

            return true;
        }

        public static T Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                lock (_lockObject)
                {
                    if (_instance != null)
                        return _instance;

                    T[] instances = FindObjectsByType<T>(FindObjectsSortMode.None);

                    if (instances.Length > 1)
                    {
                        Debug.LogError($"[Singleton] Multiple instances of {typeof(T).Name} found! Expected 1, but found {instances.Length}");
                        for (int i = 1; i < instances.Length; i++)
                        {
                            Destroy(instances[i].gameObject);
                        }
                    }

                    if (instances.Length == 1)
                    {
                        _instance = instances[0];
                        return _instance;
                    }

                    // 没有找到实例，返回 null（不自动创建）
                    Debug.LogWarning($"[Singleton] {typeof(T).Name} instance not found in scene. Please attach it to a GameObject.");
                    return null;
                }
            }
        }

        public static bool TryGetInstance(out T instance)
        {
            if (_instance != null)
            {
                instance = _instance;
                return true;
            }

            lock (_lockObject)
            {
                if (_instance != null)
                {
                    instance = _instance;
                    return true;
                }

                T[] instances = FindObjectsByType<T>(FindObjectsSortMode.None);
                if (instances.Length > 1)
                {
                    Debug.LogError($"[Singleton] Multiple instances of {typeof(T).Name} found! Expected 1, but found {instances.Length}");
                    for (int i = 1; i < instances.Length; i++)
                    {
                        Destroy(instances[i].gameObject);
                    }
                }

                if (instances.Length > 0)
                {
                    _instance = instances[0];
                    instance = _instance;
                    return true;
                }

                instance = null;
                return false;
            }
        }

        protected virtual void Awake()
        {
            InitializeSingletonInstance(this as T);
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
        public static void DestroyInstance()
        {
            if (_instance != null)
            {
                Destroy(_instance.gameObject);
            }
        }
    }

    /// <summary>
    /// 非 MonoBehaviour 版本的泛型单例基类
    /// </summary>
    public abstract class SingletonNonMono<T> where T : SingletonNonMono<T>, new()
    {
        private static T _instance;
        private static readonly object _lockObject = new object();

        public static T Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                lock (_lockObject)
                {
                    if (_instance != null)
                        return _instance;

                    _instance = new T();
                    _instance.Initialize();
                }

                return _instance;
            }
        }

        /// <summary>
        /// 初始化方法，子类可以重写
        /// </summary>
        protected virtual void Initialize() { }

        /// <summary>
        /// 清空单例
        /// </summary>
        public static void Clear()
        {
            _instance = null;
        }
    }
}
