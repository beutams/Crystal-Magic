using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CrystalMagic.Core
{
    /// <summary>
    /// 全局配置组件。
    /// </summary>
    public class ConfigComponent : GameComponent<ConfigComponent>
    {
        private readonly Dictionary<Type, object> _configs = new();

        public override int Priority => 8;

        public override void Initialize()
        {
            base.Initialize();
            LoadAllConfigs();
        }

        /// <summary>
        /// 获取初始化时已加载的配置对象。
        /// </summary>
        public T Get<T>() where T : class, new()
        {
            if (_configs.TryGetValue(typeof(T), out object cached))
                return (T)cached;

            Debug.LogWarning($"[ConfigComponent] Config {typeof(T).Name} was not preloaded, using defaults.");
            T config = new T();
            _configs[typeof(T)] = config;
            return config;
        }

        private void LoadAllConfigs()
        {
            List<Type> configTypes = CollectConfigTypes();
            for (int i = 0; i < configTypes.Count; i++)
            {
                Type configType = configTypes[i];
                _configs[configType] = Load(configType);
            }
        }

        private static List<Type> CollectConfigTypes()
        {
            List<Type> result = new();
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
            {
                Assembly assembly = assemblies[assemblyIndex];
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }
                catch
                {
                    continue;
                }

                for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    Type type = types[typeIndex];
                    if (type == null || type.IsAbstract || type.IsInterface)
                        continue;

                    if (type.GetCustomAttributes(typeof(GameConfigAttribute), false).Length > 0)
                        result.Add(type);
                }
            }

            result.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
            return result;
        }

        private static object Load(Type type)
        {
            string path = AssetPathHelper.GetConfigAsset(type.Name);
            TextAsset asset = ResourceComponent.Instance.Load<TextAsset>(path);

            if (asset != null)
            {
                object config = JsonUtility.FromJson(asset.text, type) ?? Activator.CreateInstance(type);
                Debug.Log($"[ConfigComponent] Loaded {path}");
                return config;
            }

            Debug.LogWarning($"[ConfigComponent] {path} not found, using defaults");
            return Activator.CreateInstance(type);
        }

        public override void Cleanup()
        {
            _configs.Clear();
            base.Cleanup();
        }
    }
}
