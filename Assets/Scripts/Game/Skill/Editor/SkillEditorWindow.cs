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

        private static readonly Type[] KnownEffectTypes =
        {
            typeof(AreaSearchEffectData),
            typeof(DamageEffectData),
            typeof(KnockbackEffectData),
            typeof(HitStunEffectData),
            typeof(PersistentEffectData),
            typeof(SpawnProjectileEffectData),
            typeof(SpawnSoundEffectData),
            typeof(SpawnVfxEffectData),
            typeof(CameraShakeEffectData),
        };

        private static readonly string[] KnownEffectNames =
        {
            "Area Search",
            "Damage",
            "Knockback",
            "Hit Stun",
            "Persistent",
            "Spawn Projectile",
            "Spawn Sound",
            "Spawn VFX",
            "Camera Shake",
        };

        private static readonly Color[] EffectColors =
        {
            new(0.14f, 0.38f, 0.60f),
            new(0.60f, 0.18f, 0.14f),
            new(0.55f, 0.33f, 0.14f),
            new(0.46f, 0.22f, 0.12f),
            new(0.14f, 0.50f, 0.24f),
            new(0.55f, 0.38f, 0.10f),
            new(0.38f, 0.18f, 0.55f),
            new(0.18f, 0.48f, 0.48f),
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
        private readonly Dictionary<string, int> _conditionAddSourceIndices = new();
        private readonly Dictionary<string, int> _conditionAddCompareIndices = new();

        private string[] _sourceTypeNames = Array.Empty<string>();
        private string[] _compareTypeNames = Array.Empty<string>();

        private static readonly Color SelectedColor = new(0.27f, 0.52f, 0.85f, 0.85f);
        private static readonly Color EvenRowColor = new(0.22f, 0.22f, 0.22f, 1f);
        private static readonly Color OddRowColor = new(0.25f, 0.25f, 0.25f, 1f);
        private static readonly Color HoverColor = new(0.32f, 0.32f, 0.32f, 1f);
        private static readonly Color SectionLine = new(0.45f, 0.45f, 0.45f, 1f);
        private static readonly Color DividerColor = new(0.15f, 0.15f, 0.15f, 1f);

        private static JsonSerializerSettings JsonSettings => new()
        {
            TypeNameHandling = TypeNameHandling.Auto,
            Formatting = Formatting.Indented,
            FloatFormatHandling = FloatFormatHandling.String,
            Converters = { new LayerMaskConverter(), new Vector3Converter(), new GameObjectConverter() },
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
            _sourceTypeNames = CollectTypeNames(typeof(ISource));
            _compareTypeNames = CollectTypeNames(typeof(ICompareType));
        }

        private static string[] CollectTypeNames(Type baseType)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly =>
                {
                    try
                    {
                        return assembly.GetTypes();
                    }
                    catch
                    {
                        return Array.Empty<Type>();
                    }
                })
                .Where(type => !type.IsAbstract && !type.IsInterface && baseType.IsAssignableFrom(type))
                .Select(type => type.Name)
                .OrderBy(name => name)
                .ToArray();
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
                Id = _rows.Count + 1,
                Name = $"New Skill {_rows.Count + 1}",
                Description = string.Empty,
                IconPath = string.Empty,
                MoveSpeedMultiplier = 1f,
                Conditions = new List<ConditionConfig>(),
                EffectChain = Array.Empty<EffectData>(),
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

            copy.Id = _rows.Count + 1;
            copy.Name = string.IsNullOrWhiteSpace(source.Name) ? $"Skill {copy.Id}" : $"{source.Name}_Copy";
            copy.Conditions ??= new List<ConditionConfig>();
            copy.EffectChain ??= Array.Empty<EffectData>();
            _rows.Add(copy);
            NormalizeRowIds();
            _selectedIndex = _rows.Count - 1;
            _isDirty = true;
            Repaint();
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

            SkillData row = _rows[fromIndex];
            _rows.RemoveAt(fromIndex);
            insertIndex = Mathf.Clamp(insertIndex, 0, _rows.Count);
            _rows.Insert(insertIndex, row);
            NormalizeRowIds();
            _selectedIndex = insertIndex;
            _isDirty = true;
            GUI.FocusControl(null);
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
                        GUI.FocusControl(null);
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
                    _selectedIndex = i;
                    GUI.FocusControl(null);
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
                    EditorGUI.BeginChangeCheck();
                    object oldValue = field.GetValue(effect);
                    object newValue = DrawEffectField(field.FieldType, field.Name, oldValue, entryKey);
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

            if (fieldType == typeof(GameObject))
                return EditorGUILayout.ObjectField(label, value as GameObject, typeof(GameObject), false);

            if (fieldType == typeof(List<ConditionConfig>))
            {
                List<ConditionConfig> conditions = value as List<ConditionConfig> ?? new List<ConditionConfig>();
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                DrawConditionList(conditions, $"{parentKey}/{label}");
                EditorGUI.indentLevel--;
                return conditions;
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
            string sourceKey = keyPrefix + "_src";
            string compareKey = keyPrefix + "_cmp";
            if (!_conditionAddSourceIndices.ContainsKey(sourceKey))
                _conditionAddSourceIndices[sourceKey] = 0;
            if (!_conditionAddCompareIndices.ContainsKey(compareKey))
                _conditionAddCompareIndices[compareKey] = 0;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16f);

            if (_sourceTypeNames.Length > 0)
            {
                _conditionAddSourceIndices[sourceKey] = EditorGUILayout.Popup(
                    _conditionAddSourceIndices[sourceKey],
                    _sourceTypeNames,
                    GUILayout.Width(130f));
            }
            else
            {
                GUILayout.Label("No ISource", EditorStyles.miniLabel, GUILayout.Width(80f));
            }

            if (_compareTypeNames.Length > 0)
            {
                _conditionAddCompareIndices[compareKey] = EditorGUILayout.Popup(
                    _conditionAddCompareIndices[compareKey],
                    _compareTypeNames,
                    GUILayout.Width(100f));
            }

            if (GUILayout.Button("+ Condition", GUILayout.Width(80f)))
            {
                conditions.Add(new ConditionConfig
                {
                    SourceType = _sourceTypeNames.Length > 0 ? _sourceTypeNames[_conditionAddSourceIndices[sourceKey]] : string.Empty,
                    CompareType = _compareTypeNames.Length > 0 ? _compareTypeNames[_conditionAddCompareIndices[compareKey]] : string.Empty,
                    ConditionType = ConditionType.Necessary,
                });
                _isDirty = true;
            }

            EditorGUILayout.EndHorizontal();
        }

        private bool DrawConditionRow(ConditionConfig condition)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16f);

            EditorGUI.BeginChangeCheck();

            condition.ConditionType = (ConditionType)EditorGUILayout.EnumPopup(condition.ConditionType, GUILayout.Width(88f));

            if (_sourceTypeNames.Length > 0)
            {
                int sourceIndex = Mathf.Max(0, Array.IndexOf(_sourceTypeNames, condition.SourceType));
                sourceIndex = EditorGUILayout.Popup(sourceIndex, _sourceTypeNames, GUILayout.Width(130f));
                condition.SourceType = _sourceTypeNames[sourceIndex];
            }
            else
            {
                condition.SourceType = EditorGUILayout.TextField(condition.SourceType, GUILayout.Width(130f));
            }

            if (_compareTypeNames.Length > 0)
            {
                int compareIndex = Mathf.Max(0, Array.IndexOf(_compareTypeNames, condition.CompareType));
                compareIndex = EditorGUILayout.Popup(compareIndex, _compareTypeNames, GUILayout.Width(100f));
                condition.CompareType = _compareTypeNames[compareIndex];
            }
            else
            {
                condition.CompareType = EditorGUILayout.TextField(condition.CompareType, GUILayout.Width(100f));
            }

            bool needsValue = condition.CompareType is "GreaterThan" or "LessThan" or "Equal";
            if (needsValue)
                condition.CompareValue = EditorGUILayout.FloatField(condition.CompareValue, GUILayout.Width(60f));
            else
                GUILayout.Space(64f);

            if (EditorGUI.EndChangeCheck())
                _isDirty = true;

            GUILayout.FlexibleSpace();
            GUI.color = new Color(1f, 0.5f, 0.5f);
            bool keep = !GUILayout.Button("×", GUILayout.Width(24f));
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            return keep;
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

        private class GameObjectConverter : JsonConverter<GameObject>
        {
            public override GameObject ReadJson(JsonReader reader, Type objectType, GameObject existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                string path = reader.Value as string;
                return string.IsNullOrEmpty(path)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            public override void WriteJson(JsonWriter writer, GameObject value, JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();
                    return;
                }

                writer.WriteValue(AssetDatabase.GetAssetPath(value));
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
