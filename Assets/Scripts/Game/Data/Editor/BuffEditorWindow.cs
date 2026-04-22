using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;

namespace CrystalMagic.Editor.Data
{
    /// <summary>
    /// Buff 编辑器
    /// 左侧：Buff 列表；右侧：选中 Buff 的完整配置（支持多态子类）
    /// 菜单路径：Tools/Data/Buff Editor
    /// </summary>
    public class BuffEditorWindow : EditorWindow
    {
        private const string DataPath       = "Assets/Res/Data/BuffDataTable.json";
        private const float  ListPanelWidth = 220f;
        private const float  ItemHeight     = 26f;
        private const float  InsertFieldWidth = 30f;
        private const float  LabelWidth     = 180f;

        // ===== Buff 子类注册 =====
        private static readonly Type[]   KnownBuffTypes =
        {
            typeof(PropertyBuffData),
            typeof(EffectBuffData),
        };
        private static readonly string[] KnownBuffNames =
        {
            "属性修饰 (PropertyBuff)",
            "特效 (EffectBuff)",
        };
        private static readonly Color[] BuffColors =
        {
            new(0.14f, 0.50f, 0.24f),  // PropertyBuff — 绿
            new(0.60f, 0.18f, 0.14f),  // EffectBuff   — 红
        };

        // ===== Effect 子类注册（用于 EffectBuff 的 EffectChain）=====
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
        private List<BuffData> _rows = new();
        private bool   _isDirty;
        private string _statusText = "";

        // ===== UI 状态 =====
        private int     _selectedIndex     = -1;
        private int     _addBuffTypeIndex;
        private int     _addEffectTypeIndex;
        private Vector2 _listScrollPos;
        private Vector2 _detailScrollPos;
        private readonly Dictionary<BuffData, string> _insertTexts = new();

        // 每个嵌套效果链的"待添加类型"选中索引，key = 字段路径
        private readonly Dictionary<string, int>  _nestedTypeIndices = new();
        // 每个效果条目的折叠状态，key = 条目路径，true = 展开
        private readonly Dictionary<string, bool> _effectFoldStates  = new();

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
            FloatFormatHandling = FloatFormatHandling.String,
        };

        private class TableWrapper { public List<BuffData> Rows = new(); }

        // ─────────────────────────────────────────
        [MenuItem("Tools/Data/Buff Editor")]
        public static void Open()
        {
            var w = GetWindow<BuffEditorWindow>("Buff Editor");
            w.minSize = new Vector2(920, 560);
            w.Show();
        }

