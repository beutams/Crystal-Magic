using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using CrystalMagic.Core;

namespace CrystalMagic.Editor.Data
{
    /// <summary>
    /// 配置表编辑器
    /// 支持查看、新增、修改、删除，并将结果保存回 JSON 文件
    /// 菜单路径：Crystal Magic / Data Table Viewer
    /// </summary>
    public class DataTableViewerWindow : EditorWindow
    {
        // ===== 类型列表 =====
        private List<Type> _rowTypes = new();
        private string[] _typeNames;
        private int _selectedTypeIndex;

        // ===== 当前表数据 =====
        private Type _loadedType;
        private FieldInfo[] _fields;
        private List<object> _rows = new();
        private bool _isDirty;
        private string _statusText = "";

        // ===== 滚动 =====
        private Vector2 _scrollPos;

        // ===== 搜索 =====
        private string _searchText = "";

        // ===== 列宽 =====
        private const float DeleteBtnWidth = 24f;
        private const float RowHeight = 20f;
        private const float MinColWidth = 80f;

        // ===== JSON 包装（供反射序列化用）=====
        [Serializable]
        private class TableWrapper<T> { public List<T> Rows = new(); }

        // ─────────────────────────────────────────
        [MenuItem("Tools/Data/Data Table Viewer")]
        public static void Open()
        {
            var w = GetWindow<DataTableViewerWindow>("Data Table Viewer");
            w.minSize = new Vector2(640, 420);
            w.Show();
        }

        private void OnEnable() => ScanRowTypes();

