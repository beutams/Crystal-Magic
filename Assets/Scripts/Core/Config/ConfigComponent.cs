using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 全局配置管理组件
    /// 职责：按需加载、缓存各类配置对象
    /// JSON 文件放在 Assets/Res/Config/ 目录下，文件名约定为 {TypeName}.json
    /// 文件不存在时使用默认值并自动创建文件
    /// </summary>
    public class ConfigComponent : GameComponent<ConfigComponent>
    {
        private Dictionary<Type, object> _configs = new();

        // ResourceComponent(5) 之后，DataComponent(10) 之前
        public override int Priority => 8;

        /// <summary>
        /// 获取配置对象，首次访问时从 JSON 加载
        /// </summary>
        public T Get<T>() where T : class, new()
        {
            if (_configs.TryGetValue(typeof(T), out object cached))
                return (T)cached;

            return Load<T>();
        }

        /// <summary>
        /// 强制重新从文件加载指定配置
        /// </summary>
        public T Reload<T>() where T : class, new()
        {
            _configs.Remove(typeof(T));
            return Load<T>();
        }

        /// <summary>
        /// 将指定配置写回 JSON 文件
        /// </summary>
        public void Save<T>(T config) where T : class, new()
        {
            if (config == null) return;
            _configs[typeof(T)] = config;

            string path = GetFilePath(typeof(T));
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(path, JsonUtility.ToJson(config, true),
                System.Text.Encoding.UTF8);
            Debug.Log($"[ConfigComponent] Saved {path}");
        }

        private T Load<T>() where T : class, new()
        {
            T result;
            string path = GetFilePath(typeof(T));
            TextAsset asset = ResourceComponent.Instance.Load<TextAsset>(path);

            if (asset != null)
            {
                result = JsonUtility.FromJson<T>(asset.text) ?? new T();
                Debug.Log($"[ConfigComponent] Loaded {path}");
            }
            else
            {
                result = new T();
                Debug.LogWarning($"[ConfigComponent] {path} not found, using defaults");
            }

            _configs[typeof(T)] = result;
            return result;
        }

        private static string GetFilePath(Type t) => AssetPathHelper.GetConfigAsset(t.Name);

        public override void Cleanup()
        {
            _configs.Clear();
            base.Cleanup();
        }
    }
}
