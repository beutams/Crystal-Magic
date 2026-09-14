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
    public sealed class ShopDataEditorWindow : EditorWindow
    {
        private const string DataPath = "Assets/Res/Data/ShopDataTable.json";
        private const string ItemDataPath = "Assets/Res/Data/ItemDataTable.json";
        private const float ListPanelWidth = 260f;
        private const float LabelWidth = 130f;

        private sealed class ShopTableWrapper
        {
            public List<ShopData> Rows = new();
        }

        private sealed class ItemTableWrapper
        {
            public List<ItemData> Rows = new();
        }

        private readonly List<ShopData> _rows = new();
        private readonly List<ItemData> _items = new();
        private string _selectedShopName;
        private bool _isDirty;
        private string _statusText = string.Empty;
        private Vector2 _shopListScrollPosition;
        private Vector2 _contentScrollPosition;

        [MenuItem("Tools/Data/ShopData Editor")]
        public static void Open()
        {
            ShopDataEditorWindow window = GetWindow<ShopDataEditorWindow>("ShopData Editor");
            window.minSize = new Vector2(860f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadItems();
            LoadData();
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.BeginHorizontal();
            DrawShopListPanel();
            DrawDivider();
            DrawShopContentsPanel();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUI.enabled = _isDirty;
            if (GUILayout.Button(_isDirty ? "Save *" : "Save", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                SaveData();
            GUI.enabled = true;

            if (GUILayout.Button("Add Shop", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                AddShop();

            GUI.enabled = TryGetSelectedShopName(out _);
            if (GUILayout.Button("Add Item", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                AddItemToSelectedShop();
            if (GUILayout.Button("Delete Shop", EditorStyles.toolbarButton, GUILayout.Width(82f)))
                DeleteSelectedShop();
            GUI.enabled = true;

            GUILayout.FlexibleSpace();
            if (!string.IsNullOrWhiteSpace(_statusText))
                GUILayout.Label(_statusText, EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawShopListPanel()
        {
            List<string> shopNames = GetShopNames();
            EditorGUILayout.BeginVertical(GUILayout.Width(ListPanelWidth), GUILayout.ExpandHeight(true));
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"Shop NPCs ({shopNames.Count})", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _shopListScrollPosition = EditorGUILayout.BeginScrollView(_shopListScrollPosition);
            foreach (string shopName in shopNames)
            {
                int itemCount = _rows.Count(row => string.Equals(GetShopName(row), shopName, StringComparison.Ordinal));
                string label = $"{GetShopLabel(shopName)} ({itemCount})";
                if (GUILayout.Toggle(string.Equals(_selectedShopName, shopName, StringComparison.Ordinal), label, "Button")
                    && !string.Equals(_selectedShopName, shopName, StringComparison.Ordinal))
                {
                    EditorFocusUtility.ClearTextFocus();
                    _selectedShopName = shopName;
                    _contentScrollPosition = Vector2.zero;
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawShopContentsPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (!TryGetSelectedShopName(out string shopName))
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Create or select a shop NPC from the left list.", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"{GetShopLabel(shopName)} Items", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            ShopData rowToDelete = null;
            _contentScrollPosition = EditorGUILayout.BeginScrollView(_contentScrollPosition);
            DrawShopNameField(shopName);
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Items", EditorStyles.boldLabel);

            foreach (ShopData row in _rows.Where(row => string.Equals(GetShopName(row), shopName, StringComparison.Ordinal)).ToArray())
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"[{row.Id}]", EditorStyles.miniLabel, GUILayout.Width(44f));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Remove", GUILayout.Width(66f)))
                    rowToDelete = row;
                EditorGUILayout.EndHorizontal();

                float previousLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = LabelWidth;
                EditorGUI.BeginChangeCheck();
                row.itemDataId = DrawItemPopup("Item", row.itemDataId);
                row.Price = EditorGUILayout.IntField("Price", row.Price);
                row.Grade = EditorGUILayout.IntField("Grade", row.Grade);
                if (EditorGUI.EndChangeCheck())
                    _isDirty = true;
                EditorGUIUtility.labelWidth = previousLabelWidth;
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            if (rowToDelete != null)
            {
                _rows.Remove(rowToDelete);
                _isDirty = true;
                _statusText = $"Removed item [{rowToDelete.Id}] from {GetShopLabel(shopName)}";
                Repaint();
            }
        }

        private void DrawShopNameField(string shopName)
        {
            EditorGUI.BeginChangeCheck();
            string newShopName = EditorGUILayout.TextField("NPC", shopName ?? string.Empty);
            if (!EditorGUI.EndChangeCheck())
                return;

            if (string.IsNullOrWhiteSpace(newShopName))
            {
                _statusText = "NPC name cannot be empty.";
                return;
            }

            string trimmedName = newShopName.Trim();
            if (string.Equals(shopName, trimmedName, StringComparison.Ordinal))
                return;

            foreach (ShopData row in _rows)
            {
                if (string.Equals(GetShopName(row), shopName, StringComparison.Ordinal))
                    row.NPC = trimmedName;
            }

            _selectedShopName = trimmedName;
            _isDirty = true;
        }

        private void AddShop()
        {
            string shopName = GetUniqueNewShopName();
            _rows.Add(new ShopData
            {
                Id = GetNextId(),
                itemDataId = _items.Count > 0 ? _items[0].Id : -1,
                Grade = 1,
                NPC = shopName,
            });
            _selectedShopName = shopName;
            _contentScrollPosition = Vector2.zero;
            _isDirty = true;
        }

        private void AddItemToSelectedShop()
        {
            if (!TryGetSelectedShopName(out string shopName))
                return;

            _rows.Add(new ShopData
            {
                Id = GetNextId(),
                itemDataId = _items.Count > 0 ? _items[0].Id : -1,
                Grade = 1,
                NPC = shopName,
            });
            _isDirty = true;
        }

        private void DeleteSelectedShop()
        {
            if (!TryGetSelectedShopName(out string shopName))
                return;

            int itemCount = _rows.Count(row => string.Equals(GetShopName(row), shopName, StringComparison.Ordinal));
            if (!EditorUtility.DisplayDialog("Delete Shop", $"Delete {itemCount} item(s) from {GetShopLabel(shopName)}?", "Delete", "Cancel"))
                return;

            _rows.RemoveAll(row => string.Equals(GetShopName(row), shopName, StringComparison.Ordinal));
            _selectedShopName = null;
            _isDirty = true;
        }

        private void LoadData()
        {
            _rows.Clear();
            _selectedShopName = null;
            _isDirty = false;

            try
            {
                if (File.Exists(DataPath))
                {
                    ShopTableWrapper table = JsonConvert.DeserializeObject<ShopTableWrapper>(DataFileUtility.ReadJsonText(DataPath));
                    if (table?.Rows != null)
                        _rows.AddRange(table.Rows.Where(static row => row != null));
                }

                _statusText = $"Loaded {_rows.Count} rows";
            }
            catch (Exception exception)
            {
                _statusText = $"Load failed: {exception.Message}";
                Debug.LogError($"[ShopDataEditor] Load failed:\n{exception}");
            }
        }

        private void LoadItems()
        {
            _items.Clear();
            if (!File.Exists(ItemDataPath))
                return;

            try
            {
                ItemTableWrapper table = JsonConvert.DeserializeObject<ItemTableWrapper>(DataFileUtility.ReadJsonText(ItemDataPath));
                if (table?.Rows != null)
                    _items.AddRange(table.Rows.Where(static row => row != null).OrderBy(static row => row.Id));
            }
            catch (Exception exception)
            {
                Debug.LogError($"[ShopDataEditor] Failed to load ItemData options:\n{exception}");
            }
        }

        private void SaveData()
        {
            try
            {
                string directory = Path.GetDirectoryName(DataPath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                DataFileUtility.WriteJsonText(DataPath, JsonConvert.SerializeObject(new ShopTableWrapper { Rows = _rows }, Formatting.Indented));
                AssetDatabase.Refresh();
                _isDirty = false;
                _statusText = $"Saved {_rows.Count} rows";
            }
            catch (Exception exception)
            {
                _statusText = $"Save failed: {exception.Message}";
                Debug.LogError($"[ShopDataEditor] Save failed:\n{exception}");
            }
        }

        private List<string> GetShopNames()
        {
            return _rows.Select(GetShopName).Distinct(StringComparer.Ordinal).OrderBy(static name => name, StringComparer.Ordinal).ToList();
        }

        private bool TryGetSelectedShopName(out string shopName)
        {
            string selectedShopName = _selectedShopName;
            shopName = selectedShopName;
            return selectedShopName != null
                   && _rows.Any(row => string.Equals(GetShopName(row), selectedShopName, StringComparison.Ordinal));
        }

        private int DrawItemPopup(string label, int itemId)
        {
            List<int> ids = new() { -1 };
            List<string> labels = new() { "None" };
            if (itemId >= 0 && _items.All(item => item.Id != itemId))
            {
                ids.Add(itemId);
                labels.Add($"[Missing {itemId}]");
            }

            for (int i = 0; i < _items.Count; i++)
            {
                ids.Add(_items[i].Id);
                labels.Add($"[{_items[i].Id}] {_items[i].NameKey}");
            }

            int selected = Mathf.Max(0, ids.IndexOf(itemId));
            return ids[EditorGUILayout.Popup(label, selected, labels.ToArray())];
        }

        private string GetUniqueNewShopName()
        {
            const string baseName = "New Shop";
            HashSet<string> existingNames = new(GetShopNames(), StringComparer.Ordinal);
            if (!existingNames.Contains(baseName))
                return baseName;

            for (int suffix = 2; ; suffix++)
            {
                string candidate = $"{baseName} {suffix}";
                if (!existingNames.Contains(candidate))
                    return candidate;
            }
        }

        private int GetNextId()
        {
            return _rows.Count == 0 ? 0 : _rows.Max(static row => row.Id) + 1;
        }

        private static string GetShopName(ShopData row)
        {
            return row?.NPC ?? string.Empty;
        }

        private static string GetShopLabel(string shopName)
        {
            return string.IsNullOrWhiteSpace(shopName) ? "<Unnamed Shop>" : shopName;
        }

        private static void DrawDivider()
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));
        }
    }
}
