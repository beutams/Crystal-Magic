using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Data
{
    public partial class DungeonEditorWindow : EditorWindow
    {
        private const string ThemeDataPath = "Assets/Res/Data/DungeonThemeDataTable.json";
        private const float ListPanelWidth = 240f;

        private sealed class ThemeTableWrapper
        {
            public List<DungeonThemeData> Rows = new();
        }

        private sealed class IntOption
        {
            public int Id;
            public string Label;
        }

        private sealed class StringOption
        {
            public string Value;
            public string Label;
        }

        private static JsonSerializerSettings JsonSettings => new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
        };

        private readonly List<DungeonThemeData> _themes = new();
        private readonly Dictionary<string, bool> _foldoutStates = new();
        private int _selectedThemeIndex = -1;
        private Vector2 _listScrollPosition;
        private Vector2 _detailScrollPosition;
        private bool _isDirty;
        private string _statusText = string.Empty;

        [MenuItem("Tools/Data/Dungeon Editor")]
        public static void Open()
        {
            DungeonEditorWindow window = GetWindow<DungeonEditorWindow>("Dungeon Editor");
            window.minSize = new Vector2(1080f, 620f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadThemes();
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.BeginHorizontal();
            DrawThemeList();
            DrawDivider();
            DrawThemeDetail();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUI.enabled = _isDirty;
            if (GUILayout.Button(_isDirty ? "Save *" : "Save", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                SaveThemes();
            GUI.enabled = true;

            if (GUILayout.Button("Add", EditorStyles.toolbarButton, GUILayout.Width(52f)))
                AddTheme();

            GUI.enabled = GetSelectedTheme() != null;
            if (GUILayout.Button("Copy", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                DuplicateTheme();
            if (GUILayout.Button("Delete", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                DeleteTheme();
            GUI.enabled = true;

            GUILayout.FlexibleSpace();
            GUILayout.Label("Open Field Dungeon", EditorStyles.miniBoldLabel);
            if (!string.IsNullOrWhiteSpace(_statusText))
                GUILayout.Label(_statusText, EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawThemeList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(ListPanelWidth), GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField($"Themes ({_themes.Count})", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Select a 1-10 floor theme to edit its open-field settings.", EditorStyles.wordWrappedMiniLabel);
            _listScrollPosition = EditorGUILayout.BeginScrollView(_listScrollPosition);
            for (int i = 0; i < _themes.Count; i++)
            {
                DungeonThemeData theme = _themes[i];
                if (theme == null)
                    continue;

                string label = $"[{theme.Id}] {theme.Name} ({theme.FloorStart}-{theme.FloorEnd})";
                if (GUILayout.Toggle(i == _selectedThemeIndex, label, "Button") && _selectedThemeIndex != i)
                {
                    EditorFocusUtility.ClearTextFocus();
                    _selectedThemeIndex = i;
                    _detailScrollPosition = Vector2.zero;
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawThemeDetail()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            _detailScrollPosition = EditorGUILayout.BeginScrollView(_detailScrollPosition);
            DungeonThemeData theme = GetSelectedTheme();
            if (theme == null)
            {
                EditorGUILayout.HelpBox("Select or add a dungeon theme to configure the open-field generator.", MessageType.Info);
            }
            else
            {
                DrawThemeOverview(theme);
                DrawOpenFieldDetailPanel();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawThemeOverview(DungeonThemeData theme)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Theme", EditorStyles.boldLabel);
            theme.Name = EditorGUILayout.TextField("Name", theme.Name ?? string.Empty);
            theme.ThemeKey = EditorGUILayout.TextField("Theme Key", theme.ThemeKey ?? string.Empty);
            EditorGUILayout.BeginHorizontal();
            theme.FloorStart = Mathf.Max(1, EditorGUILayout.IntField("Floor Start", theme.FloorStart));
            theme.FloorEnd = Mathf.Max(theme.FloorStart, EditorGUILayout.IntField("Floor End", theme.FloorEnd));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            if (EditorGUI.EndChangeCheck())
            {
                theme.EnsureValid();
                _isDirty = true;
            }
        }

        private void AddTheme()
        {
            int id = GetNextId(_themes.Select(static theme => theme.Id));
            DungeonThemeData theme = new()
            {
                Id = id,
                Name = $"Theme {id}",
                ThemeKey = $"theme_{id:D2}",
                FloorStart = id * 10 + 1,
                FloorEnd = id * 10 + 10,
            };
            theme.EnsureValid();
            _themes.Add(theme);
            _selectedThemeIndex = _themes.Count - 1;
            _isDirty = true;
        }

        private void DuplicateTheme()
        {
            DungeonThemeData source = GetSelectedTheme();
            if (source == null)
                return;

            DungeonThemeData copy = DeepCopy(source);
            copy.Id = GetNextId(_themes.Select(static theme => theme.Id));
            copy.Name = $"{copy.Name} Copy";
            copy.ThemeKey = $"{copy.ThemeKey}_copy";
            copy.EnsureValid();
            _themes.Add(copy);
            _selectedThemeIndex = _themes.Count - 1;
            _isDirty = true;
        }

        private void DeleteTheme()
        {
            if (_selectedThemeIndex < 0 || _selectedThemeIndex >= _themes.Count)
                return;

            _themes.RemoveAt(_selectedThemeIndex);
            _selectedThemeIndex = Mathf.Clamp(_selectedThemeIndex, -1, _themes.Count - 1);
            _isDirty = true;
        }

        private void LoadThemes()
        {
            _themes.Clear();
            _selectedThemeIndex = -1;
            _isDirty = false;
            try
            {
                ThemeTableWrapper wrapper = LoadWrapper<ThemeTableWrapper>(ThemeDataPath);
                if (wrapper?.Rows != null)
                {
                    _themes.AddRange(wrapper.Rows.Where(static theme => theme != null));
                    foreach (DungeonThemeData theme in _themes)
                        theme.EnsureValid();
                }

                _statusText = $"Loaded {_themes.Count} themes";
            }
            catch (Exception exception)
            {
                _statusText = $"Load failed: {exception.Message}";
                Debug.LogError($"[DungeonEditor] Load error:\n{exception}");
            }
        }

        private void SaveThemes()
        {
            try
            {
                foreach (DungeonThemeData theme in _themes)
                    theme?.EnsureValid();

                SaveWrapper(ThemeDataPath, new ThemeTableWrapper { Rows = _themes });
                AssetDatabase.Refresh();
                _isDirty = false;
                _statusText = $"Saved {_themes.Count} themes";
            }
            catch (Exception exception)
            {
                _statusText = $"Save failed: {exception.Message}";
                Debug.LogError($"[DungeonEditor] Save error:\n{exception}");
            }
        }

        private DungeonThemeData GetSelectedTheme()
        {
            return _selectedThemeIndex >= 0 && _selectedThemeIndex < _themes.Count
                ? _themes[_selectedThemeIndex]
                : null;
        }

        private string GetSectionFoldoutKey(DungeonThemeData theme, string section, int index)
        {
            return $"{theme?.Id ?? -1}:{section}:{index}";
        }

        private bool DrawSectionFoldout(string key, string label)
        {
            _foldoutStates.TryGetValue(key, out bool expanded);
            bool nextExpanded = EditorGUILayout.Foldout(expanded, label, true);
            _foldoutStates[key] = nextExpanded;
            return nextExpanded;
        }

        private static List<IntOption> BuildItemOptions()
        {
            List<IntOption> options = new()
            {
                new IntOption { Id = -1, Label = "None" },
            };
            foreach (ItemData row in EditorComponents.Data.FindAll<ItemData>(static _ => true).OrderBy(static row => row.Id))
            {
                options.Add(new IntOption
                {
                    Id = row.Id,
                    Label = $"[{row.Id}] {row.Name}",
                });
            }

            return options;
        }

        private static List<StringOption> BuildUnitOptions()
        {
            List<StringOption> options = new()
            {
                new StringOption { Value = string.Empty, Label = "None" },
            };
            foreach (UnitData row in EditorComponents.Data.FindAll<UnitData>(static _ => true).OrderBy(static row => row.Id))
            {
                options.Add(new StringOption
                {
                    Value = row.Name ?? string.Empty,
                    Label = $"[{row.Id}] {row.Name}",
                });
            }

            return options;
        }

        private static int DrawIntPopup(string label, int currentId, List<IntOption> options)
        {
            int selectedIndex = Mathf.Max(0, options.FindIndex(option => option.Id == currentId));
            int nextIndex = EditorGUILayout.Popup(label, selectedIndex, options.Select(static option => option.Label).ToArray());
            return nextIndex >= 0 && nextIndex < options.Count ? options[nextIndex].Id : currentId;
        }

        private static string DrawStringPopup(string label, string currentValue, List<StringOption> options)
        {
            int selectedIndex = Mathf.Max(0, options.FindIndex(option => string.Equals(option.Value, currentValue, StringComparison.Ordinal)));
            int nextIndex = EditorGUILayout.Popup(label, selectedIndex, options.Select(static option => option.Label).ToArray());
            return nextIndex >= 0 && nextIndex < options.Count ? options[nextIndex].Value : currentValue;
        }

        private static T LoadWrapper<T>(string dataPath) where T : class, new()
        {
            if (!File.Exists(dataPath))
                return new T();

            string json = DataFileUtility.ReadJsonText(dataPath);
            return JsonConvert.DeserializeObject<T>(json, JsonSettings) ?? new T();
        }

        private static void SaveWrapper<T>(string dataPath, T wrapper)
        {
            string directory = Path.GetDirectoryName(dataPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            DataFileUtility.WriteJsonText(dataPath, JsonConvert.SerializeObject(wrapper, JsonSettings));
        }

        private static T DeepCopy<T>(T source)
        {
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(source, JsonSettings), JsonSettings);
        }

        private static int GetNextId(IEnumerable<int> ids)
        {
            int nextId = 0;
            foreach (int id in ids)
                nextId = Mathf.Max(nextId, id + 1);
            return nextId;
        }

        private static void DrawDivider()
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));
        }
    }
}
