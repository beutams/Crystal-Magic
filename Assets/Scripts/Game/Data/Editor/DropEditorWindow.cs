using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Data
{
    public class DropEditorWindow : EditorWindow
    {
        private const string DataPath = "Assets/Res/Data/DropDataTable.json";
        private const float ListPanelWidth = 220f;
        private const float ItemHeight = 26f;

        private sealed class TableWrapper
        {
            public List<DropData> Rows = new();
        }

        private sealed class ItemOption
        {
            public int Id;
            public string Label;
        }

        private static readonly Color SelectedColor = new(0.27f, 0.52f, 0.85f, 0.85f);
        private static readonly Color EvenRowColor = new(0.22f, 0.22f, 0.22f, 1f);
        private static readonly Color OddRowColor = new(0.25f, 0.25f, 0.25f, 1f);
        private static readonly Color HoverColor = new(0.32f, 0.32f, 0.32f, 1f);
        private static readonly Color DividerColor = new(0.15f, 0.15f, 0.15f, 1f);

        private static JsonSerializerSettings JsonSettings => new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
        };

        private readonly List<DropData> _rows = new();
        private Vector2 _listScrollPos;
        private Vector2 _detailScrollPos;
        private int _selectedIndex = -1;
        private bool _isDirty;
        private string _statusText = string.Empty;

        [MenuItem("Tools/Data/Drop Editor")]
        public static void Open()
        {
            DropEditorWindow window = GetWindow<DropEditorWindow>("Drop Editor");
            window.minSize = new Vector2(920f, 560f);
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
            DrawPanelDivider();
            DrawDetailPanel();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(44f)))
                LoadData();

            GUI.enabled = _isDirty;
            if (GUILayout.Button(_isDirty ? "Save *" : "Save", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                SaveData();
            GUI.enabled = true;

            if (GUILayout.Button("Add", EditorStyles.toolbarButton, GUILayout.Width(44f)))
                AddRow();

            if (_selectedIndex >= 0)
            {
                if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                    DuplicateSelected();
                if (GUILayout.Button("Delete", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                    DeleteSelected();
            }

            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(_statusText))
                GUILayout.Label(_statusText, EditorStyles.miniLabel, GUILayout.ExpandWidth(false));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(ListPanelWidth), GUILayout.ExpandHeight(true));
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"Drops ({_rows.Count})", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _listScrollPos = EditorGUILayout.BeginScrollView(_listScrollPos, GUILayout.ExpandHeight(true));
            Event currentEvent = Event.current;

            for (int i = 0; i < _rows.Count; i++)
            {
                DropData row = _rows[i];
                bool isSelected = i == _selectedIndex;
                Rect itemRect = GUILayoutUtility.GetRect(ListPanelWidth, ItemHeight, GUILayout.ExpandWidth(true));

                Color backgroundColor = isSelected
                    ? SelectedColor
                    : itemRect.Contains(currentEvent.mousePosition)
                        ? HoverColor
                        : i % 2 == 0
                            ? EvenRowColor
                            : OddRowColor;
                EditorGUI.DrawRect(itemRect, backgroundColor);

                string label = $"[{row.Id}] {row.Name}";
                GUI.Label(
                    new Rect(itemRect.x + 8f, itemRect.y + 4f, itemRect.width - 16f, itemRect.height - 4f),
                    label,
                    isSelected ? EditorStyles.whiteLabel : EditorStyles.label);

                if (currentEvent.type == EventType.MouseDown && itemRect.Contains(currentEvent.mousePosition))
                {
                    _selectedIndex = i;
                    currentEvent.Use();
                    Repaint();
                }

                if (currentEvent.type == EventType.MouseMove)
                    Repaint();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawPanelDivider()
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, DividerColor);
        }

        private void DrawDetailPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Select a drop table on the left.", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            DropData row = _rows[_selectedIndex];
            _detailScrollPos = EditorGUILayout.BeginScrollView(_detailScrollPos);

            EditorGUI.BeginChangeCheck();
            row.Id = EditorGUILayout.IntField("Id", row.Id);
            row.Name = EditorGUILayout.TextField("Name", row.Name ?? string.Empty);
            EditorGUILayout.LabelField("Description");
            row.Description = EditorGUILayout.TextArea(row.Description ?? string.Empty, GUILayout.MinHeight(48f), GUILayout.MaxHeight(80f));

            GUILayout.Space(8f);
            EditorGUILayout.LabelField("Entries", EditorStyles.boldLabel);
            row.Entries ??= new List<DropEntryData>();
            List<ItemOption> itemOptions = BuildItemOptions();
            for (int i = 0; i < row.Entries.Count; i++)
            {
                DropEntryData entry = row.Entries[i] ?? new DropEntryData();
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Entry {i + 1}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Delete", GUILayout.Width(56f)))
                {
                    row.Entries.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                entry.DropType = (DropRewardType)EditorGUILayout.EnumPopup("Drop Type", entry.DropType);
                if (entry.DropType == DropRewardType.Money)
                {
                    EditorGUILayout.HelpBox("Money icon is configured in GameConfig.", MessageType.Info);
                }
                else
                {
                    entry.ItemId = DrawItemPopup("Item", entry.ItemId, itemOptions);
                }
                entry.Chance = EditorGUILayout.Slider("Chance", entry.Chance, 0f, 1f);
                entry.MinQuantity = Mathf.Max(0, EditorGUILayout.IntField("Min Quantity", entry.MinQuantity));
                entry.MaxQuantity = Mathf.Max(entry.MinQuantity, EditorGUILayout.IntField("Max Quantity", entry.MaxQuantity));
                row.Entries[i] = entry;
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("Add Entry", GUILayout.Width(96f)))
                row.Entries.Add(new DropEntryData());

            if (EditorGUI.EndChangeCheck())
            {
                row.EnsureValid();
                _isDirty = true;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void AddRow()
        {
            int nextId = _rows.Count == 0 ? 1 : _rows.Max(row => row.Id) + 1;
            DropData row = new DropData
            {
                Id = nextId,
                Name = $"Drop {nextId}",
                Description = string.Empty,
                Entries = new List<DropEntryData>(),
            };
            _rows.Add(row);
            _selectedIndex = _rows.Count - 1;
            _isDirty = true;
        }

        private void DuplicateSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
                return;

            string json = JsonConvert.SerializeObject(_rows[_selectedIndex], JsonSettings);
            DropData copy = JsonConvert.DeserializeObject<DropData>(json, JsonSettings) ?? new DropData();
            copy.Id = _rows.Count == 0 ? 1 : _rows.Max(row => row.Id) + 1;
            copy.Name = $"{copy.Name} Copy";
            copy.EnsureValid();
            _rows.Add(copy);
            _selectedIndex = _rows.Count - 1;
            _isDirty = true;
        }

        private void DeleteSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
                return;

            _rows.RemoveAt(_selectedIndex);
            _selectedIndex = Mathf.Clamp(_selectedIndex, -1, _rows.Count - 1);
            _isDirty = true;
        }

        private void LoadData()
        {
            _rows.Clear();
            _selectedIndex = -1;
            _isDirty = false;

            if (!File.Exists(DataPath))
            {
                _statusText = $"Missing file: {DataPath}, a new one will be created on save.";
                return;
            }

            try
            {
                string json = File.ReadAllText(DataPath);
                TableWrapper wrapper = JsonConvert.DeserializeObject<TableWrapper>(json, JsonSettings);
                if (wrapper?.Rows != null)
                {
                    _rows.AddRange(wrapper.Rows);
                    foreach (DropData row in _rows)
                        row?.EnsureValid();
                }

                _statusText = $"Loaded {_rows.Count} rows · {DataPath}";
            }
            catch (Exception ex)
            {
                _statusText = $"Load failed: {ex.Message}";
                Debug.LogError($"[DropEditor] Load error:\n{ex}");
            }
        }

        private void SaveData()
        {
            string directory = Path.GetDirectoryName(DataPath);
            if (string.IsNullOrWhiteSpace(directory))
                return;

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            try
            {
                foreach (DropData row in _rows)
                    row?.EnsureValid();

                string json = JsonConvert.SerializeObject(new TableWrapper { Rows = _rows }, JsonSettings);
                File.WriteAllText(DataPath, json, Encoding.UTF8);
                AssetDatabase.Refresh();
                _isDirty = false;
                _statusText = $"Saved {_rows.Count} rows · {DataPath}";
            }
            catch (Exception ex)
            {
                _statusText = $"Save failed: {ex.Message}";
                Debug.LogError($"[DropEditor] Save error:\n{ex}");
            }
        }

        private static List<ItemOption> BuildItemOptions()
        {
            List<ItemOption> options = new()
            {
                new ItemOption { Id = 0, Label = "None" }
            };

            foreach (ItemData row in EditorComponents.Data.FindAll<ItemData>(_ => true).OrderBy(row => row.Id))
            {
                options.Add(new ItemOption
                {
                    Id = row.Id,
                    Label = $"[{row.Id}] {row.Name}",
                });
            }

            return options;
        }

        private static int DrawItemPopup(string label, int currentId, List<ItemOption> options)
        {
            int selectedIndex = 0;
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Id == currentId)
                {
                    selectedIndex = i;
                    break;
                }
            }

            string[] labels = options.Select(option => option.Label).ToArray();
            int newIndex = EditorGUILayout.Popup(label, selectedIndex, labels);
            return newIndex >= 0 && newIndex < options.Count ? options[newIndex].Id : currentId;
        }

    }
}
