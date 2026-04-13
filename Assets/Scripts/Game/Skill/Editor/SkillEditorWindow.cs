using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;

namespace CrystalMagic.Editor.Skill
{
    /// <summary>
    /// 技能编辑器
    /// 左侧：技能滑动列表；右侧：选中技能的 SkillData 完整配置（含效果链）
    /// 菜单路径：Tools/Data/Skill Editor
    /// </summary>
    public class SkillEditorWindow : EditorWindow
    {
        private const string DataPath       = "Assets/Res/Data/SkillDataTable.json";
        private const float  ListPanelWidth = 220f;
        private const float  ItemHeight     = 26f;
        private const float  LabelWidth     = 150f;

        // ===== 效果类型注册 =====
        private static readonly Type[]   KnownEffectTypes =
        {
            typeof(AreaSearchEffectData),
            typeof(DamageEffectData),
            typeof(PersistentEffectData),
            typeof(SpawnProjectileEffectData),
            typeof(SpawnSoundEffectData),
            typeof(SpawnVfxEffectData),
        };
        private static readonly string[] KnownEffectNames =
        {
            "范围搜索 (AreaSearch)",
            "伤害 (Damage)",
            "持续效果 (Persistent)",
            "创建投射物 (SpawnProjectile)",
            "生成音效 (SpawnSound)",
            "生成特效 (SpawnVfx)",
        };
        private static readonly Color[] EffectColors =
        {
            new(0.14f, 0.38f, 0.60f),  // AreaSearch  — 蓝
            new(0.60f, 0.18f, 0.14f),  // Damage      — 红
            new(0.14f, 0.50f, 0.24f),  // Persistent  — 绿
            new(0.55f, 0.38f, 0.10f),  // Projectile  — 橙
            new(0.38f, 0.18f, 0.55f),  // Sound       — 紫
            new(0.18f, 0.48f, 0.48f),  // Vfx         — 青
        };

        // ===== 数据 =====
        private List<SkillData> _rows = new();
        private bool   _isDirty;
        private string _statusText = "";

        // ===== UI 状态 =====
        private int     _selectedIndex      = -1;
        private int     _addEffectTypeIndex;
        private Vector2 _listScrollPos;
        private Vector2 _detailScrollPos;

        // 每个嵌套效果链的"待添加类型"选中索引，key = 字段路径
        private readonly Dictionary<string, int>  _nestedTypeIndices = new();
        // 每个效果条目的折叠状态，key = 条目路径，true = 展开
        private readonly Dictionary<string, bool> _effectFoldStates  = new();
        // 条件列表的折叠状态
        private readonly Dictionary<string, bool> _condFoldStates    = new();

        // ===== 条件相关反射类型 =====
        private string[] _sourceTypeNames  = Array.Empty<string>();
        private string[] _compareTypeNames = Array.Empty<string>();
        private readonly Dictionary<string, int> _condAddSrcIdx = new();
        private readonly Dictionary<string, int> _condAddCmpIdx = new();

        // ===== 颜色 =====
        private static readonly Color SelectedColor = new(0.27f, 0.52f, 0.85f, 0.85f);
        private static readonly Color EvenRowColor  = new(0.22f, 0.22f, 0.22f, 1f);
        private static readonly Color OddRowColor   = new(0.25f, 0.25f, 0.25f, 1f);
        private static readonly Color HoverColor    = new(0.32f, 0.32f, 0.32f, 1f);
        private static readonly Color SectionLine   = new(0.45f, 0.45f, 0.45f, 1f);
        private static readonly Color DividerColor  = new(0.15f, 0.15f, 0.15f, 1f);

        // ===== JSON 设置 =====
        private static JsonSerializerSettings JsonSettings => new()
        {
            TypeNameHandling    = TypeNameHandling.Auto,
            Formatting          = Formatting.Indented,
            FloatFormatHandling = FloatFormatHandling.String,   // 正确序列化 Infinity / NaN
            Converters          = { new LayerMaskConverter(), new Vector3Converter(), new GameObjectConverter() },
        };

        private class TableWrapper { public List<SkillData> Rows = new(); }

        // ─────────────────────────────────────────
        [MenuItem("Tools/Data/Skill Editor")]
        public static void Open()
        {
            var w = GetWindow<SkillEditorWindow>("Skill Editor");
            w.minSize = new Vector2(920, 560);
            w.Show();
        }

        private void OnEnable()
        {
            LoadData();
            RefreshTypeArrays();
        }

