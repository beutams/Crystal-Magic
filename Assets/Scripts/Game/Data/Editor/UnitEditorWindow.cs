using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CrystalMagic.Core;
using CrystalMagic.Editor.Unit;
using CrystalMagic.Game.Data;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Data
{
    public class UnitEditorWindow : EditorWindow
    {
        private const string DataPath = "Assets/Res/Data/UnitDataTable.json";
        private const string DropDataPath = "Assets/Res/Data/DropDataTable.json";
        private const string UnitPrefabDirectory = "Assets/Res/Prefab/Unit";
        private const float ListPanelWidth = 220f;
        private const float ItemHeight = 26f;
        private const float LabelWidth = 140f;

        private sealed class UnitPrefabEntry
        {
            public string AssetPath;
            public GameObject Prefab;

            public string DisplayName => Path.GetFileNameWithoutExtension(AssetPath);
        }

        private sealed class TableWrapper
        {
            public List<UnitData> Rows = new();
        }

        private sealed class DropTableWrapper
        {
            public List<DropData> Rows = new();
        }

        private sealed class IntOption
        {
            public int Id;
            public string Label;
        }

        private static readonly Color SelectedColor = new(0.27f, 0.52f, 0.85f, 0.85f);
        private static readonly Color EvenRowColor = new(0.22f, 0.22f, 0.22f, 1f);
        private static readonly Color OddRowColor = new(0.25f, 0.25f, 0.25f, 1f);
        private static readonly Color HoverColor = new(0.32f, 0.32f, 0.32f, 1f);
        private static readonly Color SectionLine = new(0.45f, 0.45f, 0.45f, 1f);
        private static readonly Color DividerColor = new(0.15f, 0.15f, 0.15f, 1f);

        private static JsonSerializerSettings JsonSettings => new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
        };

        private List<UnitData> _rows = new();
        private readonly List<DropData> _dropRows = new();
        private readonly List<UnitPrefabEntry> _prefabEntries = new();
        private bool _isDirty;
        private string _statusText = string.Empty;
        private int _selectedIndex = -1;
        private int _selectedTab;
        private int _copySourceUnitIndex;
        private Vector2 _listScrollPos;
        private Vector2 _detailScrollPos;

        [MenuItem("Tools/Data/Unit Editor")]
        public static void Open()
        {
            UnitEditorWindow window = GetWindow<UnitEditorWindow>("Unit Editor");
            window.minSize = new Vector2(900f, 540f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadData();
            LoadDropData();
            RefreshPrefabEntries();
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

        internal static void MarkPrefabDirty(UnityEngine.Object target)
        {
            if (target != null)
            {
                EditorUtility.SetDirty(target);
            }
        }

        internal void MarkDirty()
        {
            _isDirty = true;
        }

        internal static void DrawSectionHeader(string title)
        {
            GUILayout.Space(6f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            Rect rect = GUILayoutUtility.GetLastRect();
            rect.y += rect.height + 1f;
            rect.height = 1f;
            EditorGUI.DrawRect(rect, SectionLine);
            GUILayout.Space(4f);
        }

        private void RefreshPrefabEntries()
        {
            _prefabEntries.Clear();
            if (!AssetDatabase.IsValidFolder(UnitPrefabDirectory))
            {
                _selectedIndex = -1;
                return;
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { UnitPrefabDirectory });
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                _prefabEntries.Add(new UnitPrefabEntry
                {
                    AssetPath = path,
                    Prefab = prefab,
                });
            }

            _prefabEntries.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal));
            _selectedIndex = _prefabEntries.Count == 0 ? -1 : Mathf.Clamp(_selectedIndex, 0, _prefabEntries.Count - 1);
        }

        private bool TryGetSelectedPrefabEntry(out UnitPrefabEntry entry)
        {
            if (_selectedIndex >= 0 && _selectedIndex < _prefabEntries.Count)
            {
                entry = _prefabEntries[_selectedIndex];
                return true;
            }

            entry = null;
            return false;
        }

        private UnitData ResolveUnitData(UnitPrefabEntry entry)
        {
            if (entry == null)
            {
                return null;
            }

            UnitData byPath = _rows.FirstOrDefault(row => string.Equals(row.PrefabPath, entry.AssetPath, StringComparison.Ordinal));
            if (byPath != null)
            {
                return byPath;
            }

            return _rows.FirstOrDefault(row => string.Equals(row.Name, entry.DisplayName, StringComparison.Ordinal));
        }

        private UnitData CreateUnitDataForPrefab(UnitPrefabEntry entry)
        {
            UnitData row = new UnitData
            {
                Name = entry.DisplayName,
                Description = string.Empty,
                PrefabPath = entry.AssetPath,
            };
            row.NormalizeModules();
            _rows.Add(row);
            NormalizeRowIds();
            _isDirty = true;
            return row;
        }

        private UnitData CreateUnitDataForPrefab(UnitPrefabEntry entry, UnitData source)
        {
            if (source == null)
            {
                return CreateUnitDataForPrefab(entry);
            }

            string json = JsonConvert.SerializeObject(source, JsonSettings);
            UnitData row = JsonConvert.DeserializeObject<UnitData>(json, JsonSettings);
            if (row == null)
            {
                return CreateUnitDataForPrefab(entry);
            }

            row.Name = entry.DisplayName;
            row.PrefabPath = entry.AssetPath;
            row.NormalizeModules();
            _rows.Add(row);
            NormalizeRowIds();
            _isDirty = true;
            return row;
        }

        private static bool HasAuthoring<T>(UnitPrefabEntry entry) where T : Component
        {
            return entry?.Prefab != null && entry.Prefab.GetComponent<T>() != null;
        }

        private void LoadData()
        {
            _rows.Clear();
            _selectedIndex = -1;
            _isDirty = false;

            if (!File.Exists(DataPath))
            {
                _statusText = $"未找到文件：{DataPath}，将新建";
                return;
            }

            try
            {
                string json = File.ReadAllText(DataPath);
                TableWrapper wrapper = JsonConvert.DeserializeObject<TableWrapper>(json, JsonSettings);
                if (wrapper?.Rows != null)
                {
                    _rows = wrapper.Rows;
                }

                foreach (UnitData row in _rows)
                {
                    row?.NormalizeModules();
                }

                NormalizeRowIds();
                _statusText = $"Loaded {_rows.Count} rows - {DataPath}";
            }
            catch (Exception ex)
            {
                _statusText = $"加载失败：{ex.Message}";
                Debug.LogError($"[UnitEditor] Load error:\n{ex}");
            }

        }
        private void SaveData()
        {
            string directory = Path.GetDirectoryName(DataPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory!);
            }

            try
            {
                RefreshPrefabEntries();
                List<UnitData> saveRows = BuildSaveRowsFromPrefabs();
                int removedCount = Mathf.Max(0, _rows.Count - saveRows.Count);
                _rows = saveRows;

                NormalizeRowIds();
                foreach (UnitData row in _rows)
                {
                    row?.NormalizeModules();
                }

                string json = JsonConvert.SerializeObject(new TableWrapper { Rows = _rows }, JsonSettings);
                File.WriteAllText(DataPath, json, Encoding.UTF8);
                SaveDropData();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                _isDirty = false;
                _statusText = removedCount > 0
                    ? $"已保存 {_rows.Count} 条，清理 {removedCount} 条旧数据 - {DataPath}"
                    : $"已保存 {_rows.Count} 条 - {DataPath}";
            }
            catch (Exception ex)
            {
                _statusText = $"保存失败：{ex.Message}";
                Debug.LogError($"[UnitEditor] Save error:\n{ex}");
            }
        }

        private void LoadDropData()
        {
            _dropRows.Clear();
            if (!File.Exists(DropDataPath))
                return;

            try
            {
                string json = File.ReadAllText(DropDataPath);
                DropTableWrapper wrapper = JsonConvert.DeserializeObject<DropTableWrapper>(json, JsonSettings);
                if (wrapper?.Rows != null)
                    _dropRows.AddRange(wrapper.Rows);

                NormalizeDropRowIds();
                foreach (DropData row in _dropRows)
                    row?.EnsureValid();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnitEditor] Drop table load error:\n{ex}");
            }
        }

        private void SaveDropData()
        {
            string directory = Path.GetDirectoryName(DropDataPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            NormalizeDropRowIds();
            foreach (DropData row in _dropRows)
                row?.EnsureValid();

            string json = JsonConvert.SerializeObject(new DropTableWrapper { Rows = _dropRows }, JsonSettings);
            File.WriteAllText(DropDataPath, json, Encoding.UTF8);
        }

        private List<UnitData> BuildSaveRowsFromPrefabs()
        {
            List<UnitData> rows = new();
            HashSet<UnitData> usedRows = new();

            for (int i = 0; i < _prefabEntries.Count; i++)
            {
                UnitPrefabEntry entry = _prefabEntries[i];
                UnitData row = ResolveUnitData(entry);
                if (row == null || !usedRows.Add(row))
                {
                    continue;
                }

                row.Name = entry.DisplayName;
                row.PrefabPath = entry.AssetPath;
                row.NormalizeModules();
                rows.Add(row);
            }

            return rows;
        }

        private void NormalizeRowIds()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].Id = i;
            }
        }

        private void NormalizeDropRowIds()
        {
            for (int i = 0; i < _dropRows.Count; i++)
                _dropRows[i].Id = i;
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("加载", EditorStyles.toolbarButton, GUILayout.Width(44f)))
            {
                LoadData();
                RefreshPrefabEntries();
            }

            GUI.enabled = _isDirty;
            if (GUILayout.Button(_isDirty ? "保存 *" : "保存", EditorStyles.toolbarButton, GUILayout.Width(56f)))
            {
                SaveData();
            }
            GUI.enabled = true;

            if (GUILayout.Button("刷新 Prefab", EditorStyles.toolbarButton, GUILayout.Width(78f)))
            {
                RefreshPrefabEntries();
            }

            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(_statusText))
            {
                GUILayout.Label(_statusText, EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(ListPanelWidth), GUILayout.ExpandHeight(true));
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"Prefab 列表 ({_prefabEntries.Count})", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _listScrollPos = EditorGUILayout.BeginScrollView(_listScrollPos, GUILayout.ExpandHeight(true));
            Event currentEvent = Event.current;

            for (int i = 0; i < _prefabEntries.Count; i++)
            {
                UnitPrefabEntry entry = _prefabEntries[i];
                UnitData unitData = ResolveUnitData(entry);
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

                string bindingLabel = unitData == null
                    ? "(未绑定 UnitData)"
                    : $"[{unitData.Id}] {unitData.Name}";
                string label = $"{entry.DisplayName}  {bindingLabel}";
                GUI.Label(
                    new Rect(itemRect.x + 8f, itemRect.y + 4f, itemRect.width - 16f, itemRect.height - 4f),
                    label,
                    isSelected ? EditorStyles.whiteLabel : EditorStyles.label);

                if (currentEvent.type == EventType.MouseDown && itemRect.Contains(currentEvent.mousePosition))
                {
                    CrystalMagic.Editor.EditorFocusUtility.ClearTextFocus();
                    _selectedIndex = i;
                    currentEvent.Use();
                    Repaint();
                }

                if (currentEvent.type == EventType.MouseMove)
                {
                    Repaint();
                }
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

            if (!TryGetSelectedPrefabEntry(out UnitPrefabEntry entry))
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("从左侧选择一个 Prefab", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            UnitData unit = ResolveUnitData(entry);

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(entry.DisplayName, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _detailScrollPos = EditorGUILayout.BeginScrollView(_detailScrollPos);
            DrawBindingPanel(entry, ref unit);

            if (unit == null)
            {
                EditorGUILayout.HelpBox("当前 Prefab 还没有匹配到 UnitData，可以直接创建，或者从已有数据复制一份。", MessageType.Info);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            GUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8f);
            string[] tabs = { "属性", "状态", "行为" };
            _selectedTab = Mathf.Clamp(_selectedTab, 0, tabs.Length - 1);
            int newTab = GUILayout.Toolbar(_selectedTab, tabs, GUILayout.Width(260f), GUILayout.Height(24f));
            if (newTab != _selectedTab)
            {
                _selectedTab = newTab;
                CrystalMagic.Editor.EditorFocusUtility.ClearTextFocus();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(2f);
            Rect lineRect = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(lineRect, SectionLine);
            GUILayout.Space(4f);

            float oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = LabelWidth;

            switch (_selectedTab)
            {
                case 0:
                    DrawAttributePanel(entry, unit);
                    break;
                case 1:
                    DrawStatePreviewPanel(entry, unit);
                    break;
                case 2:
                    DrawBehaviorPreviewPanel(entry);
                    break;
            }

            EditorGUIUtility.labelWidth = oldLabelWidth;

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawBindingPanel(UnitPrefabEntry entry, ref UnitData unit)
        {
            DrawSectionHeader("Prefab 绑定");

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Prefab", entry.AssetPath);
            }

            if (unit != null)
            {
                EditorGUILayout.LabelField("绑定方式", "按 PrefabPath 自动匹配");
                EditorGUILayout.LabelField("当前数据", $"[{unit.Id}] {unit.Name}");
            }
            else
            {
                EditorGUILayout.HelpBox("还没有与这个 PrefabPath 对应的 UnitData。", MessageType.Info);
            }

            if (unit == null && GUILayout.Button("为当前 Prefab 创建 UnitData", GUILayout.Width(180f)))
            {
                unit = CreateUnitDataForPrefab(entry);
            }

            if (unit == null && _rows.Count > 0)
            {
                string[] options = _rows.Select(row => $"[{row.Id}] {row.Name}").ToArray();
                _copySourceUnitIndex = Mathf.Clamp(_copySourceUnitIndex, 0, options.Length - 1);
                _copySourceUnitIndex = EditorGUILayout.Popup("复制来源", _copySourceUnitIndex, options);

                if (GUILayout.Button("复制已有 UnitData 生成", GUILayout.Width(180f)))
                {
                    unit = CreateUnitDataForPrefab(entry, _rows[_copySourceUnitIndex]);
                }
            }
        }

        private void DrawAttributePanel(UnitPrefabEntry entry, UnitData unit)
        {
            EditorGUI.BeginChangeCheck();

            DrawSectionHeader("基础信息");
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Id", unit.Id);
            }

            unit.Name = EditorGUILayout.TextField("名称", unit.Name ?? string.Empty);
            EditorGUILayout.LabelField("描述");
            unit.Description = EditorGUILayout.TextArea(unit.Description ?? string.Empty, GUILayout.MinHeight(48f), GUILayout.MaxHeight(80f));

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("预制体路径", entry.AssetPath);
            }

            if (unit.PrefabPath != entry.AssetPath)
            {
                unit.PrefabPath = entry.AssetPath;
                _isDirty = true;
            }

            UnitEditorDrawerContext context = new(this, entry.Prefab, entry.AssetPath, entry.DisplayName, unit);
            IReadOnlyList<IUnitEditorAttributeDrawer> drawers = UnitEditorAttributeDrawerFactory.GetDrawers();
            foreach (IUnitEditorAttributeDrawer drawer in drawers)
            {
                if (drawer.CanDraw(context))
                {
                    drawer.Draw(context);
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                unit.NormalizeModules();
                _isDirty = true;
            }
        }

        private void DrawStatePreviewPanel(UnitPrefabEntry entry, UnitData unit)
        {
            DrawSectionHeader("State");
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("State Machine", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Open State Editor", GUILayout.Width(140f)))
            {
                StateMachineGraphWindow.Open();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("这里只预览状态数据，修改请到 State Machine 编辑器。", MessageType.Info);

            if (!HasAuthoring<UnitDecisionFeatureAuthoring>(entry))
            {
                EditorGUILayout.HelpBox("当前 Prefab 没有挂 UnitDecisionFeatureAuthoring。", MessageType.Warning);
                return;
            }

            UnitStateMachineModuleData stateModule = unit?.GetModule<UnitStateMachineModuleData>();
            List<UnitStateConfig> states = stateModule?.States ?? new List<UnitStateConfig>();
            EditorGUILayout.LabelField("State Count", states.Count.ToString());
            EditorGUILayout.LabelField("Initial State", states.Count > 0 ? states[0].StateType : "None");

            if (states.Count == 0)
            {
                EditorGUILayout.HelpBox("当前 UnitData 没有状态配置。", MessageType.Info);
                return;
            }

            GUILayout.Space(6f);
            for (int stateIndex = 0; stateIndex < states.Count; stateIndex++)
            {
                UnitStateConfig state = states[stateIndex];
                List<UnitTransitionConfig> transitions = state?.Transitions ?? new List<UnitTransitionConfig>();

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(stateIndex == 0 ? $"{state.StateType} (Default)" : state.StateType, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Transitions", transitions.Count.ToString());

                if (transitions.Count == 0)
                {
                    EditorGUILayout.LabelField("No outgoing transitions.", EditorStyles.miniLabel);
                }
                else
                {
                    for (int transitionIndex = 0; transitionIndex < transitions.Count; transitionIndex++)
                    {
                        UnitTransitionConfig transition = transitions[transitionIndex];
                        EditorGUILayout.LabelField($"-> {transition.TargetStateType}");
                        EditorGUILayout.LabelField(GetTransitionPreviewText(transition), EditorStyles.wordWrappedMiniLabel);
                    }
                }

                EditorGUILayout.EndVertical();
                GUILayout.Space(4f);
            }
        }

        private void DrawBehaviorPreviewPanel(UnitPrefabEntry entry)
        {
            DrawSectionHeader("Behavior");
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Behavior Tree", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Open Behavior Editor", GUILayout.Width(150f)))
            {
                BehaviorTreeGraphWindow.Open();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("这里只预览行为树数据，修改请到 Behavior Tree 编辑器。", MessageType.Info);

            UnitAIFeatureAuthoring authoring = entry.Prefab.GetComponent<UnitAIFeatureAuthoring>();
            if (authoring == null)
            {
                EditorGUILayout.HelpBox("当前 Prefab 没有挂 UnitAIFeatureAuthoring。", MessageType.Warning);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Unit Name", entry.DisplayName);
            }

            BehaviorTreeData tree = EditorComponents.Data.Find<BehaviorTreeData>(
                row => string.Equals(row.Name, entry.DisplayName, StringComparison.Ordinal));
            if (tree == null)
            {
                EditorGUILayout.HelpBox("没有找到对应的 BehaviorTreeData。", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Tree", $"[{tree.Id}] {GetBehaviorTreePreviewName(tree)}");
            if (!string.IsNullOrWhiteSpace(tree.Description))
            {
                EditorGUILayout.LabelField("Description", tree.Description, EditorStyles.wordWrappedLabel);
            }

            EditorGUILayout.LabelField("Node Count", tree.Nodes?.Count.ToString() ?? "0");

            BehaviorNodeData rootNode = tree.GetRootNode();
            EditorGUILayout.LabelField("Root", rootNode != null ? BehaviorNodeDataRegistry.GetDisplayName(rootNode.Type) : "None");

            if (tree.Nodes == null || tree.Nodes.Count == 0)
            {
                EditorGUILayout.HelpBox("这棵行为树还没有节点。", MessageType.Info);
                return;
            }

            GUILayout.Space(6f);
            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                BehaviorNodeData node = tree.Nodes[i];
                if (node == null)
                {
                    continue;
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"[{i + 1}] {BehaviorNodeDataRegistry.GetDisplayName(node.Type)}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(BehaviorNodeDataRegistry.GetSummary(node), EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
                GUILayout.Space(4f);
            }
        }

        private static string GetTransitionPreviewText(UnitTransitionConfig transition)
        {
            List<ConditionConfig> conditions = transition?.Conditions ?? new List<ConditionConfig>();
            if (conditions.Count == 0)
            {
                return "Always";
            }

            return string.Join(" | ", conditions.Select(GetConditionPreviewText));
        }

        private static string GetConditionPreviewText(ConditionConfig condition)
        {
            if (condition == null)
            {
                return "None";
            }

            string sourceType = string.IsNullOrWhiteSpace(condition.SourceType)
                ? "?"
                : EditorLabelUtility.GetTypeDisplayName(condition.SourceType, typeof(ISource));
            string compareType = string.IsNullOrWhiteSpace(condition.CompareType)
                ? "?"
                : EditorLabelUtility.GetTypeDisplayName(condition.CompareType, typeof(ICompareType));
            string valueText = condition.CompareType is "GreaterThan" or "LessThan" or "Equal"
                ? $" {condition.CompareValue:0.##}"
                : string.Empty;
            string sourceParamText = condition.SourceParam >= 0
                ? $"({condition.SourceParam})"
                : string.Empty;
            return $"{condition.ConditionType}: {sourceType}{sourceParamText} {compareType}{valueText}";
        }

        private static string GetBehaviorTreePreviewName(BehaviorTreeData tree)
        {
            if (!string.IsNullOrWhiteSpace(tree?.Name))
            {
                return tree.Name;
            }

            return "Unnamed Tree";
        }

        internal DropData GetDropData(int dropDataId)
        {
            if (dropDataId < 0)
                return null;

            for (int i = 0; i < _dropRows.Count; i++)
            {
                DropData row = _dropRows[i];
                if (row != null && row.Id == dropDataId)
                    return row;
            }

            return null;
        }

        internal DropData CreateDropDataForUnit(UnitData unit, UnitDropModuleData module)
        {
            if (unit == null || module == null)
                return null;

            DropData row = new DropData
            {
                Id = _dropRows.Count,
                Name = string.IsNullOrWhiteSpace(unit.Name) ? $"Drop {_dropRows.Count}" : $"{unit.Name} Drop",
                Description = string.Empty,
                Entries = new List<DropEntryData>(),
            };
            row.EnsureValid();
            _dropRows.Add(row);
            NormalizeDropRowIds();
            module.DropDataId = row.Id;
            _isDirty = true;
            return row;
        }

        internal bool DrawInlineDropDataEditor(UnitData unit, UnitDropModuleData module)
        {
            if (module == null)
                return false;

            bool changed = false;
            List<IntOption> dropOptions = BuildDropOptions(_dropRows);
            EditorGUILayout.BeginHorizontal();
            int newDropDataId = DrawIntPopup("Drop Table", module.DropDataId, dropOptions);
            if (newDropDataId != module.DropDataId)
            {
                module.DropDataId = newDropDataId;
                changed = true;
            }

            if (GUILayout.Button("Create", GUILayout.Width(72f)))
            {
                DropData created = CreateDropDataForUnit(unit, module);
                if (created != null)
                    changed = true;
            }
            EditorGUILayout.EndHorizontal();

            DropData dropData = GetDropData(module.DropDataId);
            if (module.DropDataId >= 0 && dropData == null)
            {
                EditorGUILayout.HelpBox($"Missing DropData #{module.DropDataId}. Create a new one or switch the reference.", MessageType.Warning);
                return changed;
            }

            if (dropData == null)
            {
                EditorGUILayout.HelpBox("No drop table assigned. Click Create to make one for this unit, or pick an existing table.", MessageType.Info);
                return changed;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical("box");
            dropData.Name = EditorGUILayout.TextField("Table Name", dropData.Name ?? string.Empty);
            EditorGUILayout.LabelField("Description");
            dropData.Description = EditorGUILayout.TextArea(dropData.Description ?? string.Empty, GUILayout.MinHeight(36f), GUILayout.MaxHeight(72f));

            GUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Entries", EditorStyles.boldLabel);
            if (GUILayout.Button("Add Entry", GUILayout.Width(84f)))
            {
                dropData.Entries.Add(new DropEntryData());
                changed = true;
            }
            EditorGUILayout.EndHorizontal();

            dropData.Entries ??= new List<DropEntryData>();
            List<IntOption> itemOptions = BuildItemOptions();
            for (int i = 0; i < dropData.Entries.Count; i++)
            {
                DropEntryData entry = dropData.Entries[i] ?? new DropEntryData();
                dropData.Entries[i] = entry;

                EditorGUILayout.BeginVertical("helpbox");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Entry {i + 1}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Delete", GUILayout.Width(56f)))
                {
                    dropData.Entries.RemoveAt(i);
                    changed = true;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                entry.DropType = (DropRewardType)EditorGUILayout.EnumPopup("Drop Type", entry.DropType);
                if (entry.DropType == DropRewardType.Item)
                    entry.ItemId = DrawIntPopup("Item", entry.ItemId, itemOptions);
                else
                    EditorGUILayout.HelpBox("Money reward does not require an item id.", MessageType.None);

                entry.Chance = EditorGUILayout.Slider("Chance", entry.Chance, 0f, 1f);
                entry.MinQuantity = Mathf.Max(0, EditorGUILayout.IntField("Min Quantity", entry.MinQuantity));
                entry.MaxQuantity = Mathf.Max(entry.MinQuantity, EditorGUILayout.IntField("Max Quantity", entry.MaxQuantity));
                EditorGUILayout.EndVertical();
            }

            dropData.EnsureValid();
            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
            return true;
        }

        private static List<IntOption> BuildDropOptions(IEnumerable<DropData> dropRows)
        {
            List<IntOption> options = new()
            {
                new IntOption { Id = -1, Label = "None" }
            };

            if (dropRows != null)
            {
                foreach (DropData row in dropRows.OrderBy(static row => row.Id))
                {
                    if (row == null)
                        continue;

                    options.Add(new IntOption
                    {
                        Id = row.Id,
                        Label = $"[{row.Id}] {row.Name}",
                    });
                }
            }

            return options;
        }

        private static List<IntOption> BuildItemOptions()
        {
            List<IntOption> options = new()
            {
                new IntOption { Id = -1, Label = "None" }
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

        private static int DrawIntPopup(string label, int currentId, List<IntOption> options)
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

            string[] labels = options.Select(static option => option.Label).ToArray();
            int newIndex = EditorGUILayout.Popup(label, selectedIndex, labels);
            return newIndex >= 0 && newIndex < options.Count ? options[newIndex].Id : currentId;
        }
    }
}