        // ─────────────────────────────────────────
        //  扫描 DataRow 子类
        // ─────────────────────────────────────────
        private void ScanRowTypes()
        {
            _rowTypes.Clear();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (t.IsAbstract || t.IsInterface
                            || !typeof(DataRow).IsAssignableFrom(t) || t == typeof(DataRow))
                            continue;
                        if (Attribute.IsDefined(t, typeof(ReadOnlyDataAttribute), inherit: false))
                            continue;
                        _rowTypes.Add(t);
                    }
                }
                catch { }
            }
            _rowTypes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            _typeNames = _rowTypes.Count > 0
                ? _rowTypes.ConvertAll(t => t.Name).ToArray()
                : new[] { "（未找到 DataRow 子类）" };
        }

        // ─────────────────────────────────────────
        //  文件路径
        // ─────────────────────────────────────────
        private string GetFilePath(Type t) =>
            $"Assets/Res/Data/{t.Name}Table.json";

        // ─────────────────────────────────────────
        //  加载
        // ─────────────────────────────────────────
        private void LoadTable(Type rowType)
        {
            _rows.Clear();
            _fields = null;
            _loadedType = null;
            _isDirty = false;
            _statusText = "";

            string path = GetFilePath(rowType);
            string json = File.Exists(path) ? File.ReadAllText(path) : null;

            if (!string.IsNullOrEmpty(json))
            {
                Type wrapperType = typeof(TableWrapper<>).MakeGenericType(rowType);
                object wrapper = JsonUtility.FromJson(json, wrapperType);
                IList rowList = wrapperType.GetField("Rows").GetValue(wrapper) as IList;
                if (rowList != null)
                    foreach (object r in rowList) _rows.Add(r);
            }

            _fields = rowType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            _loadedType = rowType;
            _statusText = $"已加载 {_rows.Count} 条  ·  {path}";
        }

        // ─────────────────────────────────────────
        //  保存
        // ─────────────────────────────────────────
        private void SaveTable()
        {
            if (_loadedType == null) return;

            string path = GetFilePath(_loadedType);
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            // 构造 typed list 供 JsonUtility 序列化
            Type listType = typeof(List<>).MakeGenericType(_loadedType);
            IList typedList = (IList)Activator.CreateInstance(listType);
            foreach (object r in _rows) typedList.Add(r);

            Type wrapperType = typeof(TableWrapper<>).MakeGenericType(_loadedType);
            object wrapper = Activator.CreateInstance(wrapperType);
            wrapperType.GetField("Rows").SetValue(wrapper, typedList);

            File.WriteAllText(path, JsonUtility.ToJson(wrapper, true), Encoding.UTF8);
            AssetDatabase.Refresh();

            _isDirty = false;
            _statusText = $"已保存 {_rows.Count} 条  ·  {path}";
            Debug.Log($"[DataTableViewer] Saved {path}");
        }

        // ─────────────────────────────────────────
        //  新增行
        // ─────────────────────────────────────────
        private void AddRow()
        {
            if (_loadedType == null) return;
            object newRow = Activator.CreateInstance(_loadedType);

            // Id 自动取最大值 + 1
            FieldInfo idField = _loadedType.GetField("Id");
            if (idField != null)
            {
                int maxId = 0;
                foreach (object r in _rows)
                {
                    int id = (int)idField.GetValue(r);
                    if (id > maxId) maxId = id;
                }
                idField.SetValue(newRow, maxId + 1);
            }

            foreach (FieldInfo f in _loadedType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.FieldType == typeof(int[]))
                    f.SetValue(newRow, Array.Empty<int>());
                else if (f.FieldType == typeof(float[]))
                    f.SetValue(newRow, Array.Empty<float>());
            }

            _rows.Add(newRow);
            _isDirty = true;
        }

        // ─────────────────────────────────────────
        //  OnGUI
        // ─────────────────────────────────────────
        private void OnGUI()
        {
            DrawToolbar();
            if (_loadedType != null && _fields != null)
                DrawTable();
            else if (!string.IsNullOrEmpty(_statusText))
                EditorGUILayout.HelpBox(_statusText, MessageType.Info);
        }

        // ─────────────────────────────────────────
        //  工具栏
        // ─────────────────────────────────────────
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUILayout.LabelField("表类型", GUILayout.Width(42));
            int newIdx = EditorGUILayout.Popup(_selectedTypeIndex, _typeNames,
                EditorStyles.toolbarDropDown, GUILayout.Width(180));
            if (newIdx != _selectedTypeIndex)
            {
                _selectedTypeIndex = newIdx;
                _rows.Clear();
                _loadedType = null;
                _isDirty = false;
            }

            if (GUILayout.Button("加载", EditorStyles.toolbarButton, GUILayout.Width(44))
                && _rowTypes.Count > 0)
                LoadTable(_rowTypes[_selectedTypeIndex]);

            GUI.enabled = _isDirty;
            if (GUILayout.Button(_isDirty ? "保存 *" : "保存", EditorStyles.toolbarButton, GUILayout.Width(52)))
                SaveTable();
            GUI.enabled = true;

            if (GUILayout.Button("+ 新增行", EditorStyles.toolbarButton, GUILayout.Width(62))
                && _loadedType != null)
                AddRow();

            if (GUILayout.Button("刷新类型", EditorStyles.toolbarButton, GUILayout.Width(60)))
                ScanRowTypes();

            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField("搜索", GUILayout.Width(30));
            _searchText = EditorGUILayout.TextField(_searchText,
                EditorStyles.toolbarSearchField, GUILayout.Width(160));

            if (!string.IsNullOrEmpty(_statusText))
                GUILayout.Label(_statusText, EditorStyles.miniLabel, GUILayout.ExpandWidth(false));

            EditorGUILayout.EndHorizontal();
        }

        // ─────────────────────────────────────────
        //  表格
        // ─────────────────────────────────────────
        private void DrawTable()
        {
            float totalWidth = position.width - DeleteBtnWidth - 20f;
            float colWidth = Mathf.Max(MinColWidth, totalWidth / _fields.Length);

            // ── 表头 ──
            EditorGUILayout.BeginHorizontal(GUI.skin.box);
            foreach (var f in _fields)
                EditorGUILayout.LabelField(f.Name, EditorStyles.boldLabel,
                    GUILayout.Width(colWidth), GUILayout.Height(RowHeight));
            GUILayout.Label("", GUILayout.Width(DeleteBtnWidth)); // 占位
            EditorGUILayout.EndHorizontal();

            // ── 数据行 ──
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            int deleteIndex = -1;
            for (int i = 0; i < _rows.Count; i++)
            {
                object row = _rows[i];
                if (!string.IsNullOrEmpty(_searchText) && !RowMatchesSearch(row))
                    continue;

                Color bg = i % 2 == 0
                    ? new Color(0.22f, 0.22f, 0.22f)
                    : new Color(0.28f, 0.28f, 0.28f);

                EditorGUILayout.BeginHorizontal(MakeColorStyle(bg));

                foreach (var field in _fields)
                {
                    object val = field.GetValue(row);
                    object newVal = DrawField(field.FieldType, val, colWidth);
                    if (!Equals(newVal, val))
                    {
                        field.SetValue(row, newVal);
                        _isDirty = true;
                    }
                }

                // 删除按钮
                GUI.color = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(DeleteBtnWidth)))
                    deleteIndex = i;
                GUI.color = Color.white;

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            if (deleteIndex >= 0)
            {
                _rows.RemoveAt(deleteIndex);
                _isDirty = true;
            }
        }

        // ─────────────────────────────────────────
        //  字段编辑控件
        // ─────────────────────────────────────────
        private object DrawField(Type type, object value, float width)
        {
            var w = GUILayout.Width(width);
            var h = GUILayout.Height(RowHeight);

            if (type == typeof(int))
                return EditorGUILayout.IntField((int)(value ?? 0), w, h);
            if (type == typeof(float))
                return EditorGUILayout.FloatField((float)(value ?? 0f), w, h);
            if (type == typeof(bool))
                return EditorGUILayout.Toggle((bool)(value ?? false), w, h);
            if (type == typeof(string))
                return EditorGUILayout.TextField((string)(value ?? ""), w, h);
            if (type.IsEnum)
                return EditorGUILayout.EnumPopup((Enum)value, w, h);

            if (type == typeof(int[]))
            {
                int[] arr = (int[])value;
                string joined = JoinIntArray(arr);
                string edited = EditorGUILayout.DelayedTextField(joined, w, h);
                if (!string.Equals(edited, joined, StringComparison.Ordinal))
                {
                    if (TryParseIntArray(edited, out int[] parsed))
                        return parsed;
                    Debug.LogWarning($"[DataTableViewer] 无法解析 int[]，保持原值: \"{edited}\"");
                }
                return value;
            }

            if (type == typeof(float[]))
            {
                float[] arr = (float[])value;
                string joined = JoinFloatArray(arr);
                string edited = EditorGUILayout.DelayedTextField(joined, w, h);
                if (!string.Equals(edited, joined, StringComparison.Ordinal))
                {
                    if (TryParseFloatArray(edited, out float[] parsed))
                        return parsed;
                    Debug.LogWarning($"[DataTableViewer] 无法解析 float[]，保持原值: \"{edited}\"");
                }
                return value;
            }

            // 不支持的类型只读显示
            EditorGUILayout.LabelField(value?.ToString() ?? "—", w, h);
            return value;
        }

        private static string JoinIntArray(int[] arr)
        {
            if (arr == null || arr.Length == 0) return "";
            return string.Join(",", Array.ConvertAll(arr, x => x.ToString()));
        }

        private static string JoinFloatArray(float[] arr)
        {
            if (arr == null || arr.Length == 0) return "";
            return string.Join(",", Array.ConvertAll(arr, x => x.ToString(CultureInfo.InvariantCulture)));
        }

        private static bool TryParseIntArray(string s, out int[] result)
        {
            result = Array.Empty<int>();
            if (string.IsNullOrWhiteSpace(s))
                return true;

            string[] parts = s.Split(',');
            var list = new List<int>();
            foreach (string part in parts)
            {
                string t = part.Trim();
                if (t.Length == 0) continue;
                if (!int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
                    return false;
                list.Add(n);
            }

            result = list.ToArray();
            return true;
        }

        private static bool TryParseFloatArray(string s, out float[] result)
        {
            result = Array.Empty<float>();
            if (string.IsNullOrWhiteSpace(s))
                return true;

            string[] parts = s.Split(',');
            var list = new List<float>();
            foreach (string part in parts)
            {
                string t = part.Trim();
                if (t.Length == 0) continue;
                if (!float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                    return false;
                list.Add(f);
            }

            result = list.ToArray();
            return true;
        }

        // ─────────────────────────────────────────
        //  辅助
        // ─────────────────────────────────────────
        private bool RowMatchesSearch(object row)
        {
            foreach (var f in _fields)
            {
                string v = FieldValueToSearchString(f.FieldType, f.GetValue(row));
                if (v.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static string FieldValueToSearchString(Type fieldType, object val)
        {
            if (val == null) return "";
            if (fieldType == typeof(int[])) return JoinIntArray((int[])val);
            if (fieldType == typeof(float[])) return JoinFloatArray((float[])val);
            return val.ToString();
        }

        private static GUIStyle MakeColorStyle(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            var style = new GUIStyle();
            style.normal.background = tex;
            return style;
        }
    }
}
