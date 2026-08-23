using System;
using System.Collections.Generic;
using System.IO;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Data
{
    public sealed class SkillAdditionEditorWindow : EditorWindow
    {
        private const string DataPath = "Assets/Res/Data/SkillAdditionDataTable.json";
        private static readonly JsonSerializerSettings s_jsonSettings = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
        };

        private readonly List<SkillAdditionData> _rows = new();
        private Vector2 _rowScrollPosition;
        private Vector2 _jsonScrollPosition;
        private int _selectedIndex = -1;
        private string _selectedRowJson = string.Empty;
        private string _status = string.Empty;

        private sealed class TableWrapper
        {
            public List<SkillAdditionData> Rows = new();
        }

        [MenuItem("Tools/Data/Skill Addition Editor")]
        public static void Open()
        {
            SkillAdditionEditorWindow window = GetWindow<SkillAdditionEditorWindow>("Skill Addition Editor");
            window.minSize = new Vector2(820f, 500f);
            window.Show();
        }

        private void OnEnable()
        {
            Load();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Reload", EditorStyles.toolbarButton))
                Load();
            if (GUILayout.Button("Add", EditorStyles.toolbarButton))
                AddRow();
            using (new EditorGUI.DisabledScope(_selectedIndex < 0))
            {
                if (GUILayout.Button("Delete", EditorStyles.toolbarButton))
                    DeleteSelectedRow();
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Save", EditorStyles.toolbarButton))
                Save();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawRowList();
            DrawDetail();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(_status))
                EditorGUILayout.HelpBox(_status, MessageType.Info);
        }

        private void DrawRowList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(260f));
            EditorGUILayout.LabelField("Additions", EditorStyles.boldLabel);
            _rowScrollPosition = EditorGUILayout.BeginScrollView(_rowScrollPosition);
            for (int i = 0; i < _rows.Count; i++)
            {
                SkillAdditionData row = _rows[i];
                string label = $"[{row.Id}] {row.NameKey}";
                if (GUILayout.Toggle(_selectedIndex == i, label, "Button"))
                    Select(i);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawDetail()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
            {
                EditorGUILayout.HelpBox("Select or add an Addition. The old Followup/CastTask model is not supported.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            SkillAdditionData row = _rows[_selectedIndex];
            EditorGUI.BeginChangeCheck();
            row.NameKey = EditorGUILayout.TextField("Name Key", row.NameKey);
            row.DescriptionKey = EditorGUILayout.TextField("Description Key", row.DescriptionKey);
            row.IconPath = EditorGUILayout.TextField("Icon Path", row.IconPath);
            if (EditorGUI.EndChangeCheck())
                RefreshSelectedJson();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Callbacks", EditorStyles.boldLabel);
            int callbackCount = row.Callbacks?.Count ?? 0;
            EditorGUILayout.LabelField($"{callbackCount} callback(s). Callback actions use generated runtime action types.");
            if (GUILayout.Button("Add Empty Callback", GUILayout.Width(150f)))
            {
                row.Callbacks ??= new List<SkillAdditionCallbackData>();
                row.Callbacks.Add(new SkillAdditionCallbackData());
                RefreshSelectedJson();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Edit the selected row JSON below to configure callback conditions and actions. " +
                "Supported action data: ModifyCurrentSkill, SetSourceValue, ExecuteEffects, ReplayCurrentSkill.",
                MessageType.None);
            _jsonScrollPosition = EditorGUILayout.BeginScrollView(_jsonScrollPosition, GUILayout.ExpandHeight(true));
            _selectedRowJson = EditorGUILayout.TextArea(_selectedRowJson, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply Row JSON"))
                ApplySelectedJson();
            if (GUILayout.Button("Revert JSON"))
                RefreshSelectedJson();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void Load()
        {
            _rows.Clear();
            _selectedIndex = -1;
            _selectedRowJson = string.Empty;
            try
            {
                if (File.Exists(DataPath))
                {
                    TableWrapper wrapper = JsonConvert.DeserializeObject<TableWrapper>(DataFileUtility.ReadJsonText(DataPath), s_jsonSettings);
                    if (wrapper?.Rows != null)
                        _rows.AddRange(wrapper.Rows);
                }

                NormalizeIds();
                _status = $"Loaded {_rows.Count} row(s).";
            }
            catch (Exception exception)
            {
                _status = $"Load failed: {exception.Message}";
                Debug.LogException(exception);
            }
        }

        private void Save()
        {
            try
            {
                NormalizeIds();
                string directory = Path.GetDirectoryName(DataPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                DataFileUtility.WriteJsonText(DataPath, JsonConvert.SerializeObject(new TableWrapper { Rows = _rows }, s_jsonSettings));
                AssetDatabase.Refresh();
                RefreshSelectedJson();
                _status = $"Saved {_rows.Count} row(s).";
            }
            catch (Exception exception)
            {
                _status = $"Save failed: {exception.Message}";
                Debug.LogException(exception);
            }
        }

        private void AddRow()
        {
            _rows.Add(new SkillAdditionData
            {
                Id = _rows.Count,
                NameKey = $"skill_addition.new_{_rows.Count}.name",
                Callbacks = new List<SkillAdditionCallbackData>(),
            });
            Select(_rows.Count - 1);
        }

        private void DeleteSelectedRow()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
                return;

            _rows.RemoveAt(_selectedIndex);
            NormalizeIds();
            _selectedIndex = -1;
            _selectedRowJson = string.Empty;
        }

        private void Select(int index)
        {
            if (index < 0 || index >= _rows.Count)
                return;

            _selectedIndex = index;
            RefreshSelectedJson();
        }

        private void ApplySelectedJson()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
                return;

            try
            {
                SkillAdditionData replacement = JsonConvert.DeserializeObject<SkillAdditionData>(_selectedRowJson, s_jsonSettings);
                if (replacement == null)
                    throw new JsonSerializationException("The row JSON is empty.");

                replacement.Callbacks ??= new List<SkillAdditionCallbackData>();
                replacement.Id = _rows[_selectedIndex].Id;
                _rows[_selectedIndex] = replacement;
                RefreshSelectedJson();
                _status = "Applied row JSON.";
            }
            catch (Exception exception)
            {
                _status = $"Row JSON is invalid: {exception.Message}";
            }
        }

        private void RefreshSelectedJson()
        {
            _selectedRowJson = _selectedIndex >= 0 && _selectedIndex < _rows.Count
                ? JsonConvert.SerializeObject(_rows[_selectedIndex], s_jsonSettings)
                : string.Empty;
        }

        private void NormalizeIds()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i] ??= new SkillAdditionData();
                _rows[i].Id = i;
                _rows[i].Callbacks ??= new List<SkillAdditionCallbackData>();
            }
        }
    }
}
