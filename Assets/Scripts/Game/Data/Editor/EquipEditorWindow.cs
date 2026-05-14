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
    public class EquipEditorWindow : EditorWindow
    {
        private const string DataPath = "Assets/Res/Data/EquipDataTable.json";
        private const string ItemDataPath = "Assets/Res/Data/ItemDataTable.json";
        private const float ListPanelWidth = 260f;
        private const float LabelWidth = 160f;

        private static readonly PropertyModifierChannel[] PropertyModifierChannels =
            (PropertyModifierChannel[])System.Enum.GetValues(typeof(PropertyModifierChannel));

        private static readonly string[] PropertyModifierChannelDisplayNames =
            EditorLabelUtility.GetEnumDisplayNames<PropertyModifierChannel>();

        private readonly List<EquipData> _rows = new();
        private readonly List<ItemData> _equipItems = new();

        private bool _isDirty;
        private string _statusText = string.Empty;
        private int _selectedIndex = -1;
        private Vector2 _listScrollPos;
        private Vector2 _detailScrollPos;

        private static JsonSerializerSettings JsonSettings => new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
        };

        private class TableWrapper
        {
            public List<EquipData> Rows = new();
        }

        private class ItemTableWrapper
        {
            public List<ItemData> Rows = new();
        }

        [MenuItem("Tools/Data/Equip Editor")]
        public static void Open()
        {
            EquipEditorWindow window = GetWindow<EquipEditorWindow>("Equip Editor");
            window.minSize = new Vector2(860f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshItemCache();
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

            if (GUILayout.Button("加载", EditorStyles.toolbarButton, GUILayout.Width(44f)))
            {
                RefreshItemCache();
                LoadData();
            }

            GUI.enabled = _isDirty;
            if (GUILayout.Button(_isDirty ? "保存 *" : "保存", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                SaveData();
            GUI.enabled = true;

            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(_statusText))
                GUILayout.Label(_statusText, EditorStyles.miniLabel, GUILayout.ExpandWidth(false));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(ListPanelWidth), GUILayout.ExpandHeight(true));
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"装备列表 ({_rows.Count})", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _listScrollPos = EditorGUILayout.BeginScrollView(_listScrollPos);
            for (int i = 0; i < _rows.Count; i++)
            {
                EquipData row = _rows[i];
                string label = $"[{row.Id}] {GetListName(row)}";
                bool isSelected = i == _selectedIndex;
                if (GUILayout.Toggle(isSelected, label, "Button"))
                {
                    if (_selectedIndex != i)
                        CrystalMagic.Editor.EditorFocusUtility.ClearTextFocus();
                    _selectedIndex = i;
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawDivider()
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));
        }

        private void DrawDetailPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("从左侧选择一个 EquipData", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            EquipData row = _rows[_selectedIndex];
            row.Properties ??= new List<EquipPropertyEntry>();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"[{row.Id}] {GetListName(row)}", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _detailScrollPos = EditorGUILayout.BeginScrollView(_detailScrollPos);
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = LabelWidth;

            DrawSectionHeader("基础信息");
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Id", row.Id);
                EditorGUILayout.TextField("关联装备", GetLinkedItemsLabel(row.Id));
            }

            DrawSectionHeader("基础属性");
            int removeAt = -1;
            for (int i = 0; i < row.Properties.Count; i++)
            {
                EquipPropertyEntry entry = row.Properties[i];
                EditorGUI.BeginChangeCheck();

                EditorGUILayout.BeginHorizontal();
                int propertyChannelIndex = System.Array.IndexOf(PropertyModifierChannels, entry.Channel);
                propertyChannelIndex = EditorGUILayout.Popup(
                    Mathf.Max(0, propertyChannelIndex),
                    PropertyModifierChannelDisplayNames,
                    GUILayout.MinWidth(220f));

                entry.Channel = PropertyModifierChannels[propertyChannelIndex];

                string valueLabel = IsSpeedPropertyChannel(entry.Channel) ? "基础值 (-100~100)" : "基础值";
                float value = EditorGUILayout.FloatField(valueLabel, entry.BaseBonus, GUILayout.MinWidth(140f));
                entry.BaseBonus = IsSpeedPropertyChannel(entry.Channel)
                    ? Mathf.Clamp(value, -100f, 100f)
                    : value;

                if (GUILayout.Button("删除", GUILayout.Width(44f)))
                    removeAt = i;
                EditorGUILayout.EndHorizontal();

                if (EditorGUI.EndChangeCheck())
                {
                    row.Properties[i] = entry;
                    _isDirty = true;
                }
            }

            if (GUILayout.Button("+ 添加基础属性", GUILayout.Width(140f)))
            {
                row.Properties.Add(new EquipPropertyEntry());
                _isDirty = true;
            }

            if (removeAt >= 0)
            {
                row.Properties.RemoveAt(removeAt);
                _isDirty = true;
            }

            EditorGUIUtility.labelWidth = previousLabelWidth;
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private static bool IsSpeedPropertyChannel(PropertyModifierChannel channel)
        {
            return channel is PropertyModifierChannel.ActionSpeed or PropertyModifierChannel.ChantSpeed;
        }

        private void RefreshItemCache()
        {
            _equipItems.Clear();
            _equipItems.AddRange(EditorComponents.Data.FindAll<ItemData>(item =>
                item.ItemType == ItemType.MagicStone || item.ItemType == ItemType.Spirit));
            _equipItems.Sort((left, right) =>
            {
                int typeCompare = left.ItemType.CompareTo(right.ItemType);
                if (typeCompare != 0)
                    return typeCompare;

                return left.Id.CompareTo(right.Id);
            });
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
                    TableWrapper wrapper = JsonConvert.DeserializeObject<TableWrapper>(File.ReadAllText(DataPath), JsonSettings);
                    if (wrapper?.Rows != null)
                        _rows.AddRange(wrapper.Rows);
                }

                SyncRowsWithEquipItems();
                _statusText = File.Exists(DataPath)
                    ? $"已加载 {_rows.Count} 条 · {DataPath}"
                    : $"未找到文件：{DataPath}，已按装备列表生成默认数据";
            }
            catch (System.Exception ex)
            {
                _statusText = $"加载失败：{ex.Message}";
                Debug.LogError($"[EquipEditor] Load error:\n{ex}");
            }
        }

        private void SaveData()
        {
            string directory = Path.GetDirectoryName(DataPath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            try
            {
                SyncRowsWithEquipItems();
                int syncedItemCount = SyncLinkedItemExtraIdsFromEquipItems();
                string json = JsonConvert.SerializeObject(new TableWrapper { Rows = _rows }, JsonSettings);
                File.WriteAllText(DataPath, json, Encoding.UTF8);
                AssetDatabase.Refresh();
                RefreshItemCache();
                _isDirty = false;
                _statusText = syncedItemCount > 0
                    ? $"已保存 {_rows.Count} 条 · {DataPath} · 已同步 {syncedItemCount} 条 ItemData"
                    : $"已保存 {_rows.Count} 条 · {DataPath}";
            }
            catch (System.Exception ex)
            {
                _statusText = $"保存失败：{ex.Message}";
                Debug.LogError($"[EquipEditor] Save error:\n{ex}");
            }
        }

        private void SyncRowsWithEquipItems()
        {
            Dictionary<int, EquipData> existingRows = _rows
                .Where(row => row != null)
                .GroupBy(row => row.Id)
                .ToDictionary(group => group.Key, group => group.First());
            List<EquipData> syncedRows = new();

            for (int i = 0; i < _equipItems.Count; i++)
            {
                ItemData item = _equipItems[i];
                if (item.ExtraId < 0)
                {
                    Debug.LogWarning($"[EquipEditor] 装备物品 {item.Name} 的 ExtraId 为 -1，当前不会生成 EquipData。");
                    continue;
                }

                if (!existingRows.TryGetValue(item.ExtraId, out EquipData row))
                {
                    row = new EquipData
                    {
                        Id = item.ExtraId,
                        Properties = new List<EquipPropertyEntry>(),
                    };
                    _isDirty = true;
                }

                row.Properties ??= new List<EquipPropertyEntry>();
                row.Id = item.ExtraId;
                syncedRows.Add(row);
            }

            _rows.Clear();
            _rows.AddRange(syncedRows.OrderBy(row => row.Id));

            if (_rows.Count == 0)
                _selectedIndex = -1;
            else
                _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _rows.Count - 1);
        }

        private int SyncLinkedItemExtraIdsFromEquipItems()
        {
            if (!File.Exists(ItemDataPath))
                return 0;

            ItemTableWrapper wrapper = JsonConvert.DeserializeObject<ItemTableWrapper>(File.ReadAllText(ItemDataPath), JsonSettings);
            if (wrapper?.Rows == null || wrapper.Rows.Count == 0)
                return 0;

            Dictionary<int, int> equipItemIdToExtraId = _equipItems.ToDictionary(item => item.Id, item => item.ExtraId);
            int updatedCount = 0;
            foreach (ItemData item in wrapper.Rows)
            {
                if (item == null)
                    continue;

                if (item.ItemType != ItemType.MagicStone && item.ItemType != ItemType.Spirit)
                    continue;

                if (equipItemIdToExtraId.TryGetValue(item.Id, out int extraId))
                {
                    if (item.ExtraId == extraId)
                        continue;

                    item.ExtraId = extraId;
                    updatedCount++;
                }
            }

            if (updatedCount <= 0)
                return 0;

            string itemJson = JsonConvert.SerializeObject(wrapper, JsonSettings);
            File.WriteAllText(ItemDataPath, itemJson, Encoding.UTF8);
            return updatedCount;
        }

        private string GetListName(EquipData row)
        {
            List<string> names = GetLinkedItemNames(row.Id);
            if (names.Count == 0)
                return "未绑定装备";
            if (names.Count == 1)
                return names[0];
            return $"{names[0]} 等 {names.Count} 件";
        }

        private string GetLinkedItemsLabel(int equipId)
        {
            List<string> names = GetLinkedItemNames(equipId);
            return names.Count == 0 ? "无" : string.Join("、", names);
        }

        private List<string> GetLinkedItemNames(int equipId)
        {
            return _equipItems
                .Where(item => item.ExtraId == equipId)
                .Select(item => item.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .OrderBy(name => name)
                .ToList();
        }

        private static void DrawSectionHeader(string title)
        {
            GUILayout.Space(6);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            Rect rect = GUILayoutUtility.GetLastRect();
            rect.y += rect.height + 1f;
            rect.height = 1f;
            EditorGUI.DrawRect(rect, new Color(0.45f, 0.45f, 0.45f, 1f));
            GUILayout.Space(4);
        }
    }
}
