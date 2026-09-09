using System;
using System.Collections;
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
    /// Config Editor
    /// 扫描所有标记 [GameConfig] 的Config Class，以 Inspector 形式展示并支持Save
    /// Menu path: Tools / Config / Config Editor
    /// </summary>
    public class ConfigEditorWindow : EditorWindow
    {
        private const string ConfigDir = "Assets/Res/Config";

        // ===== Type List =====
        private List<Type> _configTypes = new();
        private string[] _typeNames;
        private int _selectedIndex;

        // ===== Current Config =====
        private Type _loadedType;
        private object _configObj;
        private FieldInfo[] _fields;
        private bool _isDirty;
        private string _statusText = "";

        // ===== Scroll =====
        private Vector2 _scrollPos;

        // ─────────────────────────────────────────
        [MenuItem("Tools/Config/Config Editor")]
        public static void Open()
        {
            var w = GetWindow<ConfigEditorWindow>("Config Editor");
            w.minSize = new Vector2(420, 360);
            w.Show();
        }

        private void OnEnable()
        {
            ScanConfigTypes();
            if (_configTypes.Count == 0)
                return;

            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _configTypes.Count - 1);
            LoadConfig(_configTypes[_selectedIndex]);
        }

        // ─────────────────────────────────────────
        //  Scan types marked with [GameConfig]
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
                ? _configTypes.ConvertAll(t => EditorLabelUtility.GetLabel(t, t.Name)).ToArray()
                : new[] { "(No [GameConfig] types found)" };
        }

        // ─────────────────────────────────────────
        //  Load
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
            _statusText = File.Exists(path) ? $"已Load  ·  {path}" : $"Using defaults (file not found)  |  {path}";
        }

        // ─────────────────────────────────────────
        //  Save
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
            _statusText = $"已Save  ·  {path}";
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

            EditorGUILayout.LabelField("Config Class", GUILayout.Width(42));
            int newIdx = EditorGUILayout.Popup(_selectedIndex, _typeNames,
                EditorStyles.toolbarDropDown, GUILayout.Width(200));
            if (newIdx != _selectedIndex)
            {
                _selectedIndex = newIdx;
                LoadConfig(_configTypes[_selectedIndex]);
            }

            GUI.enabled = _isDirty;
            if (GUILayout.Button(_isDirty ? "Save *" : "Save",
                EditorStyles.toolbarButton, GUILayout.Width(52)))
                SaveConfig();
            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            if (!string.IsNullOrEmpty(_statusText))
                GUILayout.Label(_statusText, EditorStyles.miniLabel, GUILayout.ExpandWidth(false));

            EditorGUILayout.EndHorizontal();
        }

        // ─────────────────────────────────────────
        //  Field List (Inspector Style)
        // ─────────────────────────────────────────
        private void DrawFields()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            EditorGUILayout.Space(4);

            foreach (var field in _fields)
            {
                object val = field.GetValue(_configObj);
                object newVal = DrawField(EditorLabelUtility.GetLabel(field), field.FieldType, val, out bool changed);
                if (changed)
                {
                    field.SetValue(_configObj, newVal);
                    _isDirty = true;
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.EndScrollView();
        }

        // ─────────────────────────────────────────
        //  Field Controls
        // ─────────────────────────────────────────
        private object DrawField(string label, Type type, object value, out bool changed)
        {
            changed = false;

            if (type == typeof(int))
            {
                EditorGUI.BeginChangeCheck();
                int result = EditorGUILayout.IntField(label, (int)(value ?? 0));
                changed = EditorGUI.EndChangeCheck();
                return result;
            }
            if (type == typeof(float))
            {
                EditorGUI.BeginChangeCheck();
                float result = EditorGUILayout.FloatField(label, (float)(value ?? 0f));
                changed = EditorGUI.EndChangeCheck();
                return result;
            }
            if (type == typeof(bool))
            {
                EditorGUI.BeginChangeCheck();
                bool result = EditorGUILayout.Toggle(label, (bool)(value ?? false));
                changed = EditorGUI.EndChangeCheck();
                return result;
            }
            if (type == typeof(Vector2))
            {
                Vector2 vector = value is Vector2 current ? current : Vector2.zero;
                EditorGUI.BeginChangeCheck();
                Vector2 result = EditorGUILayout.Vector2Field(label, vector);
                changed = EditorGUI.EndChangeCheck();
                return result;
            }
            if (type == typeof(Vector2Int))
            {
                Vector2Int vector = value is Vector2Int current ? current : Vector2Int.zero;
                EditorGUI.BeginChangeCheck();
                Vector2Int result = EditorGUILayout.Vector2IntField(label, vector);
                changed = EditorGUI.EndChangeCheck();
                return result;
            }
            if (type == typeof(string))
            {
                EditorGUI.BeginChangeCheck();
                string result = EditorGUILayout.TextField(label, (string)(value ?? string.Empty));
                changed = EditorGUI.EndChangeCheck();
                return result;
            }
            if (type.IsEnum)
            {
                Enum current = value as Enum ?? (Enum)Activator.CreateInstance(type);
                EditorGUI.BeginChangeCheck();
                Enum result = EditorGUILayout.EnumPopup(label, current);
                changed = EditorGUI.EndChangeCheck();
                return result;
            }
            if (IsList(type))
                return DrawList(label, type, value as IList, out changed);
            if (CanDrawFields(type))
                return DrawObject(label, type, value, out changed);

            EditorGUILayout.LabelField(label, value?.ToString() ?? string.Empty);
            return value;
        }

        private object DrawList(string label, Type listType, IList list, out bool changed)
        {
            changed = false;
            Type elementType = listType.GetGenericArguments()[0];
            if (list == null)
            {
                list = (IList)Activator.CreateInstance(listType);
                changed = true;
            }

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            int removeIndex = -1;
            for (int i = 0; i < list.Count; i++)
            {
                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Element {i}", EditorStyles.miniBoldLabel);
                if (GUILayout.Button("Remove", GUILayout.Width(64)))
                    removeIndex = i;
                EditorGUILayout.EndHorizontal();

                object newValue = DrawField(string.Empty, elementType, list[i], out bool itemChanged);
                if (itemChanged)
                {
                    list[i] = newValue;
                    changed = true;
                }

                EditorGUILayout.EndVertical();
            }

            if (removeIndex >= 0)
            {
                list.RemoveAt(removeIndex);
                changed = true;
            }

            if (GUILayout.Button("Add"))
            {
                list.Add(CreateDefaultValue(elementType));
                changed = true;
            }

            EditorGUILayout.EndVertical();
            return list;
        }

        private object DrawObject(string label, Type type, object value, out bool changed)
        {
            changed = false;
            object instance = value ?? Activator.CreateInstance(type);
            if (value == null)
                changed = true;

            EditorGUILayout.BeginVertical(GUI.skin.box);
            if (!string.IsNullOrEmpty(label))
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.IsInitOnly)
                    continue;

                object oldValue = field.GetValue(instance);
                object newValue = DrawField(EditorLabelUtility.GetLabel(field), field.FieldType, oldValue, out bool fieldChanged);
                if (fieldChanged)
                {
                    field.SetValue(instance, newValue);
                    changed = true;
                }
            }

            EditorGUILayout.EndVertical();
            return instance;
        }

        private static bool IsList(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
        }

        private static bool CanDrawFields(Type type)
        {
            return (type.IsClass || (type.IsValueType && !type.IsPrimitive))
                && type != typeof(decimal)
                && type.Namespace != "UnityEngine";
        }

        private static object CreateDefaultValue(Type type)
        {
            return type == typeof(string) ? string.Empty : Activator.CreateInstance(type);
        }
        private static string GetFilePath(Type t) => $"{ConfigDir}/{t.Name}.json";
    }
}
