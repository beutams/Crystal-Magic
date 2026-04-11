using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using CrystalMagic.Core;
using System.IO;

namespace CrystalMagic.Editor
{
    /// <summary>
    /// UI 配置编辑器窗口
    /// </summary>
    public class UIConfigWindow : EditorWindow
    {
        private const string ConfigPath = "Assets/Config/ui_config.json";
        private const string DefaultConfigName = "ui_config.json";

        private UIGroupConfig _config;
        private Vector2 _scrollPosition;
        private int _selectedGroupIndex = -1;
        private bool _isDirty = false;

        [MenuItem("Tools/Config/UI Config")]
        public static void ShowWindow()
        {
            GetWindow<UIConfigWindow>("UI Config");
        }

        private void OnEnable()
        {
            LoadConfig();
        }

        private void OnGUI()
        {
            GUILayout.Label("UI 分组配置", EditorStyles.largeLabel);

            EditorGUILayout.Space();

            // 文件操作按钮
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("新建配置", GUILayout.Width(100)))
            {
                CreateNewConfig();
            }
            if (GUILayout.Button("加载配置", GUILayout.Width(100)))
            {
                LoadConfig();
            }
            if (GUILayout.Button("保存配置", GUILayout.Width(100)))
            {
                SaveConfig();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            if (_config == null)
            {
                EditorGUILayout.HelpBox("未加载配置，请点击【加载配置】或【新建配置】", MessageType.Info);
                return;
            }

            // 分组列表
            EditorGUILayout.LabelField("分组列表", EditorStyles.boldLabel);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            for (int i = 0; i < _config.groups.Count; i++)
            {
                DrawGroupEntry(i);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            // 添加分组按钮
            if (GUILayout.Button("+ 添加分组", GUILayout.Height(30)))
            {
                _config.groups.Add(new UIGroupEntry());
                _isDirty = true;
            }

            // 删除分组按钮
            if (_selectedGroupIndex >= 0 && _selectedGroupIndex < _config.groups.Count)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("删除选中分组", GUILayout.Height(30)))
                {
                    _config.groups.RemoveAt(_selectedGroupIndex);
                    _selectedGroupIndex = -1;
                    _isDirty = true;
                }
                EditorGUILayout.EndHorizontal();
            }

            // 提示
            if (_isDirty)
            {
                EditorGUILayout.HelpBox("配置已修改，请点击【保存配置】保存", MessageType.Warning);
            }
        }

        private void DrawGroupEntry(int index)
        {
            UIGroupEntry entry = _config.groups[index];

            EditorGUILayout.BeginVertical("box");

            // 分组标题栏
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button($"▶ 分组 {index}: {entry.groupName}", EditorStyles.toolbarButton, GUILayout.Height(25)))
            {
                _selectedGroupIndex = (_selectedGroupIndex == index) ? -1 : index;
            }
            EditorGUILayout.EndHorizontal();

            // 展开内容
            if (_selectedGroupIndex == index)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("分组名称", GUILayout.Width(80));
                string newName = EditorGUILayout.TextField(entry.groupName);
                if (newName != entry.groupName)
                {
                    entry.groupName = newName;
                    _isDirty = true;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("分组类型", GUILayout.Width(80));
                UIGroupType newType = (UIGroupType)EditorGUILayout.EnumPopup(entry.groupType);
                if (newType != entry.groupType)
                {
                    entry.groupType = newType;
                    _isDirty = true;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("排序", GUILayout.Width(80));
                int newOrder = EditorGUILayout.IntField(entry.order);
                if (newOrder != entry.order)
                {
                    entry.order = newOrder;
                    _isDirty = true;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField("UI 列表", EditorStyles.boldLabel);

                // UI 列表
                for (int i = 0; i < entry.uiNames.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"UI {i}", GUILayout.Width(50));
                    string newUiName = EditorGUILayout.TextField(entry.uiNames[i]);
                    if (newUiName != entry.uiNames[i])
                    {
                        entry.uiNames[i] = newUiName;
                        _isDirty = true;
                    }
                    if (GUILayout.Button("删除", GUILayout.Width(50)))
                    {
                        entry.uiNames.RemoveAt(i);
                        _isDirty = true;
                    }
                    EditorGUILayout.EndHorizontal();
                }

                // 添加 UI 按钮
                if (GUILayout.Button("+ 添加 UI", GUILayout.Height(25)))
                {
                    entry.uiNames.Add("");
                    _isDirty = true;
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void CreateNewConfig()
        {
            _config = new UIGroupConfig();
            _config.groups.Add(new UIGroupEntry
            {
                groupName = "Default",
                groupType = UIGroupType.Stack,
                order = 0,
                uiNames = new List<string>()
            });
            _isDirty = true;
        }

        private void LoadConfig()
        {
            TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigPath);
            if (textAsset != null)
            {
                _config = UIConfigLoader.LoadFromJson(textAsset.text);
                _isDirty = false;
                Debug.Log($"[UIConfig] Loaded {ConfigPath}");
            }
            else
            {
                Debug.LogWarning($"[UIConfig] File not found: {ConfigPath}");
            }
        }

        private void SaveConfig()
        {
            if (_config == null) return;

            string directory = Path.GetDirectoryName(ConfigPath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string json = UIConfigLoader.SaveToJson(_config);
            File.WriteAllText(ConfigPath, json);

            AssetDatabase.Refresh();
            _isDirty = false;
            Debug.Log($"[UIConfig] Saved {ConfigPath}");
        }
    }
}
