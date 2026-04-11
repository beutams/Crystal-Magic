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

                    T[] instances = FindObjectsOfType<T>();

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

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
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
