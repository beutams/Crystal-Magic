using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using CrystalMagic.Core;

namespace CrystalMagic.Editor.Config
{
    /// <summary>
    /// Config 配置编辑器
    /// 扫描所有标记 [GameConfig] 的配置类，以 Inspector 形式展示并支持保存
    /// 菜单路径：Crystal Magic / Config Editor
    /// </summary>
    public class ConfigEditorWindow : EditorWindow
    {
        private const string ConfigDir = "Assets/Res/Config";

        // ===== 类型列表 =====
        private List<Type> _configTypes = new();
        private string[] _typeNames;
        private int _selectedIndex;

        // ===== 当前配置 =====
        private Type _loadedType;
        private object _configObj;
        private FieldInfo[] _fields;
        private bool _isDirty;
        private string _statusText = "";

        // ===== 滚动 =====
        private Vector2 _scrollPos;

        // ─────────────────────────────────────────
        [MenuItem("Tools/Config/Config Editor")]
        public static void Open()
        {
            var w = GetWindow<ConfigEditorWindow>("Config Editor");
            w.minSize = new Vector2(420, 360);
            w.Show();
        }

        private void OnEnable() => ScanConfigTypes();

        // ─────────────────────────────────────────
        //  扫描带 [GameConfig] 特性的类
        // ─────────────────────────────────────────
        private void ScanConfigTypes()
        {
            _configTypes.Clear();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in asm.GetTypes())
                        if (!t.IsAbstract && !t.IsInterface
                            && t.GetCustomAttribute<GameConfigAttribute>() != null)
                            _configTypes.Add(t);
                }
                catch { }
            }

            _configTypes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            _typeNames = _configTypes.Count > 0
                ? _configTypes.ConvertAll(t => t.Name).ToArray()
                : new[] { "（未找到 [GameConfig] 类）" };
        }

        // ─────────────────────────────────────────
        //  加载
        // ─────────────────────────────────────────
        private void LoadConfig(Type type)
        {
            string path = GetFilePath(type);
            _configObj = File.Exists(path)
                ? JsonUtility.FromJson(File.ReadAllText(path), type) ?? Activator.CreateInstance(type)
                : Activator.CreateInstance(type);

            _fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            _loadedType = type;
            _isDirty = false;
            _statusText = File.Exists(path) ? $"已加载  ·  {path}" : $"使用默认值（文件不存在）  ·  {path}";
        }

        // ─────────────────────────────────────────
        //  保存
        // ─────────────────────────────────────────
        private void SaveConfig()
        {
            if (_loadedType == null || _configObj == null) return;

            string path = GetFilePath(_loadedType);
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(path, JsonUtility.ToJson(_configObj, true), Encoding.UTF8);
            AssetDatabase.Refresh();
            _isDirty = false;
            _statusText = $"已保存  ·  {path}";
            Debug.Log($"[ConfigEditor] Saved {path}");
        }

        // ─────────────────────────────────────────
        //  OnGUI
        // ─────────────────────────────────────────
        private void OnGUI()
        {
            DrawToolbar();

            if (_loadedType != null && _configObj != null && _fields != null)
                DrawFields();
            else if (!string.IsNullOrEmpty(_statusText))
                EditorGUILayout.HelpBox(_statusText, MessageType.Info);
        }

        // ─────────────────────────────────────────
        //  工具栏
        // ─────────────────────────────────────────
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUILayout.LabelField("配置类", GUILayout.Width(42));
            int newIdx = EditorGUILayout.Popup(_selectedIndex, _typeNames,
                EditorStyles.toolbarDropDown, GUILayout.Width(200));
            if (newIdx != _selectedIndex)
            {
                _selectedIndex = newIdx;
                _configObj = null;
                _loadedType = null;
                _isDirty = false;
            }

            if (GUILayout.Button("加载", EditorStyles.toolbarButton, GUILayout.Width(44))
                && _configTypes.Count > 0)
                LoadConfig(_configTypes[_selectedIndex]);

            GUI.enabled = _isDirty;
            if (GUILayout.Button(_isDirty ? "保存 *" : "保存",
                EditorStyles.toolbarButton, GUILayout.Width(52)))
                SaveConfig();
            GUI.enabled = true;

            if (GUILayout.Button("刷新类型", EditorStyles.toolbarButton, GUILayout.Width(60)))
                ScanConfigTypes();

            GUILayout.FlexibleSpace();

            if (!string.IsNullOrEmpty(_statusText))
                GUILayout.Label(_statusText, EditorStyles.miniLabel, GUILayout.ExpandWidth(false));

            EditorGUILayout.EndHorizontal();
        }

        // ─────────────────────────────────────────
        //  字段列表（Inspector 风格）
        // ─────────────────────────────────────────
        private void DrawFields()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            EditorGUILayout.Space(4);

            foreach (var field in _fields)
            {
                object val = field.GetValue(_configObj);
                object newVal = DrawField(field.Name, field.FieldType, val);
                if (!Equals(newVal, val))
                {
                    field.SetValue(_configObj, newVal);
                    _isDirty = true;
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.EndScrollView();
        }

        // ─────────────────────────────────────────
        //  字段控件
        // ─────────────────────────────────────────
        private object DrawField(string label, Type type, object value)
        {
            if (type == typeof(int))
                return EditorGUILayout.IntField(label, (int)(value ?? 0));
            if (type == typeof(float))
                return EditorGUILayout.FloatField(label, (float)(value ?? 0f));
            if (type == typeof(bool))
                return EditorGUILayout.Toggle(label, (bool)(value ?? false));
            if (type == typeof(string))
                return EditorGUILayout.TextField(label, (string)(value ?? ""));
            if (type.IsEnum)
                return EditorGUILayout.EnumPopup(label, (Enum)value);

            // 不支持的类型只读显示
            EditorGUILayout.LabelField(label, value?.ToString() ?? "—");
            return value;
        }

        private static string GetFilePath(Type t) => $"{ConfigDir}/{t.Name}.json";
    }
}
