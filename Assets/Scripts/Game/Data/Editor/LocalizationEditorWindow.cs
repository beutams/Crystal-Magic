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
    public sealed class LocalizationEditorWindow : EditorWindow
    {
        private const string DataPath = "Assets/Res/Data/LocalizationDataTable.json";
        private const float ListPanelWidth = 300f;
        private const float LabelWidth = 145f;

        private sealed class TableWrapper
        {
            public List<LocalizationData> Rows = new();
        }

        private readonly List<LocalizationData> _rows = new();
        private int _selectedIndex = -1;
        private bool _isDirty;
        private string _statusText = string.Empty;
        private string _searchText = string.Empty;
        private Vector2 _listScrollPosition;
        private Vector2 _detailScrollPosition;

        [MenuItem("Tools/Data/Localization Editor")]
        public static void Open()
        {
            LocalizationEditorWindow window = GetWindow<LocalizationEditorWindow>("Localization Editor");
            window.minSize = new Vector2(900f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadData();
        }

        private void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.BeginHorizontal();
            DrawListPanel();
            DrawDivider();
            DrawDetailPanel();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUI.enabled = _isDirty;
            if (GUILayout.Button(_isDirty ? "Save *" : "Save", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                SaveData();
            GUI.enabled = true;

            if (GUILayout.Button("Add", EditorStyles.toolbarButton, GUILayout.Width(52f)))
                AddRow();

            GUI.enabled = GetSelectedRow() != null;
            if (GUILayout.Button("Copy", EditorStyles.toolbarButton, GUILayout.Width(52f)))
                CopySelectedRow();
            if (GUILayout.Button("Delete", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                DeleteSelectedRow();
            GUI.enabled = true;

            GUILayout.FlexibleSpace();
            GUILayout.Label("Search", GUILayout.Width(42f));
            _searchText = GUILayout.TextField(_searchText, EditorStyles.toolbarSearchField, GUILayout.Width(180f));
            if (!string.IsNullOrWhiteSpace(_statusText))
                GUILayout.Label(_statusText, EditorStyles.miniLabel, GUILayout.ExpandWidth(false));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(ListPanelWidth), GUILayout.ExpandHeight(true));
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"Localization ({GetVisibleRowCount()}/{_rows.Count})", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _listScrollPosition = EditorGUILayout.BeginScrollView(_listScrollPosition);
            for (int i = 0; i < _rows.Count; i++)
            {
                LocalizationData row = _rows[i];
                if (!MatchesSearch(row))
                    continue;

                string label = $"[{row.Id}] {row.Key}";
                if (GUILayout.Toggle(i == _selectedIndex, label, "Button") && _selectedIndex != i)
                {
                    EditorFocusUtility.ClearTextFocus();
                    _selectedIndex = i;
                    _detailScrollPosition = Vector2.zero;
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawDetailPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            LocalizationData row = GetSelectedRow();
            if (row == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Create or select a localization entry from the left list.", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"[{row.Id}] {row.Key}", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _detailScrollPosition = EditorGUILayout.BeginScrollView(_detailScrollPosition);
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = LabelWidth;
            EditorGUI.BeginChangeCheck();

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.IntField("Id", row.Id);

            row.Key = EditorGUILayout.TextField("Key", row.Key ?? string.Empty);
            EditorGUILayout.LabelField("Chinese Simplified");
            row.ChineseSimplified = EditorGUILayout.TextArea(row.ChineseSimplified ?? string.Empty, GUILayout.MinHeight(100f));
            EditorGUILayout.LabelField("English");
            row.English = EditorGUILayout.TextArea(row.English ?? string.Empty, GUILayout.MinHeight(100f));

            if (EditorGUI.EndChangeCheck())
                _isDirty = true;

            EditorGUIUtility.labelWidth = previousLabelWidth;
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void AddRow()
        {
            _rows.Add(new LocalizationData
            {
                Id = GetNextId(),
                Key = "new.localization.key",
                ChineseSimplified = string.Empty,
                English = string.Empty,
            });
            _selectedIndex = _rows.Count - 1;
            _isDirty = true;
        }

        private void CopySelectedRow()
        {
            LocalizationData source = GetSelectedRow();
            if (source == null)
                return;

            LocalizationData copy = JsonConvert.DeserializeObject<LocalizationData>(JsonConvert.SerializeObject(source));
            if (copy == null)
                return;

            copy.Id = GetNextId();
            copy.Key = $"{copy.Key}.copy";
            _rows.Add(copy);
            _selectedIndex = _rows.Count - 1;
            _isDirty = true;
        }

        private void DeleteSelectedRow()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
                return;

            _rows.RemoveAt(_selectedIndex);
            _selectedIndex = Mathf.Min(_selectedIndex, _rows.Count - 1);
            _isDirty = true;
        }

        private void LoadData()
        {
            _rows.Clear();
            _selectedIndex = -1;
            _isDirty = false;

            try
            {
                if (File.Exists(DataPath))
                {
                    TableWrapper table = JsonConvert.DeserializeObject<TableWrapper>(DataFileUtility.ReadJsonText(DataPath));
                    if (table?.Rows != null)
                        _rows.AddRange(table.Rows.Where(static row => row != null));
                }

                _statusText = $"Loaded {_rows.Count} rows";
            }
            catch (Exception exception)
            {
                _statusText = $"Load failed: {exception.Message}";
                Debug.LogError($"[LocalizationEditor] Load failed:\n{exception}");
            }
        }

        private void SaveData()
        {
            try
            {
                string directory = Path.GetDirectoryName(DataPath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                DataFileUtility.WriteJsonText(DataPath, JsonConvert.SerializeObject(new TableWrapper { Rows = _rows }, Formatting.Indented));
                AssetDatabase.Refresh();
                _isDirty = false;
                _statusText = $"Saved {_rows.Count} rows";
            }
            catch (Exception exception)
            {
                _statusText = $"Save failed: {exception.Message}";
                Debug.LogError($"[LocalizationEditor] Save failed:\n{exception}");
            }
        }

        private LocalizationData GetSelectedRow()
        {
            return _selectedIndex >= 0 && _selectedIndex < _rows.Count ? _rows[_selectedIndex] : null;
        }

        private bool MatchesSearch(LocalizationData row)
        {
            if (row == null || string.IsNullOrWhiteSpace(_searchText))
                return row != null;

            return Contains(row.Key) || Contains(row.ChineseSimplified) || Contains(row.English);
        }

        private int GetVisibleRowCount()
        {
            return _rows.Count(MatchesSearch);
        }

        private bool Contains(string value)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private int GetNextId()
        {
            return _rows.Count == 0 ? 0 : _rows.Max(static row => row.Id) + 1;
        }

        private static void DrawDivider()
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));
        }
    }
}
