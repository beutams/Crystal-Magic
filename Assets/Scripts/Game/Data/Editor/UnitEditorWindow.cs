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
        private const string UnitPrefabDirectory = "Assets/Res/Unit";
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
                _statusText = $"已加载 {_rows.Count} 条 - {DataPath}";
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
                NormalizeRowIds();
                foreach (UnitData row in _rows)
                {
                    row?.NormalizeModules();
                }

                string json = JsonConvert.SerializeObject(new TableWrapper { Rows = _rows }, JsonSettings);
                File.WriteAllText(DataPath, json, Encoding.UTF8);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                _isDirty = false;
                _statusText = $"已保存 {_rows.Count} 条 - {DataPath}";
            }
            catch (Exception ex)
            {
                _statusText = $"保存失败：{ex.Message}";
                Debug.LogError($"[UnitEditor] Save error:\n{ex}");
            }
        }

        private void NormalizeRowIds()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].Id = i + 1;
            }
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
                GUI.FocusControl(null);
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

            EditorGUILayout.HelpBox("这里仅预览状态数据，修改请到 State Machine 编辑器。", MessageType.Info);

            if (!HasAuthoring<UnitStateMachineAuthoring>(entry))
            {
                EditorGUILayout.HelpBox("当前 Prefab 没有挂 UnitStateMachineAuthoring。", MessageType.Warning);
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

            EditorGUILayout.HelpBox("这里仅预览行为树数据，修改请到 Behavior Tree 编辑器。", MessageType.Info);

            UnitBehaviorTreeAuthoring authoring = entry.Prefab.GetComponent<UnitBehaviorTreeAuthoring>();
            if (authoring == null)
            {
                EditorGUILayout.HelpBox("当前 Prefab 没有挂 UnitBehaviorTreeAuthoring。", MessageType.Warning);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("BehaviorTreeId", authoring.BehaviorTreeId);
                EditorGUILayout.FloatField("Tick Interval", authoring.TickInterval);
                EditorGUILayout.Toggle("Enable On Start", authoring.EnableOnStart);
            }

            BehaviorTreeData tree = authoring.BehaviorTreeId > 0
                ? EditorComponents.Data.Get<BehaviorTreeData>(authoring.BehaviorTreeId)
                : null;
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

            string sourceType = string.IsNullOrWhiteSpace(condition.SourceType) ? "?" : condition.SourceType;
            string compareType = string.IsNullOrWhiteSpace(condition.CompareType) ? "?" : condition.CompareType;
            string valueText = compareType is "GreaterThan" or "LessThan" or "Equal"
                ? $" {condition.CompareValue:0.##}"
                : string.Empty;
            return $"{condition.ConditionType}: {sourceType} {compareType}{valueText}";
        }

        private static string GetBehaviorTreePreviewName(BehaviorTreeData tree)
        {
            if (!string.IsNullOrWhiteSpace(tree?.Name))
            {
                return tree.Name;
            }

            return "Unnamed Tree";
        }
    }
}
