using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 配置表容器
    /// 从 JSON 文件加载，以 Id 为键索引所有行
    /// JSON 格式：{ "Rows": [ { "Id": 1001, ... }, ... ] }
    /// </summary>
    public class DataTable<T> where T : DataRow
    {
        private Dictionary<int, T> _dict = new();

        // JsonUtility 不支持泛型数组根节点，用包装类中转
        [System.Serializable]
        private class TableWrapper
        {
            public List<T> Rows = new();
        }

        public bool Load(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError($"[DataTable<{typeof(T).Name}>] Empty json string");
                return false;
            }

            TableWrapper wrapper = JsonUtility.FromJson<TableWrapper>(json);
            if (wrapper?.Rows == null)
            {
                Debug.LogError($"[DataTable<{typeof(T).Name}>] Failed to parse json");
                return false;
            }

            _dict.Clear();
            foreach (T row in wrapper.Rows)
            {
                if (_dict.ContainsKey(row.Id))
                {
                    Debug.LogWarning($"[DataTable<{typeof(T).Name}>] Duplicate Id: {row.Id}, skipped");
                    continue;
                }
                _dict[row.Id] = row;
            }

            Debug.Log($"[DataTable<{typeof(T).Name}>] Loaded {_dict.Count} rows");
            return true;
        }

        public T GetById(int id)
        {
            _dict.TryGetValue(id, out T row);
            return row;
        }

        public bool TryGet(int id, out T row) => _dict.TryGetValue(id, out row);

        public IEnumerable<T> GetAll() => _dict.Values;

        public int Count => _dict.Count;

        /// <summary>
        /// 添加一行（供 Newtonsoft.Json 加载时使用）
        /// </summary>
        public void Add(T row)
        {
            if (row == null) return;
            if (_dict.ContainsKey(row.Id))
            {
                Debug.LogWarning($"[DataTable<{typeof(T).Name}>] Duplicate Id: {row.Id}, skipped");
                return;
            }
            _dict[row.Id] = row;
        }
    }
}
