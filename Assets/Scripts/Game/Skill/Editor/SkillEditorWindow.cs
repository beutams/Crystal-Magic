using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Skill
{
    /// <summary>
    /// Skill editor.
    /// Left: skill list.
    /// Right: selected skill detail, including conditions and effect chain.
    /// </summary>
    public class SkillEditorWindow : EditorWindow
    {
        private const string DataPath = "Assets/Res/Data/SkillDataTable.json";
        private const float ListPanelWidth = 220f;
        private const float ItemHeight = 26f;
        private const float InsertFieldWidth = 30f;
        private const float LabelWidth = 150f;
        private static readonly string[] SkillModifierChannelDisplayNames = EditorLabelUtility.GetEnumDisplayNames<SkillModifierChannel>();

        private static readonly Type[] KnownEffectTypes =
        {
            typeof(ApplyBuffEffectData),
            typeof(AreaSearchEffectData),
            typeof(ChainSearchEffectData),
            typeof(DamageEffectData),
            typeof(ForwardRectSearchEffectData),
            typeof(HealEffectData),
            typeof(HealthCostEffectData),
            typeof(FearEffectData),
            typeof(KnockbackEffectData),
            typeof(PersistentEffectData),
            typeof(ReadBuffStackEffectData),
            typeof(RemoveBuffEffectData),
            typeof(RestoreManaEffectData),
            typeof(SpawnProjectileEffectData),
            typeof(SpawnSoundEffectData),
            typeof(SpawnVfxEffectData),
            typeof(StunEffectData),
            typeof(CameraShakeEffectData),
        };

        private static readonly string[] KnownEffectNames =
        {
            "Apply Buff",
            "Area Search",
            "Chain Search",
            "Damage",
            "Forward Rect Search",
            "Heal",
            "Health Cost",
            "Fear",
            "Knockback",
            "Persistent",
            "Read Buff Stack",
            "Remove Buff",
            "Restore Mana",
            "Spawn Projectile",
            "Spawn Sound",
            "Spawn VFX",
            "Stun",
            "Camera Shake",
        };

        private static readonly Color[] EffectColors =
        {
            new(0.34f, 0.22f, 0.56f),
            new(0.14f, 0.38f, 0.60f),
            new(0.18f, 0.42f, 0.74f),
            new(0.60f, 0.18f, 0.14f),
            new(0.60f, 0.30f, 0.12f),
            new(0.16f, 0.52f, 0.22f),
            new(0.42f, 0.16f, 0.16f),
            new(0.42f, 0.24f, 0.12f),
            new(0.55f, 0.33f, 0.14f),
            new(0.14f, 0.50f, 0.24f),
            new(0.22f, 0.42f, 0.64f),
            new(0.50f, 0.18f, 0.18f),
            new(0.14f, 0.46f, 0.60f),
            new(0.55f, 0.38f, 0.10f),
            new(0.38f, 0.18f, 0.55f),
            new(0.18f, 0.48f, 0.48f),
            new(0.32f, 0.32f, 0.32f),
            new(0.58f, 0.42f, 0.12f),
        };

        private List<SkillData> _rows = new();
        private bool _isDirty;
        private string _statusText = string.Empty;

        private int _selectedIndex = -1;
        private int _addEffectTypeIndex;
        private Vector2 _listScrollPos;
        private Vector2 _detailScrollPos;
        private readonly Dictionary<SkillData, string> _insertTexts = new();

        private readonly Dictionary<string, int> _nestedTypeIndices = new();
        private readonly Dictionary<string, bool> _effectFoldStates = new();
        private readonly Dictionary<string, bool> _conditionFoldStates = new();
        private readonly Dictionary<string, bool> _conditionAddSectionStates = new();
        private readonly Dictionary<string, int> _conditionAddSourceIndices = new();
        private readonly Dictionary<string, int> _conditionAddCompareIndices = new();

        private string[] _sourceTypeNames = Array.Empty<string>();
        private string[] _sourceTypeDisplayNames = Array.Empty<string>();
        private string[] _compareTypeNames = Array.Empty<string>();
        private string[] _compareTypeDisplayNames = Array.Empty<string>();

        private static readonly Color SelectedColor = new(0.27f, 0.52f, 0.85f, 0.85f);
        private static readonly Color EvenRowColor = new(0.22f, 0.22f, 0.22f, 1f);
        private static readonly Color OddRowColor = new(0.25f, 0.25f, 0.25f, 1f);
        private static readonly Color HoverColor = new(0.32f, 0.32f, 0.32f, 1f);
        private static readonly Color SectionLine = new(0.45f, 0.45f, 0.45f, 1f);
        private static readonly Color DividerColor = new(0.15f, 0.15f, 0.15f, 1f);
        private static readonly Color ConditionAddHeaderColor = new(0.15f, 0.15f, 0.15f, 1f);
        private static readonly Color ConditionAddBodyColor = new(0.18f, 0.18f, 0.18f, 1f);
        private static readonly Type[] FollowupConsumeRuleTypes =
        {
            typeof(UseCountSkillFollowupConsumeRuleData),
        };

        private static readonly string[] FollowupConsumeRuleNames =
        {
            "Use Count",
        };
        private static readonly Type[] FollowupModifierRuleTypes =
        {
            typeof(StaticSkillFollowupModifierRuleData),
            typeof(SequenceSkillFollowupModifierRuleData),
        };

        private static readonly string[] FollowupModifierRuleNames =
        {
            "Static",
            "Sequence",
        };

        private static JsonSerializerSettings JsonSettings => new()
        {
            TypeNameHandling = TypeNameHandling.Auto,
            Formatting = Formatting.Indented,
            FloatFormatHandling = FloatFormatHandling.String,
            Converters = { new LayerMaskConverter(), new Vector3Converter(), new UnityObjectConverter() },
        };

        private class TableWrapper
        {
            public List<SkillData> Rows = new();
        }

        [MenuItem("Tools/Data/Skill Editor")]
        public static void Open()
        {
            SkillEditorWindow window = GetWindow<SkillEditorWindow>("Skill Editor");
            window.minSize = new Vector2(920f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadData();
            RefreshTypeArrays();
        }

        private void RefreshTypeArrays()
        {
            EditorTypeDisplayEntry[] sourceEntries = EditorLabelUtility.CollectTypeEntries(typeof(ISource));
            _sourceTypeNames = sourceEntries.Select(entry => entry.Key).ToArray();
            _sourceTypeDisplayNames = sourceEntries.Select(entry => entry.DisplayName).ToArray();

            EditorTypeDisplayEntry[] compareEntries = EditorLabelUtility.CollectTypeEntries(typeof(ICompareType));
            _compareTypeNames = compareEntries.Select(entry => entry.Key).ToArray();
            _compareTypeDisplayNames = compareEntries.Select(entry => entry.DisplayName).ToArray();
        }

        private void LoadData()
        {
            _rows.Clear();
            _selectedIndex = -1;
            _isDirty = false;

            if (!File.Exists(DataPath))
            {
                _statusText = $"Missing file: {DataPath}";
                return;
            }

            try
            {
                string json = File.ReadAllText(DataPath);
                TableWrapper wrapper = JsonConvert.DeserializeObject<TableWrapper>(json, JsonSettings);
                if (wrapper?.Rows != null)
                    _rows = wrapper.Rows;

                NormalizeRowIds();
                _insertTexts.Clear();
                _statusText = $"Loaded {_rows.Count} rows";
            }
            catch (Exception ex)
            {
                _statusText = $"Load failed: {ex.Message}";
                Debug.LogError($"[SkillEditor] Load error:\n{ex}");
            }
        }

        private void SaveData()
        {
            string directory = Path.GetDirectoryName(DataPath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            try
            {
                NormalizeRowIds();
                string json = JsonConvert.SerializeObject(new TableWrapper { Rows = _rows }, JsonSettings);
                File.WriteAllText(DataPath, json, Encoding.UTF8);
                AssetDatabase.Refresh();
                _isDirty = false;
                _statusText = $"Saved {_rows.Count} rows";
            }
            catch (Exception ex)
            {
                _statusText = $"Save failed: {ex.Message}";
                Debug.LogError($"[SkillEditor] Save error:\n{ex}");
            }
        }

        private void AddSkill()
        {
            _rows.Add(new SkillData
            {
                Id = _rows.Count,
                Name = $"New Skill {_rows.Count}",
                Description = string.Empty,
                IconPath = string.Empty,
                MoveSpeedMultiplier = 1f,
                Conditions = new List<ConditionConfig>(),
                EffectChain = Array.Empty<EffectData>(),
                FollowupEffects = new List<SkillFollowupEffectData>(),
            });

            NormalizeRowIds();
            _selectedIndex = _rows.Count - 1;
            _isDirty = true;
            Repaint();
        }

        private void DeleteSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
                return;

            SkillData removedRow = _rows[_selectedIndex];
            _rows.RemoveAt(_selectedIndex);
            _insertTexts.Remove(removedRow);
            NormalizeRowIds();
            _selectedIndex = Mathf.Clamp(_selectedIndex, -1, _rows.Count - 1);
            _isDirty = true;
        }

        private void DuplicateSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
                return;

            SkillData source = _rows[_selectedIndex];
            string json = JsonConvert.SerializeObject(source, JsonSettings);
            SkillData copy = JsonConvert.DeserializeObject<SkillData>(json, JsonSettings);
            if (copy == null)
                return;

            copy.Id = _rows.Count;
            copy.Name = string.IsNullOrWhiteSpace(source.Name) ? $"Skill {copy.Id}" : $"{source.Name}_Copy";
            copy.Conditions ??= new List<ConditionConfig>();
            copy.EffectChain ??= Array.Empty<EffectData>();
            copy.FollowupEffects ??= new List<SkillFollowupEffectData>();
            _rows.Add(copy);
            NormalizeRowIds();
            _selectedIndex = _rows.Count - 1;
            _isDirty = true;
            Repaint();
        }

        private void NormalizeRowIds()
        {
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].Id = i;
        }

        private void MoveRowToInsertIndex(int fromIndex, int insertIndex)
        {
            if (fromIndex < 0 || fromIndex >= _rows.Count)
                return;

            insertIndex = Mathf.Clamp(insertIndex, 0, _rows.Count - 1);
            if (fromIndex == insertIndex)
                return;

            SkillData row = _rows[fromIndex];
            _rows.RemoveAt(fromIndex);
            insertIndex = Mathf.Clamp(insertIndex, 0, _rows.Count);
            _rows.Insert(insertIndex, row);
            NormalizeRowIds();
            _selectedIndex = insertIndex;
            _isDirty = true;
            CrystalMagic.Editor.EditorFocusUtility.ClearTextFocus();
            Repaint();
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
            if (GUILayout.Button(_isDirty ? "Save *" : "Save", EditorStyles.toolbarButton, GUILayout.Width(52f)))
                SaveData();
            GUI.enabled = true;

            if (GUILayout.Button("+ Add", EditorStyles.toolbarButton, GUILayout.Width(52f)))
                AddSkill();

            GUI.enabled = _selectedIndex >= 0;
            if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                DuplicateSelected();

            GUI.color = _selectedIndex >= 0 ? new Color(1f, 0.55f, 0.55f) : Color.white;
            if (GUILayout.Button("Delete", EditorStyles.toolbarButton, GUILayout.Width(52f)))
            {
                if (EditorUtility.DisplayDialog(
                        "Delete Skill",
                        $"Delete \"{_rows[_selectedIndex].Name}\"?",
                        "Delete",
                        "Cancel"))
                {
                    DeleteSelected();
                }
            }

            GUI.color = Color.white;
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
            GUILayout.Label($"Skill List ({_rows.Count})", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _listScrollPos = EditorGUILayout.BeginScrollView(_listScrollPos, GUILayout.ExpandHeight(true));
            Event currentEvent = Event.current;
            SkillData moveRow = null;
            int moveToIndex = -1;

            for (int i = 0; i < _rows.Count; i++)
            {
                SkillData skill = _rows[i];
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

                Rect insertRect = new Rect(itemRect.x + 6f, itemRect.y + 3f, InsertFieldWidth, itemRect.height - 6f);
                string insertText = _insertTexts.TryGetValue(skill, out string currentInsertText)
                    ? currentInsertText
                    : string.Empty;
                string controlName = $"insert_{skill.GetHashCode()}";
                GUI.SetNextControlName(controlName);
                string newInsertText = EditorGUI.TextField(insertRect, insertText);
                if (newInsertText != insertText)
                {
                    if (string.IsNullOrWhiteSpace(newInsertText))
                        _insertTexts.Remove(skill);
                    else
                        _insertTexts[skill] = newInsertText;
                }

                bool isFocused = GUI.GetNameOfFocusedControl() == controlName;
                bool submitByEnter = isFocused
                    && currentEvent.type == EventType.KeyDown
                    && (currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter);
                bool submitByBlur = !isFocused && !string.IsNullOrWhiteSpace(newInsertText) && newInsertText == insertText;
                if ((submitByEnter || submitByBlur) && int.TryParse(newInsertText, out int insertTo))
                {
                    moveRow = skill;
                    moveToIndex = Mathf.Clamp(insertTo - 1, 0, _rows.Count - 1);
                    _insertTexts.Remove(skill);
                    if (submitByEnter)
                    {
                        currentEvent.Use();
                        CrystalMagic.Editor.EditorFocusUtility.ClearTextFocus();
                    }
                }

                string label = $"[{skill.Id}]  {(string.IsNullOrEmpty(skill.Name) ? "(Unnamed)" : skill.Name)}";
                GUI.Label(
                    new Rect(insertRect.xMax + 8f, itemRect.y + 4f, itemRect.width - insertRect.width - 8f, itemRect.height - 4f),
                    label,
                    isSelected ? EditorStyles.whiteLabel : EditorStyles.label);

                if (currentEvent.type == EventType.MouseDown
                    && itemRect.Contains(currentEvent.mousePosition)
                    && !insertRect.Contains(currentEvent.mousePosition))
                {
                    if (_selectedIndex != i)
                        CrystalMagic.Editor.EditorFocusUtility.ClearTextFocus();
                    _selectedIndex = i;
                    currentEvent.Use();
                    Repaint();
                }

                if (currentEvent.type == EventType.MouseMove)
                    Repaint();
            }

            EditorGUILayout.EndScrollView();

            if (moveRow != null)
                MoveRowToInsertIndex(_rows.IndexOf(moveRow), moveToIndex);

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
                GUILayout.Label("Select a skill from the left list.", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            SkillData skill = _rows[_selectedIndex];

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"[{skill.Id}]  {skill.Name}", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _detailScrollPos = EditorGUILayout.BeginScrollView(_detailScrollPos);
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = LabelWidth;

            EditorGUI.BeginChangeCheck();

            DrawSectionHeader("Basic");
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.IntField("Id", skill.Id);

            skill.Name = EditorGUILayout.TextField("Name", skill.Name ?? string.Empty);
            EditorGUILayout.LabelField("Description");
            skill.Description = EditorGUILayout.TextArea(skill.Description ?? string.Empty, GUILayout.MinHeight(48f), GUILayout.MaxHeight(80f));
            skill.SkillType = (SkillType)EditorGUILayout.EnumPopup("Skill Type", skill.SkillType);
            skill.IconPath = EditorGUILayout.TextField("Icon Path", skill.IconPath ?? string.Empty);

            DrawSectionHeader("Cast");
            skill.MpCost = EditorGUILayout.IntField("MP Cost", skill.MpCost);
            skill.WindupDuration = EditorGUILayout.FloatField("Windup (s)", skill.WindupDuration);
            skill.ChantDuration = EditorGUILayout.FloatField("Chant (s)", skill.ChantDuration);
            skill.RecoveryDuration = EditorGUILayout.FloatField("Recovery (s)", skill.RecoveryDuration);
            skill.CanMoveWhileCasting = EditorGUILayout.Toggle("Can Move While Casting", skill.CanMoveWhileCasting);
            using (new EditorGUI.DisabledScope(!skill.CanMoveWhileCasting))
                skill.MoveSpeedMultiplier = EditorGUILayout.Slider("Move Speed Multiplier", skill.MoveSpeedMultiplier, 0f, 1f);

            if (EditorGUI.EndChangeCheck())
                _isDirty = true;

            DrawSectionHeader("Conditions");
            skill.Conditions ??= new List<ConditionConfig>();
            DrawConditionList(skill.Conditions, "__skill_conditions__");

            DrawSectionHeader("Effect Chain");
            DrawEffectChain(skill);

            DrawSectionHeader("Followup Effects");
            skill.FollowupEffects ??= new List<SkillFollowupEffectData>();
            DrawFollowupEffectList(skill.FollowupEffects);

            EditorGUIUtility.labelWidth = previousLabelWidth;
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawEffectChain(SkillData skill)
        {
            skill.EffectChain ??= Array.Empty<EffectData>();
            skill.EffectChain = DrawEffectChainInline("__root__", skill.EffectChain, ref _addEffectTypeIndex);
        }

        private EffectData[] DrawEffectChainInline(string stateKey, EffectData[] chain, ref int typeIndex)
        {
            chain ??= Array.Empty<EffectData>();

            EditorGUILayout.BeginHorizontal();
            typeIndex = EditorGUILayout.Popup(typeIndex, KnownEffectNames, GUILayout.Width(190f));
            if (GUILayout.Button("+ Add", GUILayout.Width(66f)))
            {
                EffectData newEffect = (EffectData)Activator.CreateInstance(KnownEffectTypes[typeIndex]);
                List<EffectData> list = new(chain) { newEffect };
                chain = list.ToArray();
                _isDirty = true;
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2f);

            if (chain.Length == 0)
            {
                EditorGUILayout.LabelField("(Empty)", EditorStyles.centeredGreyMiniLabel);
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
                List<EffectData> list = new(chain);
                list.RemoveAt(deleteAt);
                chain = list.ToArray();
                _isDirty = true;
            }

            return chain;
        }

        private bool DrawEffectEntry(EffectData[] chain, int index, string parentKey = "")
        {
            EffectData effect = chain[index];
            if (effect == null)
                return false;

            Type effectType = effect.GetType();
            int typeIndex = Array.IndexOf(KnownEffectTypes, effectType);
            Color headerColor = typeIndex >= 0 ? EffectColors[typeIndex] : new Color(0.4f, 0.4f, 0.4f);
            string typeName = typeIndex >= 0 ? KnownEffectNames[typeIndex] : effectType.Name;

            string entryKey = $"{parentKey}/{effectType.Name}[{index}]";
            if (!_effectFoldStates.TryGetValue(entryKey, out bool expanded))
                expanded = true;

            Rect headerRect = EditorGUILayout.GetControlRect(false, 22f);
            float indentOffset = EditorGUI.indentLevel * 15f;
            headerRect.x += indentOffset;
            headerRect.width -= indentOffset;
            EditorGUI.DrawRect(headerRect, headerColor);

            float buttonX = headerRect.xMax - 106f;

            if (GUI.Button(new Rect(buttonX, headerRect.y + 2f, 22f, 18f), expanded ? "▼" : "▶", EditorStyles.miniButton))
            {
                _effectFoldStates[entryKey] = !expanded;
                expanded = !expanded;
                Repaint();
            }

            GUI.enabled = index > 0;
            if (GUI.Button(new Rect(buttonX + 26f, headerRect.y + 2f, 22f, 18f), "↑", EditorStyles.miniButton))
            {
                (chain[index - 1], chain[index]) = (chain[index], chain[index - 1]);
                _isDirty = true;
                Repaint();
            }

            GUI.enabled = index < chain.Length - 1;
            if (GUI.Button(new Rect(buttonX + 50f, headerRect.y + 2f, 22f, 18f), "↓", EditorStyles.miniButton))
            {
                (chain[index], chain[index + 1]) = (chain[index + 1], chain[index]);
                _isDirty = true;
                Repaint();
            }

            GUI.enabled = true;

            GUI.color = new Color(1f, 0.5f, 0.5f);
            bool deleted = GUI.Button(new Rect(buttonX + 78f, headerRect.y + 2f, 24f, 18f), "×", EditorStyles.miniButton);
            GUI.color = Color.white;

            GUI.Label(
                new Rect(headerRect.x + 6f, headerRect.y + 3f, buttonX - headerRect.x - 10f, 18f),
                $"#{index + 1}  {typeName}",
                EditorStyles.whiteLabel);

            if (expanded)
            {
                EditorGUI.indentLevel++;
                GUILayout.Space(2f);

                effect.Conditions ??= new List<ConditionConfig>();
                DrawConditionList(effect.Conditions, $"{entryKey}_conditions");
                GUILayout.Space(4f);

                foreach (FieldInfo field in effectType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    bool disableField =
                        effect is SpawnVfxEffectData spawnVfxEffect &&
                        field.Name == nameof(SpawnVfxEffectData.Duration) &&
                        !spawnVfxEffect.Loop;

                    EditorGUI.BeginChangeCheck();
                    object oldValue = field.GetValue(effect);
                    object newValue;
                    using (new EditorGUI.DisabledScope(disableField))
                    {
                        newValue = DrawEffectField(field.FieldType, EditorLabelUtility.GetLabel(field), oldValue, entryKey);
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        field.SetValue(effect, newValue);
                        _isDirty = true;
                    }
                }

                GUILayout.Space(6f);
                EditorGUI.indentLevel--;
            }

            return deleted;
        }

        private object DrawEffectField(Type fieldType, string label, object value, string parentKey = "")
        {
            if (fieldType == typeof(int))
                return EditorGUILayout.IntField(label, (int)(value ?? 0));
            if (fieldType == typeof(float))
                return EditorGUILayout.FloatField(label, (float)(value ?? 0f));
            if (fieldType == typeof(bool))
                return EditorGUILayout.Toggle(label, (bool)(value ?? false));
            if (fieldType == typeof(string))
                return EditorGUILayout.TextField(label, (string)(value ?? string.Empty));
            if (fieldType == typeof(Vector3))
                return EditorGUILayout.Vector3Field(label, value is Vector3 vector3 ? vector3 : Vector3.zero);
            if (fieldType == typeof(LayerMask))
            {
                LayerMask layerMask = value is LayerMask mask ? mask : default;
                int newValue = EditorGUILayout.IntField($"{label} (bitmask)", layerMask.value);
                return new LayerMask { value = newValue };
            }

            if (fieldType.IsEnum)
                return EditorGUILayout.EnumPopup(label, value as Enum ?? (Enum)Activator.CreateInstance(fieldType));

            if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
                return EditorGUILayout.ObjectField(label, value as UnityEngine.Object, fieldType, false);

            if (fieldType == typeof(List<ConditionConfig>))
            {
                List<ConditionConfig> conditions = value as List<ConditionConfig> ?? new List<ConditionConfig>();
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                DrawConditionList(conditions, $"{parentKey}/{label}");
                EditorGUI.indentLevel--;
                return conditions;
            }

            if (fieldType == typeof(List<SkillModifierEntry>))
            {
                List<SkillModifierEntry> modifiers = value as List<SkillModifierEntry> ?? new List<SkillModifierEntry>();
                DrawSkillModifierList(label, modifiers);
                return modifiers;
            }

            if (fieldType == typeof(EffectData[]) || (fieldType.IsArray && typeof(EffectData).IsAssignableFrom(fieldType.GetElementType())))
            {
                string stateKey = $"{parentKey}/{label}";
                if (!_nestedTypeIndices.TryGetValue(stateKey, out int typeIndex))
                    typeIndex = 0;

                GUILayout.Space(2f);
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EffectData[] nestedChain = DrawEffectChainInline(stateKey, (EffectData[])value, ref typeIndex);
                EditorGUI.indentLevel--;
                _nestedTypeIndices[stateKey] = typeIndex;
                GUILayout.Space(2f);
                return nestedChain;
            }

            EditorGUILayout.LabelField(label, $"[{fieldType.Name}] {value}");
            return value;
        }

        private void DrawSkillModifierList(string label, List<SkillModifierEntry> modifiers)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            int removeAt = -1;
            for (int i = 0; i < modifiers.Count; i++)
            {
                SkillModifierEntry entry = modifiers[i];
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginHorizontal();

                int channelIndex = Array.IndexOf((SkillModifierChannel[])Enum.GetValues(typeof(SkillModifierChannel)), entry.Channel);
                channelIndex = EditorGUILayout.Popup(Mathf.Max(0, channelIndex), SkillModifierChannelDisplayNames, GUILayout.MinWidth(180));
                entry.Channel = ((SkillModifierChannel[])Enum.GetValues(typeof(SkillModifierChannel)))[Mathf.Clamp(channelIndex, 0, SkillModifierChannelDisplayNames.Length - 1)];
                entry.Factor = EditorGUILayout.FloatField("Factor", entry.Factor);
                entry.Bonus = EditorGUILayout.FloatField("Bonus", entry.Bonus);

                GUI.color = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("删除", GUILayout.Width(52)))
                    removeAt = i;
                GUI.color = Color.white;

                EditorGUILayout.EndHorizontal();

                if (EditorGUI.EndChangeCheck())
                {
                    modifiers[i] = entry;
                    _isDirty = true;
                }
            }

            if (removeAt >= 0)
            {
                modifiers.RemoveAt(removeAt);
                _isDirty = true;
            }

            if (GUILayout.Button("+ 添加修正", GUILayout.Width(96)))
            {
                modifiers.Add(new SkillModifierEntry());
                _isDirty = true;
            }

            EditorGUI.indentLevel--;
        }

        private void DrawFollowupEffectList(List<SkillFollowupEffectData> followupEffects)
        {
            int removeAt = -1;
            for (int i = 0; i < followupEffects.Count; i++)
            {
                SkillFollowupEffectData followup = followupEffects[i] ?? new SkillFollowupEffectData();
                followup.EnsureDefaults();
                EditorGUI.BeginChangeCheck();

                EditorGUILayout.BeginVertical("box");
                followup.FilterType = (SkillFollowupFilterType)EditorGUILayout.EnumPopup("Filter", followup.FilterType);
                DrawFollowupConsumeRule(followup);

                switch (followup.FilterType)
                {
                    case SkillFollowupFilterType.SkillId:
                        followup.SkillId = EditorGUILayout.IntField("Skill Id", followup.SkillId);
                        break;
                    case SkillFollowupFilterType.SkillType:
                        followup.SkillType = (SkillType)EditorGUILayout.EnumPopup("Skill Type", followup.SkillType);
                        break;
                    case SkillFollowupFilterType.Element:
                        followup.Element = (ElementType)EditorGUILayout.EnumPopup("Element", followup.Element);
                        break;
                    case SkillFollowupFilterType.SkillAdditionId:
                        followup.SkillAdditionId = EditorGUILayout.IntField("Skill Addition Id", followup.SkillAdditionId);
                        break;
                }

                DrawFollowupModifierRule(followup);

                GUI.color = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("Delete Followup Effect", GUILayout.Width(148f)))
                    removeAt = i;
                GUI.color = Color.white;

                EditorGUILayout.EndVertical();

                if (EditorGUI.EndChangeCheck())
                {
                    followupEffects[i] = followup;
                    _isDirty = true;
                }
            }

            if (GUILayout.Button("+ Add Followup Effect", GUILayout.Width(140f)))
            {
                followupEffects.Add(new SkillFollowupEffectData());
                _isDirty = true;
            }

            if (removeAt >= 0)
            {
                followupEffects.RemoveAt(removeAt);
                _isDirty = true;
            }
        }

        private void DrawFollowupConsumeRule(SkillFollowupEffectData followup)
        {
            followup.EnsureDefaults();

            int selectedIndex = GetFollowupConsumeRuleIndex(followup.ConsumeRule);
            int nextIndex = EditorGUILayout.Popup("Consume Rule", selectedIndex, FollowupConsumeRuleNames);
            if (nextIndex != selectedIndex)
            {
                followup.ConsumeRule = (SkillFollowupConsumeRuleData)Activator.CreateInstance(FollowupConsumeRuleTypes[nextIndex]);
                _isDirty = true;
            }

            switch (followup.ConsumeRule)
            {
                case UseCountSkillFollowupConsumeRuleData useCountRuleData:
                    useCountRuleData.Uses = Mathf.Max(1, EditorGUILayout.IntField("Uses", useCountRuleData.Uses));
                    break;
            }
        }

        private void DrawFollowupModifierRule(SkillFollowupEffectData followup)
        {
            followup.EnsureDefaults();

            int selectedIndex = GetFollowupModifierRuleIndex(followup.ModifierRule);
            int nextIndex = EditorGUILayout.Popup("Modifier Rule", selectedIndex, FollowupModifierRuleNames);
            if (nextIndex != selectedIndex)
            {
                followup.ModifierRule = (SkillFollowupModifierRuleData)Activator.CreateInstance(FollowupModifierRuleTypes[nextIndex]);
                followup.EnsureDefaults();
                _isDirty = true;
            }

            switch (followup.ModifierRule)
            {
                case StaticSkillFollowupModifierRuleData staticRuleData:
                    staticRuleData.Modifiers ??= new List<SkillModifierEntry>();
                    DrawSkillModifierList("Applied Modifiers", staticRuleData.Modifiers);
                    break;

                case SequenceSkillFollowupModifierRuleData sequenceRuleData:
                    sequenceRuleData.ModifierSets ??= new List<SkillFollowupModifierSetData>();
                    DrawSequenceModifierSets(sequenceRuleData.ModifierSets);
                    break;
            }
        }

        private static int GetFollowupConsumeRuleIndex(SkillFollowupConsumeRuleData consumeRule)
        {
            if (consumeRule == null)
                return 0;

            for (int i = 0; i < FollowupConsumeRuleTypes.Length; i++)
            {
                if (FollowupConsumeRuleTypes[i] == consumeRule.GetType())
                    return i;
            }

            return 0;
        }

        private static int GetFollowupModifierRuleIndex(SkillFollowupModifierRuleData modifierRule)
        {
            if (modifierRule == null)
                return 0;

            for (int i = 0; i < FollowupModifierRuleTypes.Length; i++)
            {
                if (FollowupModifierRuleTypes[i] == modifierRule.GetType())
                    return i;
            }

            return 0;
        }

        private void DrawSequenceModifierSets(List<SkillFollowupModifierSetData> modifierSets)
        {
            int removeAt = -1;
            for (int i = 0; i < modifierSets.Count; i++)
            {
                modifierSets[i] ??= new SkillFollowupModifierSetData();
                modifierSets[i].Modifiers ??= new List<SkillModifierEntry>();

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Set {i}", EditorStyles.boldLabel);
                GUI.color = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("Delete Set", GUILayout.Width(88f)))
                    removeAt = i;
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();

                DrawSkillModifierList("Modifiers", modifierSets[i].Modifiers);
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ Add Modifier Set", GUILayout.Width(140f)))
            {
                modifierSets.Add(new SkillFollowupModifierSetData());
                _isDirty = true;
            }

            if (removeAt >= 0)
            {
                modifierSets.RemoveAt(removeAt);
                _isDirty = true;
            }
        }

        private void DrawConditionList(List<ConditionConfig> conditions, string keyPrefix)
        {
            string foldKey = keyPrefix + "_fold";
            if (!_conditionFoldStates.TryGetValue(foldKey, out bool isOpen))
                isOpen = true;

            isOpen = EditorGUILayout.Foldout(isOpen, $"Conditions ({conditions.Count})", true);
            _conditionFoldStates[foldKey] = isOpen;
            if (!isOpen)
                return;

            EditorGUI.indentLevel++;
            DrawAddConditionRow(conditions, keyPrefix);

            int removeAt = -1;
            for (int i = 0; i < conditions.Count; i++)
            {
                if (!DrawConditionRow(conditions[i]))
                    removeAt = i;
            }

            if (removeAt >= 0)
            {
                conditions.RemoveAt(removeAt);
                _isDirty = true;
            }

            EditorGUI.indentLevel--;
        }

        private void DrawAddConditionRow(List<ConditionConfig> conditions, string keyPrefix)
        {
            string sectionKey = keyPrefix + "_add_section";
            string sourceKey = keyPrefix + "_src";
            string compareKey = keyPrefix + "_cmp";
            if (!_conditionAddSectionStates.ContainsKey(sectionKey))
                _conditionAddSectionStates[sectionKey] = false;
            if (!_conditionAddSourceIndices.ContainsKey(sourceKey))
                _conditionAddSourceIndices[sourceKey] = 0;
            if (!_conditionAddCompareIndices.ContainsKey(compareKey))
                _conditionAddCompareIndices[compareKey] = 0;

            Rect headerRect = GetConditionContentRect();
            EditorGUI.DrawRect(headerRect, ConditionAddHeaderColor);
            Rect foldoutRect = new(headerRect.x + 6f, headerRect.y, headerRect.width - 12f, headerRect.height);
            bool isOpen = EditorGUI.Foldout(foldoutRect, _conditionAddSectionStates[sectionKey], "Add Condition", true);
            _conditionAddSectionStates[sectionKey] = isOpen;
            if (!isOpen)
            {
                GUILayout.Space(2f);
                return;
            }

            Rect rowRect = GetConditionContentRect();
            const float spacing = 4f;
            const float buttonWidth = 92f;
            Rect backgroundRect = new(rowRect.x, rowRect.y - 1f, rowRect.width, rowRect.height + 2f);
            EditorGUI.DrawRect(backgroundRect, ConditionAddBodyColor);
            rowRect.x += 6f;
            rowRect.width = Mathf.Max(0f, rowRect.width - 12f);

            Rect buttonRect = new(rowRect.xMax - buttonWidth, rowRect.y, buttonWidth, rowRect.height);
            float fieldsWidth = Mathf.Max(0f, buttonRect.x - rowRect.x - spacing);
            float sourceWidth = fieldsWidth;
            float compareWidth = 0f;

            if (_compareTypeNames.Length > 0)
                SplitConditionFieldWidths(fieldsWidth, 0.58f, 72f, 72f, out sourceWidth, out compareWidth);

            Rect sourceRect = new(rowRect.x, rowRect.y, sourceWidth, rowRect.height);
            Rect compareRect = new(sourceRect.xMax + spacing, rowRect.y, compareWidth, rowRect.height);

            if (_sourceTypeNames.Length > 0)
            {
                _conditionAddSourceIndices[sourceKey] = EditorGUI.Popup(
                    sourceRect,
                    _conditionAddSourceIndices[sourceKey],
                    _sourceTypeDisplayNames);
            }
            else
            {
                EditorGUI.LabelField(sourceRect, "No ISource", EditorStyles.miniLabel);
            }

            if (_compareTypeNames.Length > 0)
            {
                _conditionAddCompareIndices[compareKey] = EditorGUI.Popup(
                    compareRect,
                    _conditionAddCompareIndices[compareKey],
                    _compareTypeDisplayNames);
            }

            if (GUI.Button(buttonRect, "+ Condition"))
            {
                conditions.Add(new ConditionConfig
                {
                    SourceType = _sourceTypeNames.Length > 0 ? _sourceTypeNames[_conditionAddSourceIndices[sourceKey]] : string.Empty,
                    CompareType = _compareTypeNames.Length > 0 ? _compareTypeNames[_conditionAddCompareIndices[compareKey]] : string.Empty,
                    SourceParam = -1,
                    ConditionType = ConditionType.Necessary,
                });
                _isDirty = true;
            }

            GUILayout.Space(2f);
        }

        private bool DrawConditionRow(ConditionConfig condition)
        {
            EditorGUI.BeginChangeCheck();

            bool expectsValueWidth = condition.CompareType is "GreaterThan" or "LessThan" or "Equal";
            Rect rowRect = GetConditionContentRect();
            const float spacing = 4f;
            const float conditionTypeWidth = 118f;
            const float sourceParamWidth = 64f;
            const float valueWidth = 64f;
            const float deleteWidth = 24f;

            float trailingWidth = deleteWidth + sourceParamWidth + spacing + (expectsValueWidth ? valueWidth + spacing : 0f);
            float fieldsWidth = Mathf.Max(0f, rowRect.width - conditionTypeWidth - trailingWidth - spacing * 2f);
            SplitConditionFieldWidths(fieldsWidth, 0.58f, 72f, 72f, out float sourceWidth, out float compareWidth);

            Rect typeRect = new(rowRect.x, rowRect.y, conditionTypeWidth, rowRect.height);
            Rect sourceRect = new(typeRect.xMax + spacing, rowRect.y, sourceWidth, rowRect.height);
            Rect compareRect = new(sourceRect.xMax + spacing, rowRect.y, compareWidth, rowRect.height);
            Rect sourceParamRect = new(compareRect.xMax + spacing, rowRect.y, sourceParamWidth, rowRect.height);
            Rect valueRect = new(sourceParamRect.xMax + spacing, rowRect.y, valueWidth, rowRect.height);
            Rect deleteRect = new(rowRect.xMax - deleteWidth, rowRect.y, deleteWidth, rowRect.height);

            condition.ConditionType = (ConditionType)EditorGUI.EnumPopup(typeRect, condition.ConditionType);

            if (_sourceTypeNames.Length > 0)
            {
                int sourceIndex = Mathf.Max(0, Array.IndexOf(_sourceTypeNames, condition.SourceType));
                sourceIndex = EditorGUI.Popup(sourceRect, sourceIndex, _sourceTypeDisplayNames);
                condition.SourceType = _sourceTypeNames[sourceIndex];
            }
            else
            {
                condition.SourceType = EditorGUI.TextField(sourceRect, condition.SourceType);
            }

            if (_compareTypeNames.Length > 0)
            {
                int compareIndex = Mathf.Max(0, Array.IndexOf(_compareTypeNames, condition.CompareType));
                compareIndex = EditorGUI.Popup(compareRect, compareIndex, _compareTypeDisplayNames);
                condition.CompareType = _compareTypeNames[compareIndex];
            }
            else
            {
                condition.CompareType = EditorGUI.TextField(compareRect, condition.CompareType);
            }

            condition.SourceParam = EditorGUI.IntField(sourceParamRect, condition.SourceParam);

            bool needsValue = condition.CompareType is "GreaterThan" or "LessThan" or "Equal";
            if (needsValue)
                condition.CompareValue = EditorGUI.FloatField(valueRect, condition.CompareValue);

            if (EditorGUI.EndChangeCheck())
                _isDirty = true;

            GUI.color = new Color(1f, 0.5f, 0.5f);
            bool keep = !GUI.Button(deleteRect, "×");
            GUI.color = Color.white;

            return keep;
        }

        private static Rect GetConditionContentRect()
        {
            Rect rowRect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight));
            rowRect.x += 16f;
            rowRect.width = Mathf.Max(0f, rowRect.width - 16f);
            return rowRect;
        }

        private static void SplitConditionFieldWidths(
            float totalWidth,
            float leftRatio,
            float minLeftWidth,
            float minRightWidth,
            out float leftWidth,
            out float rightWidth)
        {
            if (totalWidth <= 0f)
            {
                leftWidth = 0f;
                rightWidth = 0f;
                return;
            }

            float desiredLeftWidth = totalWidth * leftRatio;
            leftWidth = Mathf.Clamp(desiredLeftWidth, minLeftWidth, Mathf.Max(minLeftWidth, totalWidth - minRightWidth));
            rightWidth = Mathf.Max(0f, totalWidth - leftWidth);

            if (rightWidth < minRightWidth)
            {
                rightWidth = Mathf.Min(minRightWidth, totalWidth);
                leftWidth = Mathf.Max(0f, totalWidth - rightWidth);
            }
        }

        private static void DrawSectionHeader(string title)
        {
            GUILayout.Space(6f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            Rect rect = GUILayoutUtility.GetLastRect();
            rect.y += rect.height + 1f;
            rect.height = 1f;
            EditorGUI.DrawRect(rect, SectionLine);
            GUILayout.Space(4f);
        }

        private class LayerMaskConverter : JsonConverter<LayerMask>
        {
            public override LayerMask ReadJson(JsonReader reader, Type objectType, LayerMask existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                return new LayerMask { value = Convert.ToInt32(reader.Value) };
            }

            public override void WriteJson(JsonWriter writer, LayerMask value, JsonSerializer serializer)
            {
                writer.WriteValue(value.value);
            }
        }

        private class UnityObjectConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return typeof(UnityEngine.Object).IsAssignableFrom(objectType);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                string path = reader.Value as string;
                return string.IsNullOrEmpty(path)
                    ? null
                    : AssetDatabase.LoadAssetAtPath(path, objectType);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();
                    return;
                }

                writer.WriteValue(AssetDatabase.GetAssetPath((UnityEngine.Object)value));
            }
        }

        private class Vector3Converter : JsonConverter<Vector3>
        {
            public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
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
                writer.WritePropertyName("x");
                writer.WriteValue(value.x);
                writer.WritePropertyName("y");
                writer.WriteValue(value.y);
                writer.WritePropertyName("z");
                writer.WriteValue(value.z);
                writer.WriteEndObject();
            }
        }
    }
}