        private void RefreshTypeArrays()
        {
            _sourceTypeNames  = CollectTypeNames(typeof(ISource));
            _compareTypeNames = CollectTypeNames(typeof(ICompareType));
        }

        private static string[] CollectTypeNames(Type baseType)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => !t.IsAbstract && !t.IsInterface && baseType.IsAssignableFrom(t))
                .Select(t => t.Name)
                .OrderBy(n => n)
                .ToArray();
        }

        // ─────────────────────────────────────────
        //  加载 / 保存
        // ─────────────────────────────────────────
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
                string json = File.ReadAllText(DataPath);
                var wrapper = JsonConvert.DeserializeObject<TableWrapper>(json, JsonSettings);
                if (wrapper?.Rows != null) _rows = wrapper.Rows;
                _statusText = $"已加载 {_rows.Count} 条  ·  {DataPath}";
            }
            catch (Exception ex)
            {
                _statusText = $"加载失败：{ex.Message}";
                Debug.LogError($"[SkillEditor] Load error:\n{ex}");
            }
        }

        private void SaveData()
        {
            string dir = Path.GetDirectoryName(DataPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            try
            {
                string json = JsonConvert.SerializeObject(new TableWrapper { Rows = _rows }, JsonSettings);
                File.WriteAllText(DataPath, json, Encoding.UTF8);
                AssetDatabase.Refresh();
                _isDirty    = false;
                _statusText = $"已保存 {_rows.Count} 条  ·  {DataPath}";
                Debug.Log($"[SkillEditor] Saved {DataPath}");
            }
            catch (Exception ex)
            {
                _statusText = $"保存失败：{ex.Message}";
                Debug.LogError($"[SkillEditor] Save error:\n{ex}");
            }
        }

        // ─────────────────────────────────────────
        //  新增 / 删除技能
        // ─────────────────────────────────────────
        private void AddSkill()
        {
            int maxId = 0;
            foreach (SkillData r in _rows) if (r.Id > maxId) maxId = r.Id;

            _rows.Add(new SkillData
            {
                Id                  = maxId + 1,
                Name                = $"新技能 {maxId + 1}",
                MoveSpeedMultiplier = 1f,
                EffectChain         = Array.Empty<EffectData>(),
            });
            _selectedIndex = _rows.Count - 1;
            _isDirty       = true;
            Repaint();
        }

        private void DeleteSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count) return;
            _rows.RemoveAt(_selectedIndex);
            _selectedIndex = Mathf.Clamp(_selectedIndex, -1, _rows.Count - 1);
            _isDirty = true;
        }

        // ─────────────────────────────────────────
        //  OnGUI
        // ─────────────────────────────────────────
        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.BeginHorizontal();
            DrawListPanel();
            DrawPanelDivider();
            DrawDetailPanel();
            EditorGUILayout.EndHorizontal();
        }

        // ─────────────────────────────────────────
        //  工具栏
        // ─────────────────────────────────────────
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("加载", EditorStyles.toolbarButton, GUILayout.Width(44)))
                LoadData();

            GUI.enabled = _isDirty;
            if (GUILayout.Button(_isDirty ? "保存 *" : "保存", EditorStyles.toolbarButton, GUILayout.Width(52)))
                SaveData();
            GUI.enabled = true;

            if (GUILayout.Button("+ 新增", EditorStyles.toolbarButton, GUILayout.Width(52)))
                AddSkill();

            GUI.enabled = _selectedIndex >= 0;
            GUI.color   = _selectedIndex >= 0 ? new Color(1f, 0.55f, 0.55f) : Color.white;
            if (GUILayout.Button("删除", EditorStyles.toolbarButton, GUILayout.Width(44)))
                if (EditorUtility.DisplayDialog("删除技能",
                    $"确认删除「{_rows[_selectedIndex].Name}」？", "删除", "取消"))
                    DeleteSelected();
            GUI.color   = Color.white;
            GUI.enabled = true;

            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(_statusText))
                GUILayout.Label(_statusText, EditorStyles.miniLabel, GUILayout.ExpandWidth(false));

            EditorGUILayout.EndHorizontal();
        }

        // ─────────────────────────────────────────
        //  左侧列表
        // ─────────────────────────────────────────
        private void DrawListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(ListPanelWidth), GUILayout.ExpandHeight(true));

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"技能列表 ({_rows.Count})", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _listScrollPos = EditorGUILayout.BeginScrollView(_listScrollPos, GUILayout.ExpandHeight(true));
            Event evt = Event.current;

            for (int i = 0; i < _rows.Count; i++)
            {
                SkillData skill      = _rows[i];
                bool      isSelected = i == _selectedIndex;

                Rect itemRect = GUILayoutUtility.GetRect(ListPanelWidth, ItemHeight, GUILayout.ExpandWidth(true));

                Color bg = isSelected ? SelectedColor
                    : (itemRect.Contains(evt.mousePosition) ? HoverColor
                        : (i % 2 == 0 ? EvenRowColor : OddRowColor));
                EditorGUI.DrawRect(itemRect, bg);

                string label = $"[{skill.Id}]  {(string.IsNullOrEmpty(skill.Name) ? "（未命名）" : skill.Name)}";
                GUI.Label(
                    new Rect(itemRect.x + 8, itemRect.y + 4, itemRect.width - 8, itemRect.height - 4),
                    label,
                    isSelected ? EditorStyles.whiteLabel : EditorStyles.label);

                if (evt.type == EventType.MouseDown && itemRect.Contains(evt.mousePosition))
                {
                    _selectedIndex = i;
                    GUI.FocusControl(null);
                    evt.Use();
                    Repaint();
                }
                if (evt.type == EventType.MouseMove)
                    Repaint();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawPanelDivider()
        {
            Rect r = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(r, DividerColor);
        }

        // ─────────────────────────────────────────
        //  右侧详情
        // ─────────────────────────────────────────
        private void DrawDetailPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("← 从左侧选择一个技能", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            SkillData skill = _rows[_selectedIndex];

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"[{skill.Id}]  {skill.Name}", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _detailScrollPos = EditorGUILayout.BeginScrollView(_detailScrollPos);

            float prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = LabelWidth;

            EditorGUI.BeginChangeCheck();

            // ── 基础信息 ──────────────────────────────────
            DrawSectionHeader("基础信息");
            skill.Id          = EditorGUILayout.IntField("Id", skill.Id);
            skill.Name        = EditorGUILayout.TextField("名称", skill.Name ?? "");
            EditorGUILayout.LabelField("描述");
            skill.Description = EditorGUILayout.TextArea(skill.Description ?? "",
                GUILayout.MinHeight(48), GUILayout.MaxHeight(80));
            skill.SkillType   = (SkillType)EditorGUILayout.EnumPopup("技能类型", skill.SkillType);
            skill.IconPath    = EditorGUILayout.TextField("图标路径", skill.IconPath ?? "");

            // ── 施法属性 ──────────────────────────────────
            DrawSectionHeader("施法属性");
            skill.MpCost           = EditorGUILayout.IntField("MP 消耗", skill.MpCost);
            skill.WindupDuration   = EditorGUILayout.FloatField("前摇 (s)", skill.WindupDuration);
            skill.RecoveryDuration = EditorGUILayout.FloatField("后摇 (s)", skill.RecoveryDuration);
            skill.CanMoveWhileCasting = EditorGUILayout.Toggle("施法可移动", skill.CanMoveWhileCasting);
            using (new EditorGUI.DisabledScope(!skill.CanMoveWhileCasting))
                skill.MoveSpeedMultiplier = EditorGUILayout.Slider("移动速度倍率", skill.MoveSpeedMultiplier, 0f, 1f);

            if (EditorGUI.EndChangeCheck())
                _isDirty = true;

            // ── 释放条件 ──────────────────────────────────
            DrawSectionHeader("释放条件");
            skill.Conditions ??= new List<ConditionConfig>();
            DrawConditionList(skill.Conditions, "_skill_cond_");

            // ── 效果链 ────────────────────────────────────
            DrawSectionHeader("效果链");
            DrawEffectChain(skill);

            EditorGUIUtility.labelWidth = prevLabelWidth;
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // ─────────────────────────────────────────
        //  顶层效果链（绑定到 SkillData）
        // ─────────────────────────────────────────
        private void DrawEffectChain(SkillData skill)
        {
            skill.EffectChain ??= Array.Empty<EffectData>();
            skill.EffectChain = DrawEffectChainInline("__root__", skill.EffectChain, ref _addEffectTypeIndex);
        }

        // ─────────────────────────────────────────
        //  通用效果链绘制（顶层与嵌套共用）
        //  stateKey   : 唯一标识该链的字符串，用于存取"待添加类型"选中状态
        //  chain      : 当前链数组（可能为 null）
        //  typeIndex  : 该链"待添加类型"下拉的选中索引（ref，允许内部修改）
        //  返回       : 修改后的数组（删除操作会产生新数组）
        // ─────────────────────────────────────────
        private EffectData[] DrawEffectChainInline(string stateKey, EffectData[] chain, ref int typeIndex)
        {
            chain ??= Array.Empty<EffectData>();

            // 添加行
            EditorGUILayout.BeginHorizontal();
            typeIndex = EditorGUILayout.Popup(typeIndex, KnownEffectNames, GUILayout.Width(190));
            if (GUILayout.Button("＋ 添加", GUILayout.Width(66)))
            {
                var newEffect = (EffectData)Activator.CreateInstance(KnownEffectTypes[typeIndex]);
                var list = new List<EffectData>(chain) { newEffect };
                chain    = list.ToArray();
                _isDirty = true;
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(2);

            if (chain.Length == 0)
            {
                EditorGUILayout.LabelField("（空）", EditorStyles.centeredGreyMiniLabel);
                return chain;
            }

            int deleteAt = -1;
            for (int i = 0; i < chain.Length; i++)
            {
                if (DrawEffectEntry(chain, i, stateKey))
                    deleteAt = i;
            }

            if (deleteAt >= 0)
            {
                var list = new List<EffectData>(chain);
                list.RemoveAt(deleteAt);
                chain    = list.ToArray();
                _isDirty = true;
            }

            return chain;
        }

        // 返回 true 表示该条目需要删除
        // parentKey 用于构造嵌套链的唯一状态键
        private bool DrawEffectEntry(EffectData[] chain, int index, string parentKey = "")
        {
            EffectData effect = chain[index];
            if (effect == null) return false;

            Type   effectType  = effect.GetType();
            int    typeIdx     = Array.IndexOf(KnownEffectTypes, effectType);
            Color  headerColor = typeIdx >= 0 ? EffectColors[typeIdx] : new Color(0.4f, 0.4f, 0.4f);
            string typeName    = typeIdx >= 0 ? KnownEffectNames[typeIdx] : effectType.Name;
            string displayName = typeName;

            // 折叠状态
            string entryKey = $"{parentKey}/{effectType.Name}[{index}]";
            if (!_effectFoldStates.TryGetValue(entryKey, out bool expanded))
                expanded = true;

            // 标题栏（手动应用缩进，GetControlRect 不受 indentLevel 影响）
            Rect headerRect = EditorGUILayout.GetControlRect(false, 22f);
            float indentOffset = EditorGUI.indentLevel * 15f;
            headerRect.x     += indentOffset;
            headerRect.width -= indentOffset;
            EditorGUI.DrawRect(headerRect, headerColor);

            // 按钮：折叠 ↑ ↓ ×（从右向左排）
            float bx = headerRect.xMax - 106;

            // 折叠按钮
            if (GUI.Button(new Rect(bx,      headerRect.y + 2, 22, 18),
                    expanded ? "▼" : "▶", EditorStyles.miniButton))
            {
                _effectFoldStates[entryKey] = !expanded;
                expanded = !expanded;
                Repaint();
            }

            GUI.enabled = index > 0;
            if (GUI.Button(new Rect(bx + 26, headerRect.y + 2, 22, 18), "↑", EditorStyles.miniButton))
            {
                (chain[index - 1], chain[index]) = (chain[index], chain[index - 1]);
                _isDirty = true;
                Repaint();
            }
            GUI.enabled = index < chain.Length - 1;
            if (GUI.Button(new Rect(bx + 50, headerRect.y + 2, 22, 18), "↓", EditorStyles.miniButton))
            {
                (chain[index], chain[index + 1]) = (chain[index + 1], chain[index]);
                _isDirty = true;
                Repaint();
            }
            GUI.enabled = true;

            GUI.color = new Color(1f, 0.5f, 0.5f);
            bool deleted = GUI.Button(new Rect(bx + 78, headerRect.y + 2, 24, 18), "×", EditorStyles.miniButton);
            GUI.color = Color.white;

            // 标题文字（为按钮组留出空间）
            GUI.Label(
                new Rect(headerRect.x + 6, headerRect.y + 3, bx - headerRect.x - 10, 18),
                $"#{index + 1}  {displayName}",
                EditorStyles.whiteLabel);

            // 字段（折叠时隐藏）
            if (expanded)
            {
                EditorGUI.indentLevel++;
                GUILayout.Space(2);

                // 效果释放条件（来自基类 EffectData.Conditions）
                effect.Conditions ??= new List<ConditionConfig>();
                DrawConditionList(effect.Conditions, $"{entryKey}_cond_");
                GUILayout.Space(4);

                // 子类字段
                foreach (FieldInfo field in effectType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    EditorGUI.BeginChangeCheck();
                    object oldVal = field.GetValue(effect);
                    object newVal = DrawEffectField(field.FieldType, field.Name, oldVal, entryKey);
                    if (EditorGUI.EndChangeCheck())
                    {
                        field.SetValue(effect, newVal);
                        _isDirty = true;
                    }
                }
                GUILayout.Space(6);
                EditorGUI.indentLevel--;
            }

            return deleted;
        }

        // ─────────────────────────────────────────
        //  字段控件（按类型分发）
        //  parentKey 用于嵌套链的状态隔离
        // ─────────────────────────────────────────
        private object DrawEffectField(Type t, string label, object value, string parentKey = "")
        {
            if (t == typeof(int))
                return EditorGUILayout.IntField(label, (int)(value ?? 0));
            if (t == typeof(float))
                return EditorGUILayout.FloatField(label, (float)(value ?? 0f));
            if (t == typeof(bool))
                return EditorGUILayout.Toggle(label, (bool)(value ?? false));
            if (t == typeof(string))
                return EditorGUILayout.TextField(label, (string)(value ?? ""));
            if (t == typeof(Vector3))
                return EditorGUILayout.Vector3Field(label, value is Vector3 v3 ? v3 : Vector3.zero);
            if (t == typeof(LayerMask))
            {
                LayerMask lm = value is LayerMask m ? m : default;
                int newVal = EditorGUILayout.IntField($"{label} (bitmask)", lm.value);
                return new LayerMask { value = newVal };
            }
            if (t.IsEnum)
                return EditorGUILayout.EnumPopup(label, value as Enum ?? (Enum)Activator.CreateInstance(t));
            if (t == typeof(GameObject))
                return EditorGUILayout.ObjectField(label, value as GameObject, typeof(GameObject), false);

            // ── 嵌套效果链 ──────────────────────────────
            if (t == typeof(EffectData[]) ||
                (t.IsArray && typeof(EffectData).IsAssignableFrom(t.GetElementType())))
            {
                string stateKey = $"{parentKey}/{label}";
                if (!_nestedTypeIndices.TryGetValue(stateKey, out int idx)) idx = 0;

                GUILayout.Space(2);
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

                // 缩进区域内绘制嵌套链
                EditorGUI.indentLevel++;
                EffectData[] nestedChain = DrawEffectChainInline(stateKey, (EffectData[])value, ref idx);
                EditorGUI.indentLevel--;

                _nestedTypeIndices[stateKey] = idx;
                GUILayout.Space(2);
                return nestedChain;
            }

            EditorGUILayout.LabelField(label, $"[{t.Name}] {value}");
            return value;
        }

        // ─────────────────────────────────────────
        //  条件列表绘制（Skill / Effect 共用）
        // ─────────────────────────────────────────

        private void DrawConditionList(List<ConditionConfig> conditions, string keyPrefix)
        {
            string foldKey = keyPrefix + "_fold";
            if (!_condFoldStates.TryGetValue(foldKey, out bool foldOpen))
                foldOpen = true;

            EditorGUILayout.BeginHorizontal();
            foldOpen = EditorGUILayout.Foldout(foldOpen, $"条件 ({conditions.Count})", true);
            _condFoldStates[foldKey] = foldOpen;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (!foldOpen) return;

            EditorGUI.indentLevel++;

            // 添加行
            DrawAddConditionRow(conditions, keyPrefix);

            // 条件列表
            int removeCond = -1;
            for (int ci = 0; ci < conditions.Count; ci++)
            {
                if (!DrawConditionRow(conditions[ci]))
                    removeCond = ci;
            }
            if (removeCond >= 0)
            {
                conditions.RemoveAt(removeCond);
                _isDirty = true;
            }

            EditorGUI.indentLevel--;
        }

        private void DrawAddConditionRow(List<ConditionConfig> conditions, string keyPrefix)
        {
            string srcKey = keyPrefix + "src";
            string cmpKey = keyPrefix + "cmp";
            if (!_condAddSrcIdx.ContainsKey(srcKey)) _condAddSrcIdx[srcKey] = 0;
            if (!_condAddCmpIdx.ContainsKey(cmpKey)) _condAddCmpIdx[cmpKey] = 0;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16);

            if (_sourceTypeNames.Length > 0)
                _condAddSrcIdx[srcKey] = EditorGUILayout.Popup(
                    _condAddSrcIdx[srcKey], _sourceTypeNames, GUILayout.Width(130));
            else
                GUILayout.Label("（无 ISource）", EditorStyles.miniLabel, GUILayout.Width(80));

            if (_compareTypeNames.Length > 0)
                _condAddCmpIdx[cmpKey] = EditorGUILayout.Popup(
                    _condAddCmpIdx[cmpKey], _compareTypeNames, GUILayout.Width(100));

            if (GUILayout.Button("＋ 条件", GUILayout.Width(60)))
            {
                string srcType = _sourceTypeNames.Length > 0 ? _sourceTypeNames[_condAddSrcIdx[srcKey]] : "";
                string cmpType = _compareTypeNames.Length > 0 ? _compareTypeNames[_condAddCmpIdx[cmpKey]] : "";
                conditions.Add(new ConditionConfig
                {
                    SourceType    = srcType,
                    CompareType   = cmpType,
                    ConditionType = ConditionType.Necessary,
                });
                _isDirty = true;
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>绘制一行条件。返回 false 表示删除。</summary>
        private bool DrawConditionRow(ConditionConfig cond)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16);

            EditorGUI.BeginChangeCheck();

            cond.ConditionType = (ConditionType)EditorGUILayout.EnumPopup(
                cond.ConditionType, GUILayout.Width(88));

            if (_sourceTypeNames.Length > 0)
            {
                int idx = Mathf.Max(0, Array.IndexOf(_sourceTypeNames, cond.SourceType));
                idx             = EditorGUILayout.Popup(idx, _sourceTypeNames, GUILayout.Width(130));
                cond.SourceType = _sourceTypeNames[idx];
            }
            else
            {
                cond.SourceType = EditorGUILayout.TextField(cond.SourceType, GUILayout.Width(130));
            }

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

            bool needsValue = cond.CompareType is "GreaterThan" or "LessThan" or "Equal";
            if (needsValue)
                cond.CompareValue = EditorGUILayout.FloatField(cond.CompareValue, GUILayout.Width(60));
            else
                GUILayout.Space(64);

            if (EditorGUI.EndChangeCheck()) _isDirty = true;

            GUILayout.FlexibleSpace();
            GUI.color = new Color(1f, 0.5f, 0.5f);
            bool remove = GUILayout.Button("×", GUILayout.Width(24));
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            return !remove;
        }

        // ─────────────────────────────────────────
        //  辅助：区块标题
        // ─────────────────────────────────────────
        private static void DrawSectionHeader(string title)
        {
            GUILayout.Space(6);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            Rect r = GUILayoutUtility.GetLastRect();
            r.y += r.height + 1;
            r.height = 1;
            EditorGUI.DrawRect(r, SectionLine);
            GUILayout.Space(4);
        }

        // ─────────────────────────────────────────
        //  JSON 自定义转换器
        // ─────────────────────────────────────────
        private class LayerMaskConverter : JsonConverter<LayerMask>
        {
            public override LayerMask ReadJson(JsonReader reader, Type objectType,
                LayerMask existingValue, bool hasExistingValue, JsonSerializer serializer)
                => new LayerMask { value = Convert.ToInt32(reader.Value) };

            public override void WriteJson(JsonWriter writer, LayerMask value, JsonSerializer serializer)
                => writer.WriteValue(value.value);
        }

        // GameObject ↔ 资产路径字符串（仅支持项目资产，不支持场景对象）
        private class GameObjectConverter : JsonConverter<GameObject>
        {
            public override GameObject ReadJson(JsonReader reader, Type objectType,
                GameObject existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                string path = reader.Value as string;
                return string.IsNullOrEmpty(path)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            public override void WriteJson(JsonWriter writer, GameObject value, JsonSerializer serializer)
            {
                if (value == null) { writer.WriteNull(); return; }
                writer.WriteValue(AssetDatabase.GetAssetPath(value));
            }
        }

        private class Vector3Converter : JsonConverter<Vector3>
        {
            public override Vector3 ReadJson(JsonReader reader, Type objectType,
                Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                JObject obj = JObject.Load(reader);
                return new Vector3(
                    obj["x"]?.Value<float>() ?? 0f,
                    obj["y"]?.Value<float>() ?? 0f,
                    obj["z"]?.Value<float>() ?? 0f);
            }

            public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("x"); writer.WriteValue(value.x);
                writer.WritePropertyName("y"); writer.WriteValue(value.y);
                writer.WritePropertyName("z"); writer.WriteValue(value.z);
                writer.WriteEndObject();
            }
        }
    }
}