        private void OnEnable() => LoadData();

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
                string json    = UpgradeLegacyJson(File.ReadAllText(DataPath));
                var    wrapper = JsonConvert.DeserializeObject<TableWrapper>(json, JsonSettings);
                if (wrapper?.Rows != null) _rows = wrapper.Rows;
                NormalizeRowIds();
                _insertTexts.Clear();
                _statusText = $"已加载 {_rows.Count} 条  ·  {DataPath}";
            }
            catch (Exception ex)
            {
                _statusText = $"加载失败：{ex.Message}";
                Debug.LogError($"[BuffEditor] Load error:\n{ex}");
            }
        }

        private void SaveData()
        {
            string dir = Path.GetDirectoryName(DataPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            try
            {
                NormalizeRowIds();
                string json = JsonConvert.SerializeObject(new TableWrapper { Rows = _rows }, JsonSettings);
                File.WriteAllText(DataPath, json, Encoding.UTF8);
                AssetDatabase.Refresh();
                _isDirty    = false;
                _statusText = $"已保存 {_rows.Count} 条  ·  {DataPath}";
                Debug.Log($"[BuffEditor] Saved {DataPath}");
            }
            catch (Exception ex)
            {
                _statusText = $"保存失败：{ex.Message}";
                Debug.LogError($"[BuffEditor] Save error:\n{ex}");
            }
        }

        // ─────────────────────────────────────────
        //  新增 / 删除
        // ─────────────────────────────────────────
        private void AddBuff()
        {
            BuffData newBuff = (BuffData)Activator.CreateInstance(KnownBuffTypes[_addBuffTypeIndex]);
            newBuff.Id       = _rows.Count + 1;
            newBuff.Name     = $"新Buff {_rows.Count + 1}";
            newBuff.MaxStacks = 1;

            if (newBuff is EffectBuffData te)
                te.EffectChain = Array.Empty<EffectData>();

            _rows.Add(newBuff);
            NormalizeRowIds();
            _selectedIndex = _rows.Count - 1;
            _isDirty       = true;
            Repaint();
        }

        private void DeleteSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count) return;
            BuffData removedRow = _rows[_selectedIndex];
            _rows.RemoveAt(_selectedIndex);
            _insertTexts.Remove(removedRow);
            NormalizeRowIds();
            _selectedIndex = Mathf.Clamp(_selectedIndex, -1, _rows.Count - 1);
            _isDirty = true;
        }

        private void DuplicateSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count) return;

            BuffData source = _rows[_selectedIndex];
            string json = JsonConvert.SerializeObject(source, JsonSettings);
            BuffData copy = JsonConvert.DeserializeObject<BuffData>(json, JsonSettings);
            if (copy == null) return;

            copy.Id = _rows.Count + 1;
            copy.Name = string.IsNullOrWhiteSpace(source.Name) ? $"鏂癇uff {copy.Id}" : $"{source.Name}_Copy";
            _rows.Add(copy);
            NormalizeRowIds();
            _selectedIndex = _rows.Count - 1;
            _isDirty = true;
            Repaint();
        }

        private static string UpgradeLegacyJson(string json)
        {
            return json.Replace(
                "CrystalMagic.Game.Data.TickEffectBuffData, Assembly-CSharp",
                "CrystalMagic.Game.Data.EffectBuffData, Assembly-CSharp");
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

            BuffData row = _rows[fromIndex];
            _rows.RemoveAt(fromIndex);

            insertIndex = Mathf.Clamp(insertIndex, 0, _rows.Count);
            _rows.Insert(insertIndex, row);
            NormalizeRowIds();
            _selectedIndex = insertIndex;
            _isDirty = true;
            GUI.FocusControl(null);
            Repaint();
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

            // 新增：先选子类类型，再点加号
            _addBuffTypeIndex = EditorGUILayout.Popup(_addBuffTypeIndex, KnownBuffNames,
                EditorStyles.toolbarPopup, GUILayout.Width(180));
            if (GUILayout.Button("＋ 新增", EditorStyles.toolbarButton, GUILayout.Width(52)))
                AddBuff();

            GUI.enabled = _selectedIndex >= 0;
            if (GUILayout.Button("复制当前", EditorStyles.toolbarButton, GUILayout.Width(64)))
                DuplicateSelected();

            GUI.enabled = _selectedIndex >= 0;
            GUI.color   = _selectedIndex >= 0 ? new Color(1f, 0.55f, 0.55f) : Color.white;
            if (GUILayout.Button("删除", EditorStyles.toolbarButton, GUILayout.Width(44)))
                if (EditorUtility.DisplayDialog("删除 Buff",
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
            GUILayout.Label($"Buff 列表 ({_rows.Count})", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _listScrollPos = EditorGUILayout.BeginScrollView(_listScrollPos, GUILayout.ExpandHeight(true));
            Event evt = Event.current;
            BuffData moveRow = null;
            int moveToIndex = -1;

            for (int i = 0; i < _rows.Count; i++)
            {
                BuffData buff       = _rows[i];
                bool     isSelected = i == _selectedIndex;

                Rect itemRect = GUILayoutUtility.GetRect(ListPanelWidth, ItemHeight, GUILayout.ExpandWidth(true));

                Color bg = isSelected ? SelectedColor
                    : (itemRect.Contains(evt.mousePosition) ? HoverColor
                        : (i % 2 == 0 ? EvenRowColor : OddRowColor));
                EditorGUI.DrawRect(itemRect, bg);

                Rect insertRect = new Rect(itemRect.x + 6f, itemRect.y + 3f, InsertFieldWidth, itemRect.height - 6f);
                string insertText = _insertTexts.TryGetValue(buff, out string currentInsertText) ? currentInsertText : string.Empty;
                string controlName = $"insert_{buff.GetHashCode()}";
                GUI.SetNextControlName(controlName);
                string newInsertText = EditorGUI.TextField(insertRect, insertText);
                if (newInsertText != insertText)
                {
                    if (string.IsNullOrWhiteSpace(newInsertText))
                        _insertTexts.Remove(buff);
                    else
                        _insertTexts[buff] = newInsertText;
                }

                bool isFocused = GUI.GetNameOfFocusedControl() == controlName;
                bool submitByEnter = isFocused && evt.type == EventType.KeyDown &&
                    (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter);
                bool submitByBlur = !isFocused && !string.IsNullOrWhiteSpace(newInsertText) && newInsertText == insertText;
                if ((submitByEnter || submitByBlur) && int.TryParse(newInsertText, out int insertTo))
                {
                    moveRow = buff;
                    moveToIndex = Mathf.Clamp(insertTo - 1, 0, _rows.Count - 1);
                    _insertTexts.Remove(buff);
                    if (submitByEnter)
                    {
                        evt.Use();
                        GUI.FocusControl(null);
                    }
                }

                // 左侧小色块表示 Buff 子类
                int    typeIdx  = Array.IndexOf(KnownBuffTypes, buff.GetType());
                Color  typeColor = typeIdx >= 0 ? BuffColors[typeIdx] : Color.gray;
                EditorGUI.DrawRect(new Rect(insertRect.xMax + 4f, itemRect.y, 4f, itemRect.height), typeColor);

                string label = $"[{buff.Id}]  {(string.IsNullOrEmpty(buff.Name) ? "（未命名）" : buff.Name)}";
                GUI.Label(
                    new Rect(insertRect.xMax + 14f, itemRect.y + 4, itemRect.width - insertRect.width - 14f, itemRect.height - 4),
                    label,
                    isSelected ? EditorStyles.whiteLabel : EditorStyles.label);

                if (evt.type == EventType.MouseDown && itemRect.Contains(evt.mousePosition) && !insertRect.Contains(evt.mousePosition))
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
            if (moveRow != null)
                MoveRowToInsertIndex(_rows.IndexOf(moveRow), moveToIndex);
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
                GUILayout.Label("← 从左侧选择一个 Buff", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            BuffData buff    = _rows[_selectedIndex];
            int      typeIdx = Array.IndexOf(KnownBuffTypes, buff.GetType());
            string   typeName = typeIdx >= 0 ? KnownBuffNames[typeIdx] : buff.GetType().Name;

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (typeIdx >= 0)
            {
                Color prev = GUI.color;
                GUI.color = BuffColors[typeIdx];
                GUILayout.Label("■", GUILayout.Width(14));
                GUI.color = prev;
            }
            GUILayout.Label($"[{buff.Id}]  {buff.Name}  —  {typeName}", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _detailScrollPos = EditorGUILayout.BeginScrollView(_detailScrollPos);

            float prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = LabelWidth;

            EditorGUI.BeginChangeCheck();

            // ── 基础信息 ──────────────────────────────────
            DrawSectionHeader("基础信息");
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.IntField("Id", buff.Id);
            buff.Name      = EditorGUILayout.TextField("名称", buff.Name ?? "");
            buff.CanStack  = EditorGUILayout.Toggle("可叠层", buff.CanStack);
            using (new EditorGUI.DisabledScope(!buff.CanStack))
                buff.MaxStacks = EditorGUILayout.IntField("最大叠层数", buff.MaxStacks);

            if (EditorGUI.EndChangeCheck())
                _isDirty = true;

            DrawSkillModifierFields(buff);

            // ── 子类字段 ──────────────────────────────────
            if (buff is PropertyBuffData propBuff)
                DrawPropertyBuffFields(propBuff);
            else if (buff is EffectBuffData effectBuff)
                DrawEffectBuffFields(effectBuff);

            EditorGUIUtility.labelWidth = prevLabelWidth;
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // ─────────────────────────────────────────
        //  PropertyBuffData 字段
        // ─────────────────────────────────────────
        private void DrawPropertyBuffFields(PropertyBuffData buff)
        {
            EditorGUI.BeginChangeCheck();

            DrawSectionHeader("Move（移动）");
            DrawAttributeRow("MoveSpeed",  ref buff.MoveSpeedFactor,   ref buff.MoveSpeedBonus);

            DrawSectionHeader("Vitality（生存）");
            DrawAttributeRow("MaxHealth",  ref buff.MaxHealthFactor,   ref buff.MaxHealthBonus);
            DrawAttributeRow("Defense",    ref buff.DefenseFactor,     ref buff.DefenseBonus);

            DrawSectionHeader("Attack（攻击）");
            DrawAttributeRow("Attack",     ref buff.AttackPowerFactor, ref buff.AttackPowerBonus);
            DrawAttributeRow("SkillRange", ref buff.SkillRangeFactor,  ref buff.SkillRangeBonus);

            DrawSectionHeader("Mana（法力）");
            DrawAttributeRow("MaxMp",      ref buff.MaxMpFactor,       ref buff.MaxMpBonus);

            if (EditorGUI.EndChangeCheck())
                _isDirty = true;
        }

        private static void DrawAttributeRow(string attrName, ref float multiply, ref float add)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(attrName);
            float prevWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 52f;
            multiply = EditorGUILayout.FloatField("×倍率", multiply, GUILayout.ExpandWidth(true));
            add      = EditorGUILayout.FloatField("+加值", add,      GUILayout.ExpandWidth(true));
            EditorGUIUtility.labelWidth = prevWidth;
            EditorGUILayout.EndHorizontal();
        }

        // ─────────────────────────────────────────
        //  EffectBuffData 字段
        // ─────────────────────────────────────────
        private void DrawSkillModifierFields(BuffData buff)
        {
            DrawSectionHeader("技能修正");
            buff.SkillModifiers ??= new List<SkillModifierEntry>();

            int removeAt = -1;
            for (int i = 0; i < buff.SkillModifiers.Count; i++)
            {
                SkillModifierEntry entry = buff.SkillModifiers[i];

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginHorizontal();
                entry.Channel = (SkillModifierChannel)EditorGUILayout.EnumPopup(entry.Channel, GUILayout.MinWidth(180));

                float prevWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 46f;
                entry.Factor = EditorGUILayout.FloatField("倍率", entry.Factor, GUILayout.MinWidth(90));
                entry.Bonus = EditorGUILayout.FloatField("加值", entry.Bonus, GUILayout.MinWidth(90));
                EditorGUIUtility.labelWidth = prevWidth;

                if (GUILayout.Button("删除", GUILayout.Width(44)))
                    removeAt = i;

                EditorGUILayout.EndHorizontal();

                if (EditorGUI.EndChangeCheck())
                {
                    buff.SkillModifiers[i] = entry;
                    _isDirty = true;
                }
            }

            if (GUILayout.Button("+ 添加技能修正", GUILayout.Width(120)))
            {
                buff.SkillModifiers.Add(new SkillModifierEntry());
                _isDirty = true;
            }

            if (removeAt >= 0)
            {
                buff.SkillModifiers.RemoveAt(removeAt);
                _isDirty = true;
            }
        }

        private void DrawEffectBuffFields(EffectBuffData buff)
        {
            DrawSectionHeader("效果链");
            buff.EffectChain ??= Array.Empty<EffectData>();
            buff.EffectChain = DrawEffectChainInline("__buff_root__", buff.EffectChain, ref _addEffectTypeIndex);
        }

        // ─────────────────────────────────────────
        //  效果链（复用 SkillEditorWindow 同款逻辑）
        // ─────────────────────────────────────────
        private EffectData[] DrawEffectChainInline(string stateKey, EffectData[] chain, ref int typeIndex)
        {
            chain ??= Array.Empty<EffectData>();

            EditorGUILayout.BeginHorizontal();
            typeIndex = EditorGUILayout.Popup(typeIndex, KnownEffectNames, GUILayout.Width(190));
            if (GUILayout.Button("＋ 添加", GUILayout.Width(66)))
            {
                var newEffect = (EffectData)Activator.CreateInstance(KnownEffectTypes[typeIndex]);
                var list      = new List<EffectData>(chain) { newEffect };
                chain         = list.ToArray();
                _isDirty      = true;
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

        private bool DrawEffectEntry(EffectData[] chain, int index, string parentKey = "")
        {
            EffectData effect = chain[index];
            if (effect == null) return false;

            Type   effectType  = effect.GetType();
            int    typeIdx     = Array.IndexOf(KnownEffectTypes, effectType);
            Color  headerColor = typeIdx >= 0 ? EffectColors[typeIdx] : new Color(0.4f, 0.4f, 0.4f);
            string typeName    = typeIdx >= 0 ? KnownEffectNames[typeIdx] : effectType.Name;

            string entryKey = $"{parentKey}/{effectType.Name}[{index}]";
            if (!_effectFoldStates.TryGetValue(entryKey, out bool expanded))
                expanded = true;

            Rect  headerRect   = EditorGUILayout.GetControlRect(false, 22f);
            float indentOffset = EditorGUI.indentLevel * 15f;
            headerRect.x     += indentOffset;
            headerRect.width -= indentOffset;
            EditorGUI.DrawRect(headerRect, headerColor);

            float bx = headerRect.xMax - 106;

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

            GUI.Label(
                new Rect(headerRect.x + 6, headerRect.y + 3, bx - headerRect.x - 10, 18),
                $"#{index + 1}  {typeName}",
                EditorStyles.whiteLabel);

            if (expanded)
            {
                EditorGUI.indentLevel++;
                GUILayout.Space(2);
                foreach (FieldInfo field in effectType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    EditorGUI.BeginChangeCheck();
                    object oldVal = field.GetValue(effect);
                    object newVal = DrawField(field.FieldType, field.Name, oldVal, entryKey);
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
        // ─────────────────────────────────────────
        private object DrawField(Type t, string label, object value, string parentKey = "")
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
                LayerMask lm  = value is LayerMask m ? m : default;
                int       newVal = EditorGUILayout.IntField($"{label} (bitmask)", lm.value);
                return new LayerMask { value = newVal };
            }
            if (t.IsEnum)
                return EditorGUILayout.EnumPopup(label, value as Enum ?? (Enum)Activator.CreateInstance(t));

            // 嵌套效果链
            if (t == typeof(EffectData[]) ||
                (t.IsArray && typeof(EffectData).IsAssignableFrom(t.GetElementType())))
            {
                string stateKey = $"{parentKey}/{label}";
                if (!_nestedTypeIndices.TryGetValue(stateKey, out int idx)) idx = 0;

                GUILayout.Space(2);
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EffectData[] nested = DrawEffectChainInline(stateKey, (EffectData[])value, ref idx);
                EditorGUI.indentLevel--;
                _nestedTypeIndices[stateKey] = idx;
                GUILayout.Space(2);
                return nested;
            }

            EditorGUILayout.LabelField(label, $"[{t.Name}] {value}");
            return value;
        }

        // ─────────────────────────────────────────
        //  区块标题
        // ─────────────────────────────────────────
        private static void DrawSectionHeader(string title)
        {
            GUILayout.Space(6);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            Rect r = GUILayoutUtility.GetLastRect();
            r.y     += r.height + 1;
            r.height =  1;
            EditorGUI.DrawRect(r, SectionLine);
            GUILayout.Space(4);
        }
    }
}
