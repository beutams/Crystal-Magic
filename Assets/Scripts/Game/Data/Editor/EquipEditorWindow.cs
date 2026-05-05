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
        private const float ListPanelWidth = 260f;
        private const float LabelWidth = 160f;

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

            if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(44f)))
            {
                RefreshItemCache();
                LoadData();
            }

            GUI.enabled = _isDirty;
            if (GUILayout.Button(_isDirty ? "Save *" : "Save", EditorStyles.toolbarButton, GUILayout.Width(52f)))
            {
                SaveData();
            }
            GUI.enabled = true;

            if (GUILayout.Button("+ Add", EditorStyles.toolbarButton, GUILayout.Width(56f)))
            {
                AddEquip();
            }

            GUI.enabled = _selectedIndex >= 0;
            if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            {
                DuplicateSelected();
            }

            if (GUILayout.Button("Delete", EditorStyles.toolbarButton, GUILayout.Width(56f)))
            {
                DeleteSelected();
            }
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
            GUILayout.Label($"Equip 列表 ({_rows.Count})", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _listScrollPos = EditorGUILayout.BeginScrollView(_listScrollPos);
            for (int i = 0; i < _rows.Count; i++)
            {
                EquipData row = _rows[i];
                string label = $"[{row.Id}] {GetListName(row)}";
                bool isSelected = i == _selectedIndex;
                if (GUILayout.Toggle(isSelected, label, "Button"))
                    _selectedIndex = i;
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
                GUILayout.Label("← 从左侧选择一个 EquipData", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            EquipData row = _rows[_selectedIndex];
            row.Properties ??= new List<EquipPropertyEntry>();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"[{row.Id}]  {GetListName(row)}", EditorStyles.boldLabel);
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
                entry.Channel = (PropertyModifierChannel)EditorGUILayout.EnumPopup(entry.Channel, GUILayout.MinWidth(220f));
                entry.BaseBonus = EditorGUILayout.FloatField("基础值", entry.BaseBonus, GUILayout.MinWidth(140f));
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

        private void RefreshItemCache()
        {
            _equipItems.Clear();
            _equipItems.AddRange(EditorComponents.Data.FindAll<ItemData>(item =>
                item.ItemType == ItemType.Weapon || item.ItemType == ItemType.Accessory));
        }

        private void LoadData()
        {
            _rows.Clear();
            _selectedIndex = -1;
            _isDirty = false;

            if (!File.Exists(DataPath))
            {
                _statusText = $"未找到文件：{DataPath}，将自动新建";
                return;
            }

            try
            {
                TableWrapper wrapper = JsonConvert.DeserializeObject<TableWrapper>(File.ReadAllText(DataPath), JsonSettings);
                if (wrapper?.Rows != null)
                    _rows.AddRange(wrapper.Rows);

                EnsureValidIds();
                _statusText = $"已加载 {_rows.Count} 条 · {DataPath}";
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
                EnsureValidIds();
                string json = JsonConvert.SerializeObject(new TableWrapper { Rows = _rows }, JsonSettings);
                File.WriteAllText(DataPath, json, Encoding.UTF8);
                AssetDatabase.Refresh();
                _isDirty = false;
                _statusText = $"已保存 {_rows.Count} 条 · {DataPath}";
            }
            catch (System.Exception ex)
            {
                _statusText = $"保存失败：{ex.Message}";
                Debug.LogError($"[EquipEditor] Save error:\n{ex}");
            }
        }

        private void AddEquip()
        {
            EquipData data = new()
            {
                Id = GetNextId(),
                Properties = new List<EquipPropertyEntry>(),
            };
            _rows.Add(data);
            _selectedIndex = _rows.Count - 1;
            _isDirty = true;
        }

        private void DuplicateSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
                return;

            string json = JsonConvert.SerializeObject(_rows[_selectedIndex], JsonSettings);
            EquipData copy = JsonConvert.DeserializeObject<EquipData>(json, JsonSettings);
            if (copy == null)
                return;

            copy.Id = GetNextId();
            copy.Properties ??= new List<EquipPropertyEntry>();
            _rows.Add(copy);
            _selectedIndex = _rows.Count - 1;
            _isDirty = true;
        }

        private void DeleteSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
                return;

            EquipData selected = _rows[_selectedIndex];
            string linkedItems = GetLinkedItemsLabel(selected.Id);
            bool hasLinks = !string.IsNullOrEmpty(linkedItems) && linkedItems != "无";
            string message = hasLinks
                ? $"这条 EquipData 仍被以下装备引用：\n{linkedItems}\n\n确认继续删除吗？"
                : "确认删除当前 EquipData 吗？";

            if (!EditorUtility.DisplayDialog("删除 EquipData", message, "删除", "取消"))
                return;

            _rows.RemoveAt(_selectedIndex);
            _selectedIndex = Mathf.Clamp(_selectedIndex, -1, _rows.Count - 1);
            _isDirty = true;
        }

        private int GetNextId()
        {
            int maxId = 0;
            for (int i = 0; i < _rows.Count; i++)
                maxId = Mathf.Max(maxId, _rows[i].Id);
            return maxId + 1;
        }

        private void EnsureValidIds()
        {
            HashSet<int> usedIds = new();
            int nextId = 1;

            for (int i = 0; i < _rows.Count; i++)
            {
                EquipData row = _rows[i];
                row.Properties ??= new List<EquipPropertyEntry>();

                if (row.Id <= 0 || usedIds.Contains(row.Id))
                {
                    while (usedIds.Contains(nextId))
                        nextId++;
                    row.Id = nextId;
                }

                usedIds.Add(row.Id);
                _rows[i] = row;
            }
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
