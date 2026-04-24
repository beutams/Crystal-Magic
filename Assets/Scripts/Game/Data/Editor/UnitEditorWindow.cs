using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using CrystalMagic.Core;
using CrystalMagic.Editor.Unit;
using CrystalMagic.Game.Data;

namespace CrystalMagic.Editor.Data
{
    /// <summary>
    /// 单位编辑器
    /// 左侧：单位列表；右侧：属性 / AI 面板（选项卡切换）
    /// 菜单路径：Tools/Data/Unit Editor
    /// </summary>
    public class UnitEditorWindow : EditorWindow
    {
        private const string DataPath       = "Assets/Res/Data/UnitDataTable.json";
        private const string UnitPrefabDirectory = "Assets/Res/Unit";
        private const float  ListPanelWidth = 220f;
        private const float  ItemHeight     = 26f;
        private const float  InsertFieldWidth = 30f;
        private const float  LabelWidth     = 140f;

        private sealed class UnitPrefabEntry
        {
            public string AssetPath;
            public GameObject Prefab;

            public string DisplayName => Path.GetFileNameWithoutExtension(AssetPath);
        }

        private static readonly string[] TabNames = { "属性", "AI" };

        // ─── 数据 ─────────────────────────────────────
        private List<UnitData> _rows = new();
        private readonly List<UnitPrefabEntry> _prefabEntries = new();
        private bool   _isDirty;
        private string _statusText = "";

        // ─── UI 基础状态 ──────────────────────────────
        private int     _selectedIndex  = -1;
        private int     _selectedTab;
        private Vector2 _listScrollPos;
        private Vector2 _detailScrollPos;
        private readonly Dictionary<UnitData, string> _insertTexts = new();

        // ─── AI 面板：反射到的类型名 ──────────────────
        private string[] _stateTypeNames   = Array.Empty<string>();
        private string[] _sourceTypeNames  = Array.Empty<string>();
        private string[] _compareTypeNames = Array.Empty<string>();

        // ─── AI 面板：UI 状态 ─────────────────────────
        private int _addStateTypeIndex;
        private readonly Dictionary<string, bool> _stateFoldStates      = new();
        private readonly Dictionary<string, bool> _transitionFoldStates = new();
        private readonly Dictionary<string, int>  _transAddTargetIdx    = new();
        private readonly Dictionary<string, int>  _condAddSrcIdx        = new();
        private readonly Dictionary<string, int>  _condAddCmpIdx        = new();
        private int _copySourceUnitIndex;

        // ─── 颜色 ─────────────────────────────────────
        private static readonly Color SelectedColor = new(0.27f, 0.52f, 0.85f, 0.85f);
        private static readonly Color EvenRowColor  = new(0.22f, 0.22f, 0.22f, 1f);
        private static readonly Color OddRowColor   = new(0.25f, 0.25f, 0.25f, 1f);
        private static readonly Color HoverColor    = new(0.32f, 0.32f, 0.32f, 1f);
        private static readonly Color SectionLine   = new(0.45f, 0.45f, 0.45f, 1f);
        private static readonly Color DividerColor  = new(0.15f, 0.15f, 0.15f, 1f);
        private static readonly Color DangerColor   = new(1f,    0.45f, 0.45f, 1f);
        private static readonly Color StateBoxColor = new(0.20f, 0.20f, 0.25f, 1f);
        private static readonly Color TransBoxColor = new(0.18f, 0.22f, 0.22f, 1f);

        // ─── JSON ─────────────────────────────────────
        private static JsonSerializerSettings JsonSettings => new()
        {
            Formatting        = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
        };

        private class TableWrapper { public List<UnitData> Rows = new(); }

        // ══════════════════════════════════════════════
        [MenuItem("Tools/Data/Unit Editor")]
        public static void Open()
        {
            var w = GetWindow<UnitEditorWindow>("Unit Editor");
            w.minSize = new Vector2(900, 540);
            w.Show();
        }

        private void OnEnable()
        {
            LoadData();
            RefreshPrefabEntries();
            RefreshTypeArrays();
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

            _prefabEntries.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal));
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

            UnitData byPath = _rows.FirstOrDefault(r => string.Equals(r.PrefabPath, entry.AssetPath, StringComparison.Ordinal));
            if (byPath != null)
            {
                return byPath;
            }

