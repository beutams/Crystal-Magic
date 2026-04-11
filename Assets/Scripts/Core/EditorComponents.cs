using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Core
{
    /// <summary>
    /// 编辑器端统一组件访问入口（Baker / EditorWindow 专用）
    /// 运行时各组件为 MonoBehaviour 单例，烘焙期尚未初始化，用此类替代。
    ///
    /// 对称关系：
    ///   EditorComponents.Resource  ↔  ResourceComponent.Instance
    ///   EditorComponents.Data      ↔  DataComponent.Instance
    ///   EditorComponents.Config    ↔  ConfigComponent.Instance
    /// </summary>
    public static class EditorComponents
    {
        private static readonly IResourceLoader _loader = new EditorResourceLoader();

        // ────────────────────────────────────────────────────────────
        //  Resource
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// 对应 ResourceComponent.Instance
        /// </summary>
        public static class Resource
        {
            /// <summary>
            /// 对应 ResourceComponent.Instance.Load&lt;T&gt;(path)
            /// </summary>
            public static T Load<T>(string path) where T : UnityEngine.Object
            {
                T asset = _loader.Load<T>(path);
                if (asset == null)
                    Debug.LogWarning($"[EditorComponents.Resource] 找不到资源: {path}");
                return asset;
            }
        }

        // ────────────────────────────────────────────────────────────
        //  Data
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// 对应 DataComponent.Instance
        /// 表名约定：{TypeName}Table，与 DataTableRegistryGenerator 保持一致
        /// </summary>
        public static class Data
        {
            /// <summary>
            /// 对应 DataComponent.Instance.Get&lt;T&gt;(id)
            /// </summary>
            public static T Get<T>(int id) where T : DataRow
            {
                return LoadTable<T>()?.GetById(id);
            }

            /// <summary>
            /// 对应 DataComponent.Instance.Find&lt;T&gt;(predicate)
            /// </summary>
            public static T Find<T>(Func<T, bool> predicate) where T : DataRow
            {
                DataTable<T> table = LoadTable<T>();
                if (table == null) return null;

                foreach (T row in table.GetAll())
                {
                    if (predicate(row)) return row;
                }
                return null;
            }

            /// <summary>
            /// 对应 DataComponent.Instance.FindAll&lt;T&gt;(predicate)
            /// </summary>
            public static IEnumerable<T> FindAll<T>(Func<T, bool> predicate) where T : DataRow
            {
                DataTable<T> table = LoadTable<T>();
                if (table == null) yield break;

                foreach (T row in table.GetAll())
                {
                    if (predicate(row)) yield return row;
                }
            }

            private static DataTable<T> LoadTable<T>() where T : DataRow
            {
                string tableName = typeof(T).Name + "Table";
                string path = AssetPathHelper.GetDataAsset(tableName);
                TextAsset textAsset = _loader.Load<TextAsset>(path);

                if (textAsset == null)
                {
                    Debug.LogError($"[EditorComponents.Data] 找不到配置表: {path}");
                    return null;
                }

                DataTable<T> table = new DataTable<T>();
                table.Load(textAsset.text);
                return table;
            }
        }

        // ────────────────────────────────────────────────────────────
        //  Config
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// 对应 ConfigComponent.Instance
        /// </summary>
        public static class Config
        {
            /// <summary>
            /// 对应 ConfigComponent.Instance.Get&lt;T&gt;()
            /// 文件路径：Assets/Res/Config/{TypeName}.json
            /// </summary>
            public static T Get<T>() where T : class, new()
            {
                string path = AssetPathHelper.GetConfigAsset(typeof(T).Name);
                TextAsset textAsset = _loader.Load<TextAsset>(path);

                if (textAsset == null)
                {
                    Debug.LogWarning($"[EditorComponents.Config] {path} 不存在，使用默认值");
                    return new T();
                }

                return JsonUtility.FromJson<T>(textAsset.text) ?? new T();
            }
        }
    }
}
