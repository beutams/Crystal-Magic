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
    public sealed class ItemDataEditorWindow : EditorWindow
    {
        private const string DataPath = "Assets/Res/Data/ItemDataTable.json";
        private const float ListPanelWidth = 260f;
        private const float LabelWidth = 145f;

        private static readonly ItemType[] ItemTypes = (ItemType[])Enum.GetValues(typeof(ItemType));
        private static readonly string[] ItemTypeNames = EditorLabelUtility.GetEnumDisplayNames<ItemType>();

        private sealed class TableWrapper
        {
            public List<ItemData> Rows = new();
        }

        private readonly List<ItemData> _rows = new();
        private int _selectedIndex = -1;
        private int _selectedTypeIndex;
        private bool _isDirty;
        private string _statusText = string.Empty;
        private Vector2 _listScrollPosition;
        private Vector2 _detailScrollPosition;

        [MenuItem("Tools/Data/ItemData Editor")]
        public static void Open()
        {
            ItemDataEditorWindow window = GetWindow<ItemDataEditorWindow>("ItemData Editor");
            window.minSize = new Vector2(880f, 560f);
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
            if (!string.IsNullOrWhiteSpace(_statusText))
                GUILayout.Label(_statusText, EditorStyles.miniLabel, GUILayout.ExpandWidth(false));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(ListPanelWidth), GUILayout.ExpandHeight(true));
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"Items ({GetVisibleRowCount()}/{_rows.Count})", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            int filterIndex = EditorGUILayout.Popup("Type", _selectedTypeIndex, GetFilterNames());
            if (EditorGUI.EndChangeCheck())
            {
                _selectedTypeIndex = filterIndex;
                _listScrollPosition = Vector2.zero;
            }

            _listScrollPosition = EditorGUILayout.BeginScrollView(_listScrollPosition);
            for (int i = 0; i < _rows.Count; i++)
            {
                ItemData row = _rows[i];
                if (!MatchesCurrentFilter(row))
                    continue;

                string label = $"[{row.Id}] {row.NameKey}";
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
            ItemData row = GetSelectedRow();
            if (row == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Select an item from the left list.", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"[{row.Id}] {row.NameKey}", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _detailScrollPosition = EditorGUILayout.BeginScrollView(_detailScrollPosition);
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = LabelWidth;
            EditorGUI.BeginChangeCheck();

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.IntField("Id", row.Id);

            row.NameKey = EditorGUILayout.TextField("Name Key", row.NameKey ?? string.Empty);
            EditorGUILayout.LabelField("Description Key");
            row.DescriptionKey = EditorGUILayout.TextArea(row.DescriptionKey ?? string.Empty, GUILayout.MinHeight(56f));
            row.ItemType = (ItemType)EditorGUILayout.EnumPopup("Type", row.ItemType);
            row.ExtraId = EditorGUILayout.IntField("Extra Id", row.ExtraId);
            row.Rarity = EditorGUILayout.IntField("Rarity", row.Rarity);
            row.MaxStack = EditorGUILayout.IntField("Max Stack", row.MaxStack);
            row.SellPrice = EditorGUILayout.IntField("Sell Price", row.SellPrice);
            row.IconPath = EditorGUILayout.TextField("Icon Path", row.IconPath ?? string.Empty);
            row.IsNonTransferable = EditorGUILayout.Toggle("Non Transferable", row.IsNonTransferable);

            if (EditorGUI.EndChangeCheck())
                _isDirty = true;

            EditorGUIUtility.labelWidth = previousLabelWidth;
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void AddRow()
        {
            ItemType itemType = _selectedTypeIndex > 0 ? ItemTypes[_selectedTypeIndex - 1] : ItemType.None;
            _rows.Add(new ItemData
            {
                Id = GetNextId(),
                NameKey = "New Item",
                DescriptionKey = string.Empty,
                ItemType = itemType,
                ExtraId = -1,
                MaxStack = 1,
            });
            _selectedIndex = _rows.Count - 1;
            _isDirty = true;
        }

        private void CopySelectedRow()
        {
            ItemData source = GetSelectedRow();
            if (source == null)
                return;

            ItemData copy = JsonConvert.DeserializeObject<ItemData>(JsonConvert.SerializeObject(source));
            if (copy == null)
                return;

            copy.Id = GetNextId();
            copy.NameKey = $"{copy.NameKey} Copy";
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
                Debug.LogError($"[ItemDataEditor] Load failed:\n{exception}");
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
                Debug.LogError($"[ItemDataEditor] Save failed:\n{exception}");
            }
        }

        private ItemData GetSelectedRow()
        {
            return _selectedIndex >= 0 && _selectedIndex < _rows.Count ? _rows[_selectedIndex] : null;
        }

        private bool MatchesCurrentFilter(ItemData row)
        {
            return row != null && (_selectedTypeIndex == 0 || row.ItemType == ItemTypes[_selectedTypeIndex - 1]);
        }

        private int GetVisibleRowCount()
        {
            return _rows.Count(MatchesCurrentFilter);
        }

        private string[] GetFilterNames()
        {
            string[] names = new string[ItemTypeNames.Length + 1];
            names[0] = "All";
            Array.Copy(ItemTypeNames, 0, names, 1, ItemTypeNames.Length);
            return names;
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