            return _rows.FirstOrDefault(r => string.Equals(r.Name, entry.DisplayName, StringComparison.Ordinal));
        }

        private UnitData CreateUnitDataForPrefab(UnitPrefabEntry entry)
        {
            var row = new UnitData
            {
                Name = entry.DisplayName,
                Description = "",
                PrefabPath = entry.AssetPath,
            };
            _rows.Add(row);
            NormalizeRowIds();

            _isDirty = true;
            return row;
        }

        private UnitData CreateUnitDataForPrefab(UnitPrefabEntry entry, UnitData source)
        {
            if (source == null)
                return CreateUnitDataForPrefab(entry);

            string json = JsonConvert.SerializeObject(source, JsonSettings);
            UnitData row = JsonConvert.DeserializeObject<UnitData>(json, JsonSettings);
            if (row == null)
                return CreateUnitDataForPrefab(entry);

            row.Name = entry.DisplayName;
            row.PrefabPath = entry.AssetPath;
            _rows.Add(row);
            NormalizeRowIds();
            _isDirty = true;
            return row;
        }

        private static bool HasAuthoring<T>(UnitPrefabEntry entry) where T : Component
        {
            return entry?.Prefab != null && entry.Prefab.GetComponent<T>() != null;
        }

        private static void MarkPrefabDirty(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            EditorUtility.SetDirty(target);
        }

        // ══════════════════════════════════════════════
        //  类型反射
        // ══════════════════════════════════════════════
        private void RefreshTypeArrays()
        {
            _stateTypeNames   = CollectTypeNames(typeof(AUnitState),   subclass: true);
            _sourceTypeNames  = CollectTypeNames(typeof(ISource),      subclass: false);
            _compareTypeNames = CollectTypeNames(typeof(ICompareType), subclass: false);
        }

        private static string[] CollectTypeNames(Type baseType, bool subclass)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t =>
                {
                    if (t.IsAbstract || t.IsInterface) return false;
                    return subclass ? t.IsSubclassOf(baseType) : baseType.IsAssignableFrom(t);
                })
                .Select(t => t.Name)
                .OrderBy(n => n)
                .ToArray();
        }

        // ══════════════════════════════════════════════
        //  加载 / 保存
        // ══════════════════════════════════════════════
        private void LoadData()
        {
            _rows.Clear();
            _selectedIndex = -1;
            _isDirty       = false;

            if (!File.Exists(DataPath))
            {
                _statusText = $"未找到文件：{DataPath}，将新建";
                return;
            }

            try
            {
                string json    = File.ReadAllText(DataPath);
                var    wrapper = JsonConvert.DeserializeObject<TableWrapper>(json, JsonSettings);
                if (wrapper?.Rows != null) _rows = wrapper.Rows;
                foreach (var r in _rows) r.States ??= new List<UnitStateConfig>();
                NormalizeRowIds();
                _insertTexts.Clear();
                _statusText = $"已加载 {_rows.Count} 条  ·  {DataPath}";
            }
            catch (Exception ex)
            {
                _statusText = $"加载失败：{ex.Message}";
                Debug.LogError($"[UnitEditor] Load error:\n{ex}");
            }
        }

        private void SaveData()
        {
            string dir = Path.GetDirectoryName(DataPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);

            try
            {
                NormalizeRowIds();
                string json = JsonConvert.SerializeObject(new TableWrapper { Rows = _rows }, JsonSettings);
                File.WriteAllText(DataPath, json, Encoding.UTF8);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                _isDirty    = false;
                _statusText = $"已保存 {_rows.Count} 条  ·  {DataPath}";
            }
            catch (Exception ex)
            {
                _statusText = $"保存失败：{ex.Message}";
                Debug.LogError($"[UnitEditor] Save error:\n{ex}");
            }
        }

        // ══════════════════════════════════════════════
        //  增删
        // ══════════════════════════════════════════════
        private void AddUnit()
        {
            _rows.Add(new UnitData { Id = _rows.Count + 1, Name = $"新单位 {_rows.Count + 1}" });
            NormalizeRowIds();
            _selectedIndex = _rows.Count - 1;
            _isDirty = true;
            Repaint();
        }

        private void DeleteSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count) return;
            UnitData removedRow = _rows[_selectedIndex];
            _rows.RemoveAt(_selectedIndex);
            _insertTexts.Remove(removedRow);
            NormalizeRowIds();
            _selectedIndex = Mathf.Clamp(_selectedIndex, -1, _rows.Count - 1);
            _isDirty = true;
        }

        private void NormalizeRowIds()
        {
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].Id = i + 1;
        }

        private void MoveRowToInsertIndex(int fromIndex, int insertIndex)
        {
            if (fromIndex < 0 || fromIndex >= _rows.Count)
                return;

            insertIndex = Mathf.Clamp(insertIndex, 0, _rows.Count - 1);
            if (fromIndex == insertIndex)
                return;

            UnitData row = _rows[fromIndex];
            _rows.RemoveAt(fromIndex);

            insertIndex = Mathf.Clamp(insertIndex, 0, _rows.Count);
            _rows.Insert(insertIndex, row);
            NormalizeRowIds();
            _selectedIndex = insertIndex;
            _isDirty = true;
            GUI.FocusControl(null);
            Repaint();
        }

        // ══════════════════════════════════════════════
        //  OnGUI 入口
        // ══════════════════════════════════════════════
        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.BeginHorizontal();
            DrawListPanel();
            DrawPanelDivider();
            DrawDetailPanel();
            EditorGUILayout.EndHorizontal();
        }

        // ══════════════════════════════════════════════
        //  工具栏
        // ══════════════════════════════════════════════
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("加载", EditorStyles.toolbarButton, GUILayout.Width(44)))
            {
                LoadData();
                RefreshPrefabEntries();
            }

            GUI.enabled = _isDirty;
            if (GUILayout.Button(_isDirty ? "保存 *" : "保存", EditorStyles.toolbarButton, GUILayout.Width(52))) SaveData();
            GUI.enabled = true;

            if (GUILayout.Button("刷新Prefab", EditorStyles.toolbarButton, GUILayout.Width(74))) RefreshPrefabEntries();

            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(_statusText))
                GUILayout.Label(_statusText, EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
            EditorGUILayout.EndHorizontal();
        }

        // ══════════════════════════════════════════════
        //  左侧列表
        // ══════════════════════════════════════════════
        private void DrawListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(ListPanelWidth), GUILayout.ExpandHeight(true));
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"Prefab 列表 ({_prefabEntries.Count})", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _listScrollPos = EditorGUILayout.BeginScrollView(_listScrollPos, GUILayout.ExpandHeight(true));
            Event evt = Event.current;

            for (int i = 0; i < _prefabEntries.Count; i++)
            {
                UnitPrefabEntry entry = _prefabEntries[i];
                UnitData unitData = ResolveUnitData(entry);
                bool     isSelected = i == _selectedIndex;
                Rect     itemRect   = GUILayoutUtility.GetRect(ListPanelWidth, ItemHeight, GUILayout.ExpandWidth(true));

                Color bg = isSelected ? SelectedColor
                    : itemRect.Contains(evt.mousePosition) ? HoverColor
                    : i % 2 == 0 ? EvenRowColor : OddRowColor;
                EditorGUI.DrawRect(itemRect, bg);

                string bindingLabel = unitData == null
                    ? "(未绑定 UnitData)"
                    : $"[{unitData.Id}] {unitData.Name}";
                string label = $"{entry.DisplayName}  {bindingLabel}";
                GUI.Label(new Rect(itemRect.x + 8f, itemRect.y + 4f, itemRect.width - 16f, itemRect.height - 4f),
                    label, isSelected ? EditorStyles.whiteLabel : EditorStyles.label);

                if (evt.type == EventType.MouseDown && itemRect.Contains(evt.mousePosition))
                {
                    _selectedIndex = i;
                    evt.Use();
                    Repaint();
                }
                if (evt.type == EventType.MouseMove) Repaint();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawPanelDivider()
        {
            Rect r = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(r, DividerColor);
        }

        // ══════════════════════════════════════════════
        //  右侧详情
        // ══════════════════════════════════════════════
        private void DrawDetailPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (!TryGetSelectedPrefabEntry(out UnitPrefabEntry entry))
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("← 从左侧选择一个 Prefab", EditorStyles.centeredGreyMiniLabel);
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
                EditorGUILayout.HelpBox("当前 prefab 还没有匹配到 UnitData。会按 PrefabPath 自动绑定；如果还没有对应数据，可以直接创建一条。", MessageType.Info);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            GUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);
            string[] tabs = { "属性", "状态", "行为" };
            _selectedTab = Mathf.Clamp(_selectedTab, 0, tabs.Length - 1);
            int newTab = GUILayout.Toolbar(_selectedTab, tabs, GUILayout.Width(260), GUILayout.Height(24));
            if (newTab != _selectedTab) { _selectedTab = newTab; GUI.FocusControl(null); Repaint(); }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(2);
            Rect lineRect = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(lineRect, SectionLine);
            GUILayout.Space(4);

            float prev = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = LabelWidth;

            switch (_selectedTab)
            {
                case 0: DrawAttributePanel(entry, unit); break;
                case 1: DrawStatePreviewPanel(entry, unit); break;
                case 2: DrawBehaviorPreviewPanel(entry); break;
            }

            EditorGUIUtility.labelWidth = prev;
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawBindingPanel(UnitPrefabEntry entry, ref UnitData unit)
        {
            DrawSectionHeader("Prefab 绑定");

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField("Prefab", entry.AssetPath);

            if (unit != null)
            {
                EditorGUILayout.LabelField("绑定方式", "按 PrefabPath 自动匹配");
                EditorGUILayout.LabelField("当前数据", $"[{unit.Id}] {unit.Name}");
            }
            else
            {
                EditorGUILayout.HelpBox("还没有与这个 PrefabPath 对应的 UnitData。", MessageType.Info);
            }

            if (unit == null && GUILayout.Button("为当前 Prefab 创建 UnitData", GUILayout.Width(180)))
            {
                unit = CreateUnitDataForPrefab(entry);
            }

            if (unit == null && _rows.Count > 0)
            {
                string[] options = _rows.Select(row => $"[{row.Id}] {row.Name}").ToArray();
                _copySourceUnitIndex = Mathf.Clamp(_copySourceUnitIndex, 0, options.Length - 1);
                _copySourceUnitIndex = EditorGUILayout.Popup("复制来源", _copySourceUnitIndex, options);

                if (GUILayout.Button("复制已有 UnitData 生成", GUILayout.Width(180)))
                {
                    unit = CreateUnitDataForPrefab(entry, _rows[_copySourceUnitIndex]);
                }
            }
        }

        private void DrawNpcInteractableSection(UnitPrefabEntry entry)
        {
            NPCInteractableAuthoring npcAuthoring = entry.Prefab.GetComponent<NPCInteractableAuthoring>();
            if (npcAuthoring == null)
            {
                return;
            }

            GUILayout.Space(8);
            DrawSectionHeader("NPC 交互");

            List<NPCData> npcRows = EditorComponents.Data.FindAll<NPCData>(_ => true)
                .OrderBy(row => row.Id)
                .ToList();

            List<string> options = new() { "未绑定" };
            int selectedIndex = 0;
            for (int i = 0; i < npcRows.Count; i++)
            {
                NPCData row = npcRows[i];
                options.Add($"[{row.Id}] {row.DisplayName} ({row.NPC})");
                if (row.Id == npcAuthoring.NpcDataId)
                {
                    selectedIndex = i + 1;
                }
            }

            int newIndex = EditorGUILayout.Popup("NPCData", selectedIndex, options.ToArray());
            int newNpcId = newIndex == 0 ? 0 : npcRows[newIndex - 1].Id;
            float newRange = EditorGUILayout.FloatField("交互范围", npcAuthoring.InteractRange);

            if (newNpcId != npcAuthoring.NpcDataId || !Mathf.Approximately(newRange, npcAuthoring.InteractRange))
            {
                npcAuthoring.NpcDataId = newNpcId;
                npcAuthoring.InteractRange = newRange;
                MarkPrefabDirty(npcAuthoring);
                _isDirty = true;
            }
        }

        private void DrawFactionSection(UnitPrefabEntry entry)
        {
            UnitFactionAuthoring factionAuthoring = entry.Prefab.GetComponent<UnitFactionAuthoring>();
            if (factionAuthoring == null)
            {
                return;
            }

            GUILayout.Space(8);
            DrawSectionHeader("Faction");

            UnitFactionType newFaction = (UnitFactionType)EditorGUILayout.EnumPopup("阵营", factionAuthoring.Faction);
            if (newFaction != factionAuthoring.Faction)
            {
                factionAuthoring.Faction = newFaction;
                MarkPrefabDirty(factionAuthoring);
                _isDirty = true;
            }
        }

        // ══════════════════════════════════════════════
        //  属性面板
        // ══════════════════════════════════════════════
        private void DrawPerceptionSection(UnitPrefabEntry entry)
        {
            UnitPerceptionAuthoring perceptionAuthoring = entry.Prefab.GetComponent<UnitPerceptionAuthoring>();
            if (perceptionAuthoring == null)
            {
                return;
            }

            GUILayout.Space(8);
            DrawSectionHeader("Perception（感知）");

            float newSearchRadius = EditorGUILayout.FloatField("搜索范围", perceptionAuthoring.SearchRadius);
            if (!Mathf.Approximately(newSearchRadius, perceptionAuthoring.SearchRadius))
            {
                perceptionAuthoring.SearchRadius = newSearchRadius;
                MarkPrefabDirty(perceptionAuthoring);
                _isDirty = true;
            }
        }

        private void DrawAttributePanel(UnitPrefabEntry entry, UnitData unit)
        {
            EditorGUI.BeginChangeCheck();

            DrawSectionHeader("基础信息");
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.IntField("Id", unit.Id);
            unit.Name        = EditorGUILayout.TextField("名称",   unit.Name        ?? "");
            EditorGUILayout.LabelField("描述");
            unit.Description = EditorGUILayout.TextArea(unit.Description ?? "",
                GUILayout.MinHeight(48), GUILayout.MaxHeight(80));
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField("预制体路径", entry.AssetPath);

            if (unit.PrefabPath != entry.AssetPath)
            {
                unit.PrefabPath = entry.AssetPath;
                _isDirty = true;
            }

            DrawFactionSection(entry);

            if (HasAuthoring<UnitMoveAuthoring>(entry))
            {
                GUILayout.Space(8);
                DrawSectionHeader("Move（移动）");
                unit.BaseMoveSpeed       = EditorGUILayout.FloatField("最大速度",       unit.BaseMoveSpeed);
                unit.BaseMaxAcceleration = EditorGUILayout.FloatField("最大加速度",     unit.BaseMaxAcceleration);
            }

            if (HasAuthoring<UnitVitalityAuthoring>(entry))
            {
                GUILayout.Space(8);
                DrawSectionHeader("Vitality（生存）");
                unit.BaseMaxHealth = EditorGUILayout.FloatField("最大生命值", unit.BaseMaxHealth);
                unit.BaseDefense   = EditorGUILayout.FloatField("防御力",     unit.BaseDefense);
            }

            if (HasAuthoring<UnitAttackAuthoring>(entry))
            {
                GUILayout.Space(8);
                DrawSectionHeader("Attack（攻击）");
                unit.BaseAttackPower = EditorGUILayout.FloatField("攻击力",   unit.BaseAttackPower);
                unit.BaseSkillRange  = EditorGUILayout.FloatField("技能范围", unit.BaseSkillRange);
            }

            if (HasAuthoring<UnitManaAuthoring>(entry))
            {
                GUILayout.Space(8);
                DrawSectionHeader("Mana（法力）");
                unit.BaseMaxMp = EditorGUILayout.FloatField("最大魔力值", unit.BaseMaxMp);
            }

            DrawPerceptionSection(entry);
            DrawNpcInteractableSection(entry);

            if (EditorGUI.EndChangeCheck()) _isDirty = true;
        }

        private void DrawStatePreviewPanel(UnitPrefabEntry entry, UnitData unit)
        {
            DrawSectionHeader("State");
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("State Machine", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Open State Editor", GUILayout.Width(140f)))
                StateMachineGraphWindow.Open();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("State data is preview-only here. Edit it in the State Machine editor.", MessageType.Info);

            if (!HasAuthoring<UnitStateMachineAuthoring>(entry))
            {
                EditorGUILayout.HelpBox("This prefab does not have UnitStateMachineAuthoring.", MessageType.Warning);
                return;
            }

            List<UnitStateConfig> states = unit?.States ?? new List<UnitStateConfig>();
            EditorGUILayout.LabelField("State Count", states.Count.ToString());
            EditorGUILayout.LabelField("Initial State", states.Count > 0 ? states[0].StateType : "None");

            if (states.Count == 0)
            {
                EditorGUILayout.HelpBox("No state config found on the bound UnitData.", MessageType.Info);
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
                BehaviorTreeGraphWindow.Open();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("Behavior data is preview-only here. Edit it in the Behavior Tree editor.", MessageType.Info);

            UnitBehaviorTreeAuthoring authoring = entry.Prefab.GetComponent<UnitBehaviorTreeAuthoring>();
            if (authoring == null)
            {
                EditorGUILayout.HelpBox("This prefab does not have UnitBehaviorTreeAuthoring.", MessageType.Warning);
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
                EditorGUILayout.HelpBox("BehaviorTreeData was not found for the current BehaviorTreeId.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Tree", $"[{tree.Id}] {GetBehaviorTreePreviewName(tree)}");
            if (!string.IsNullOrWhiteSpace(tree.Description))
                EditorGUILayout.LabelField("Description", tree.Description, EditorStyles.wordWrappedLabel);

            EditorGUILayout.LabelField("Node Count", tree.Nodes?.Count.ToString() ?? "0");

            BehaviorNodeData rootNode = tree.GetRootNode();
            EditorGUILayout.LabelField(
                "Root",
                rootNode != null ? BehaviorNodeDataRegistry.GetDisplayName(rootNode.Type) : "None");

            if (tree.Nodes == null || tree.Nodes.Count == 0)
            {
                EditorGUILayout.HelpBox("This behavior tree has no nodes.", MessageType.Info);
                return;
            }

            GUILayout.Space(6f);
            for (int nodeIndex = 0; nodeIndex < tree.Nodes.Count; nodeIndex++)
            {
                BehaviorNodeData node = tree.Nodes[nodeIndex];
                if (node == null)
                    continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(
                    $"[{nodeIndex + 1}] {BehaviorNodeDataRegistry.GetDisplayName(node.Type)}",
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField(BehaviorNodeDataRegistry.GetSummary(node), EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
                GUILayout.Space(4f);
            }
        }

        private static string GetTransitionPreviewText(UnitTransitionConfig transition)
        {
            List<ConditionConfig> conditions = transition?.Conditions ?? new List<ConditionConfig>();
            if (conditions.Count == 0)
                return "Always";

            return string.Join(" | ", conditions.Select(GetConditionPreviewText));
        }

        private static string GetConditionPreviewText(ConditionConfig condition)
        {
            if (condition == null)
                return "None";

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
                return tree.Name;

            return "Unnamed Tree";
        }

        // ══════════════════════════════════════════════
        //  AI 面板
        // ══════════════════════════════════════════════
        private void DrawAIPanel(UnitData unit)
        {
            unit.States ??= new List<UnitStateConfig>();

            // ── 顶部：添加状态 ────────────────────────
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("状态机", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (_stateTypeNames.Length == 0)
            {
                GUILayout.Label("（未找到 AUnitState 子类）", EditorStyles.miniLabel);
            }
            else
            {
                _addStateTypeIndex = EditorGUILayout.Popup(
                    _addStateTypeIndex, _stateTypeNames, GUILayout.Width(150));

                if (GUILayout.Button("＋ 添加状态", GUILayout.Width(76)))
                {
                    string typeName = _stateTypeNames[_addStateTypeIndex];
                    if (!unit.States.Exists(s => s.StateType == typeName))
                    {
                        unit.States.Add(new UnitStateConfig { StateType = typeName });
                        _isDirty = true;
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("提示", $"状态 {typeName} 已存在", "OK");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);
            Rect div = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(div, SectionLine);
            GUILayout.Space(6);

            // ── 状态列表 ──────────────────────────────
            if (unit.States.Count == 0)
            {
                GUILayout.Label("暂无状态，点击右上角「＋ 添加状态」", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            int removeState = -1;
            for (int si = 0; si < unit.States.Count; si++)
            {
                if (!DrawStateConfig(unit, si)) removeState = si;
                GUILayout.Space(4);
            }

            if (removeState >= 0)
            {
                unit.States.RemoveAt(removeState);
                _isDirty = true;
            }
        }

        /// <summary>
        /// 绘制一个状态块。返回 false 表示该状态需要被删除。
        /// </summary>
        private bool DrawStateConfig(UnitData unit, int si)
        {
            UnitStateConfig state   = unit.States[si];
            string          key     = $"s{si}";
            if (!_stateFoldStates.ContainsKey(key)) _stateFoldStates[key] = true;

            // 外框
            Rect boxRect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(boxRect, StateBoxColor);

            // 标题行
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _stateFoldStates[key] = EditorGUILayout.Foldout(
                _stateFoldStates[key],
                $"  {state.StateType}  （{state.Transitions.Count} 条转换）",
                true, EditorStyles.foldoutHeader);

            GUILayout.FlexibleSpace();
            GUI.color = DangerColor;
            bool remove = GUILayout.Button("删除", EditorStyles.toolbarButton, GUILayout.Width(44));
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            if (_stateFoldStates[key])
            {
                GUILayout.Space(4);
                EditorGUI.indentLevel++;

                // 添加转换行
                DrawAddTransitionRow(unit, si, key);
                GUILayout.Space(4);

                // 各转换条目
                int removeTrans = -1;
                for (int ti = 0; ti < state.Transitions.Count; ti++)
                {
                    if (!DrawTransitionConfig(unit, si, ti, key)) removeTrans = ti;
                    GUILayout.Space(2);
                }
                if (removeTrans >= 0)
                {
                    state.Transitions.RemoveAt(removeTrans);
                    _isDirty = true;
                }

                EditorGUI.indentLevel--;
                GUILayout.Space(4);
            }

            EditorGUILayout.EndVertical();

            // 边框线
            GUI.Box(boxRect, GUIContent.none);

            return !remove;
        }

        private void DrawAddTransitionRow(UnitData unit, int si, string stateKey)
        {
            if (_stateTypeNames.Length == 0) return;

            string addKey = $"{stateKey}_add";
            if (!_transAddTargetIdx.ContainsKey(addKey)) _transAddTargetIdx[addKey] = 0;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16);
            GUILayout.Label("→ 添加转换至:", GUILayout.Width(80));
            _transAddTargetIdx[addKey] = EditorGUILayout.Popup(
                _transAddTargetIdx[addKey], _stateTypeNames, GUILayout.Width(150));

            if (GUILayout.Button("＋ 转换", GUILayout.Width(60)))
            {
                string target = _stateTypeNames[_transAddTargetIdx[addKey]];
                unit.States[si].Transitions.Add(new UnitTransitionConfig { TargetStateType = target });
                _isDirty = true;
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制一条转换。返回 false 表示需要删除。
        /// </summary>
        private bool DrawTransitionConfig(UnitData unit, int si, int ti, string stateKey)
        {
            UnitTransitionConfig trans  = unit.States[si].Transitions[ti];
            string               tKey  = $"{stateKey}_t{ti}";
            if (!_transitionFoldStates.ContainsKey(tKey)) _transitionFoldStates[tKey] = true;

            Rect boxRect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(boxRect, TransBoxColor);

            // 转换标题行
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            _transitionFoldStates[tKey] = EditorGUILayout.Foldout(
                _transitionFoldStates[tKey],
                $"→  {trans.TargetStateType}  （{trans.Conditions.Count} 个条件）",
                true);

            GUILayout.FlexibleSpace();
            GUI.color = DangerColor;
            bool remove = GUILayout.Button("×", GUILayout.Width(24));
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            if (_transitionFoldStates[tKey])
            {
                GUILayout.Space(2);
                EditorGUI.indentLevel++;

                // 添加条件行
                DrawAddConditionRow(unit, si, ti, tKey);

                // 条件列表
                int removeCond = -1;
                for (int ci = 0; ci < trans.Conditions.Count; ci++)
                {
                    if (!DrawConditionRow(trans.Conditions[ci]))
                        removeCond = ci;
                }
                if (removeCond >= 0)
                {
                    trans.Conditions.RemoveAt(removeCond);
                    _isDirty = true;
                }

                EditorGUI.indentLevel--;
                GUILayout.Space(2);
            }

            EditorGUILayout.EndVertical();
            return !remove;
        }

        private void DrawAddConditionRow(UnitData unit, int si, int ti, string tKey)
        {
            string srcKey = $"{tKey}_src";
            string cmpKey = $"{tKey}_cmp";
            if (!_condAddSrcIdx.ContainsKey(srcKey)) _condAddSrcIdx[srcKey] = 0;
            if (!_condAddCmpIdx.ContainsKey(cmpKey)) _condAddCmpIdx[cmpKey] = 0;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16);

            if (_sourceTypeNames.Length > 0)
            {
                _condAddSrcIdx[srcKey] = EditorGUILayout.Popup(
                    _condAddSrcIdx[srcKey], _sourceTypeNames, GUILayout.Width(130));
            }
            else
            {
                GUILayout.Label("（无 ISource）", EditorStyles.miniLabel, GUILayout.Width(80));
            }

            if (_compareTypeNames.Length > 0)
            {
                _condAddCmpIdx[cmpKey] = EditorGUILayout.Popup(
                    _condAddCmpIdx[cmpKey], _compareTypeNames, GUILayout.Width(100));
            }

            if (GUILayout.Button("＋ 条件", GUILayout.Width(60)))
            {
                string srcType = _sourceTypeNames.Length > 0 ? _sourceTypeNames[_condAddSrcIdx[srcKey]] : "";
                string cmpType = _compareTypeNames.Length > 0 ? _compareTypeNames[_condAddCmpIdx[cmpKey]] : "";
                unit.States[si].Transitions[ti].Conditions.Add(new ConditionConfig
                {
                    SourceType    = srcType,
                    CompareType   = cmpType,
                    ConditionType = ConditionType.Necessary,
                });
                _isDirty = true;
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制一行条件。返回 false 表示删除。
        /// </summary>
        private bool DrawConditionRow(ConditionConfig cond)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16);

            EditorGUI.BeginChangeCheck();

            // 条件类型（Necessary / Unallowed）
            cond.ConditionType = (ConditionType)EditorGUILayout.EnumPopup(
                cond.ConditionType, GUILayout.Width(88));

            // ISource
            if (_sourceTypeNames.Length > 0)
            {
                int idx = Mathf.Max(0, Array.IndexOf(_sourceTypeNames, cond.SourceType));
                idx            = EditorGUILayout.Popup(idx, _sourceTypeNames, GUILayout.Width(130));
                cond.SourceType = _sourceTypeNames[idx];
            }
            else
            {
                cond.SourceType = EditorGUILayout.TextField(cond.SourceType, GUILayout.Width(130));
            }

            // ICompareType
            if (_compareTypeNames.Length > 0)
            {
                int idx = Mathf.Max(0, Array.IndexOf(_compareTypeNames, cond.CompareType));
                idx              = EditorGUILayout.Popup(idx, _compareTypeNames, GUILayout.Width(100));
                cond.CompareType = _compareTypeNames[idx];
            }
            else
            {
                cond.CompareType = EditorGUILayout.TextField(cond.CompareType, GUILayout.Width(100));
            }

            // 阈值（仅 GreaterThan / LessThan / Equal 需要）
            bool needsValue = cond.CompareType is "GreaterThan" or "LessThan" or "Equal";
            if (needsValue)
                cond.CompareValue = EditorGUILayout.FloatField(cond.CompareValue, GUILayout.Width(60));
            else
                GUILayout.Space(64);

            if (EditorGUI.EndChangeCheck()) _isDirty = true;

            GUILayout.FlexibleSpace();
            GUI.color = DangerColor;
            bool remove = GUILayout.Button("×", GUILayout.Width(24));
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            return !remove;
        }

        // ══════════════════════════════════════════════
        //  工具方法
        // ══════════════════════════════════════════════
        private static void DrawSectionHeader(string title)
        {
            GUILayout.Space(6);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            Rect r = GUILayoutUtility.GetLastRect();
            r.y += r.height + 1; r.height = 1;
            EditorGUI.DrawRect(r, SectionLine);
            GUILayout.Space(4);
        }
    }
}
