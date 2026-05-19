using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CrystalMagic.Core {
    /// <summary>
    /// 配置表管理组件
    /// </summary>
    public class DataComponent : GameComponent<DataComponent>
    {
        private Dictionary<Type, object> _tables = new();

        public override int Priority => 11;

        public override void Initialize()
        {
            base.Initialize();
            LoadAllTables();
        }
        private void LoadAllTables()
        {
            DataTableRegistry.RegisterAll(this);
        }

        #region 接口
        /// <summary>
        /// 按 Id 获取配置行，找不到返回 null
        /// </summary>
        public T Get<T>(int id) where T : DataRow
        {
            return GetTable<T>()?.GetById(id);
        }

        /// <summary>
        /// 按条件查找第一个匹配行，找不到返回 null
        /// </summary>
        public T Find<T>(Func<T, bool> predicate) where T : DataRow
        {
            var table = GetTable<T>();
            if (table == null) return null;
            foreach (T row in table.GetAll())
            {
                if (predicate(row)) return row;
            }
            return null;
        }

        /// <summary>
        /// 按条件查找所有匹配行
        /// </summary>
        public IEnumerable<T> FindAll<T>(Func<T, bool> predicate) where T : DataRow
        {
            var table = GetTable<T>();
            if (table == null) return Enumerable.Empty<T>();
            return table.GetAll().Where(predicate);
        }

        #endregion
        /// <summary>
        /// 加载配置表，传入表名即可（如 "ItemDataTable"）
        /// 路径由 AssetPathHelper.GetDataAsset 统一生成
        /// </summary>
        public DataTable<T> LoadTable<T>(string tableName) where T : DataRow
        {
            string resourcePath = AssetPathHelper.GetDataAsset(tableName);
            TextAsset asset = ResourceComponent.Instance.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                Debug.LogError($"[DataComponent] Table not found: {resourcePath}");
                return null;
            }

            DataTable<T> table = new DataTable<T>();
            
            // 尝试用 Newtonsoft.Json 加载（支持多态）
            if (!TryLoad(asset.text, table))
            {
                // 回退到 JsonUtility（不支持多态）
                if (!table.Load(asset.text))
                    return null;
            }

            _tables[typeof(T)] = table;
            return table;
        }

        /// <summary>
        /// 用 Newtonsoft.Json 加载支持多态的表
        /// </summary>
        private bool TryLoad<T>(string json, DataTable<T> table) where T : DataRow
        {
            try
            {
                var jObject = JObject.Parse(json);
                var jArray = jObject["Rows"] as JArray;
                
                if (jArray == null) return false;

                var settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    NullValueHandling = NullValueHandling.Ignore,
                    Converters = new List<JsonConverter>
                    {
                        new UnityObjectPathJsonConverter(path => ResourceComponent.Instance.Load<UnityEngine.Object>(path)),
                    },
                };
                JsonSerializer serializer = JsonSerializer.Create(settings);

                foreach (var jRow in jArray)
                {
                    T row = jRow.ToObject<T>(serializer);
                    if (row != null)
                    {
                        table.Add(row);
                    }
                }

                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[DataComponent] Newtonsoft.Json load failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取配置表
        /// </summary>
        public DataTable<T> GetTable<T>() where T : DataRow
        {
            _tables.TryGetValue(typeof(T), out object table);
            return table as DataTable<T>;
        }
        /// <summary>
        /// 重新加载所有已注册的配置表（编辑器调试用）
        /// </summary>
        public void ReloadAll()
        {
            _tables.Clear();
            LoadAllTables();
            Debug.Log("[DataComponent] All tables reloaded");
        }

        public override void Cleanup()
        {
            _tables.Clear();
            base.Cleanup();
        }
    }
}
