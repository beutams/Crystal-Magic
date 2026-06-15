using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Data
{
    public class SkillAdditionEditorWindow : EditorWindow
    {
        private const string DataPath = "Assets/Res/Data/SkillAdditionDataTable.json";
        private const string UnitDataPath = "Assets/Res/Data/UnitDataTable.json";
        private const float ListPanelWidth = 220f;
        private const float ItemHeight = 26f;
        private const float InsertFieldWidth = 30f;
        private const float LabelWidth = 150f;
        private static readonly SkillModifierChannel[] EditableSkillModifierChannels = SkillModifierChannelUtility.GetEditableChannels();
        private static readonly string[] SkillModifierChannelDisplayNames = SkillModifierChannelUtility.GetEditableDisplayNames();

        private static readonly Type[] KnownEffectTypes =
        {
            typeof(ApplyBuffEffectData),
            typeof(AreaSearchEffectData),
            typeof(ChainSearchEffectData),
            typeof(ConeSearchEffectData),
            typeof(DamageEffectData),
            typeof(ForwardRectSearchEffectData),
            typeof(HealEffectData),
            typeof(HealthCostEffectData),
            typeof(FearEffectData),
            typeof(KnockbackEffectData),
            typeof(PersistentBeamEffectData),
            typeof(PersistentEffectData),
            typeof(RandomAreaPointEffectData),
            typeof(ReadBuffStackEffectData),
            typeof(RemoveBuffEffectData),
            typeof(RestoreManaEffectData),
            typeof(SpawnProjectileEffectData),
            typeof(SpawnSoundEffectData),
            typeof(SpawnUnitEffectData),
            typeof(SpawnVfxEffectData),
            typeof(StunEffectData),
            typeof(CameraShakeEffectData),
        };

        private static readonly string[] KnownEffectNames =
        {
            "Apply Buff",
            "Area Search",
            "Chain Search",
            "Cone Search",
            "Damage",
            "Forward Rect Search",
            "Heal",
            "Health Cost",
            "Fear",
            "Knockback",
            "Persistent Beam",
            "Persistent",
            "Random Area Points",
            "Read Buff Stack",
            "Remove Buff",
            "Restore Mana",
            "Spawn Projectile",
            "Spawn Sound",
            "Spawn Unit",
            "Spawn VFX",
            "Stun",
            "Camera Shake",
        };

        private static readonly Color[] EffectColors =
        {
            new(0.34f, 0.22f, 0.56f),
            new(0.14f, 0.38f, 0.60f),
            new(0.18f, 0.42f, 0.74f),
            new(0.20f, 0.50f, 0.70f),
            new(0.60f, 0.18f, 0.14f),
            new(0.60f, 0.30f, 0.12f),
            new(0.16f, 0.52f, 0.22f),
            new(0.42f, 0.16f, 0.16f),
            new(0.42f, 0.24f, 0.12f),
            new(0.55f, 0.33f, 0.14f),
            new(0.68f, 0.26f, 0.12f),
            new(0.14f, 0.50f, 0.24f),
            new(0.26f, 0.50f, 0.24f),
            new(0.22f, 0.42f, 0.64f),
            new(0.50f, 0.18f, 0.18f),
            new(0.14f, 0.46f, 0.60f),
            new(0.55f, 0.38f, 0.10f),
            new(0.38f, 0.18f, 0.55f),
            new(0.46f, 0.30f, 0.14f),
            new(0.18f, 0.48f, 0.48f),
            new(0.32f, 0.32f, 0.32f),
            new(0.58f, 0.42f, 0.12f),
        };

        private List<SkillAdditionData> _rows = new();
        private bool _isDirty;
        private string _statusText = string.Empty;

        private int _selectedIndex = -1;
        private int _addEffectTypeIndex;
        private int _addCastTaskTypeIndex;
        private Vector2 _listScrollPos;
        private Vector2 _detailScrollPos;
        private readonly Dictionary<SkillAdditionData, string> _insertTexts = new();
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
        private static readonly Color DividerColor = new(0.15f, 0.15f, 0.15f, 1f);
        private static readonly Color SectionLine = new(0.45f, 0.45f, 0.45f, 1f);
        private static readonly Color ConditionAddHeaderColor = new(0.15f, 0.15f, 0.15f, 1f);
        private static readonly Color ConditionAddBodyColor = new(0.18f, 0.18f, 0.18f, 1f);
        private static readonly Type[] CastTaskTypes =
        {
            typeof(DoubleExecuteSkillCastTaskData),
            typeof(ApplyRuntimeBuffSkillCastTaskData),
            typeof(JumpArcSkillCastTaskData),
            typeof(TurnToTargetSkillCastTaskData),
            typeof(RepeatCastWithRetargetSkillCastTaskData),
        };

        private static readonly string[] CastTaskNames =
        {
            "Double Execute",
            "Apply Runtime Buff",
            "Jump Arc",
            "Turn To Target",
            "Repeat Cast With Retarget",
        };
        private static readonly SkillCastHookPoint[] SkillCastHookPointValues = (SkillCastHookPoint[])Enum.GetValues(typeof(SkillCastHookPoint));
        private static readonly string[] SkillCastHookPointDisplayNames = EditorLabelUtility.GetEnumDisplayNames<SkillCastHookPoint>();

        private static JsonSerializerSettings JsonSettings => new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
        };

        private class TableWrapper
        {
            public List<SkillAdditionData> Rows = new();
        }

        private class UnitTableWrapper
        {
            public List<UnitData> Rows = new();
        }

        private static JsonSerializerSettings UnitJsonSettings => new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
        };

        [MenuItem("Tools/Data/Skill Addition Editor")]
        public static void Open()
        {
            SkillAdditionEditorWindow window = GetWindow<SkillAdditionEditorWindow>("Skill Addition Editor");
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
                Debug.LogError($"[SkillAdditionEditor] Load error:\n{ex}");
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
                Debug.LogError($"[SkillAdditionEditor] Save error:\n{ex}");
            }
        }

        private void AddRow()
        {
            _rows.Add(new SkillAdditionData
            {
                Id = _rows.Count,
                Name = $"New Skill Effect {_rows.Count}",
                Description = string.Empty,
                IconPath = string.Empty,
                Modifiers = new List<SkillModifierEntry>(),
                FollowupEffects = new List<SkillFollowupEffectData>(),
                CastTasks = new List<SkillCastTaskData>(),
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

            SkillAdditionData removedRow = _rows[_selectedIndex];
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

            SkillAdditionData source = _rows[_selectedIndex];
            string json = JsonConvert.SerializeObject(source, JsonSettings);
            SkillAdditionData copy = JsonConvert.DeserializeObject<SkillAdditionData>(json, JsonSettings);
            if (copy == null)
                return;

            copy.Id = _rows.Count;
            copy.Name = string.IsNullOrWhiteSpace(source.Name) ? $"Skill Effect {copy.Id}" : $"{source.Name}_Copy";
            copy.Modifiers ??= new List<SkillModifierEntry>();
            copy.FollowupEffects ??= new List<SkillFollowupEffectData>();
            copy.CastTasks ??= new List<SkillCastTaskData>();
            copy.EffectChain ??= Array.Empty<EffectData>();
            _rows.Add(copy);
            NormalizeRowIds();
            _selectedIndex = _rows.Count - 1;
            _isDirty = true;
            Repaint();
        }

        private Dictionary<int, int> NormalizeRowIds()
        {
            Dictionary<int, int> idRemap = new();
            for (int i = 0; i < _rows.Count; i++)
            {
                int oldId = _rows[i].Id;
                int newId = i;
                idRemap[oldId] = newId;
                _rows[i].Id = newId;
            }

            return idRemap;
        }

        private void MoveRowToInsertIndex(int fromIndex, int insertIndex)
        {
            if (fromIndex < 0 || fromIndex >= _rows.Count)
                return;

            insertIndex = Mathf.Clamp(insertIndex, 0, _rows.Count - 1);
            if (fromIndex == insertIndex)
                return;

            SkillAdditionData row = _rows[fromIndex];
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
                AddRow();

            GUI.enabled = _selectedIndex >= 0;
            if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                DuplicateSelected();

            GUI.color = _selectedIndex >= 0 ? new Color(1f, 0.55f, 0.55f) : Color.white;
            if (GUILayout.Button("Delete", EditorStyles.toolbarButton, GUILayout.Width(52f)))
            {
                if (EditorUtility.DisplayDialog("Delete Skill Effect", $"Delete {_rows[_selectedIndex].Name}?", "Delete", "Cancel"))
                    DeleteSelected();
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
            GUILayout.Label($"Skill Effects ({_rows.Count})", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _listScrollPos = EditorGUILayout.BeginScrollView(_listScrollPos, GUILayout.ExpandHeight(true));
            Event evt = Event.current;
            SkillAdditionData moveRow = null;
            int moveToIndex = -1;

            for (int i = 0; i < _rows.Count; i++)
            {
                SkillAdditionData row = _rows[i];
                bool isSelected = i == _selectedIndex;
                Rect itemRect = GUILayoutUtility.GetRect(ListPanelWidth, ItemHeight, GUILayout.ExpandWidth(true));

                Color bg = isSelected ? SelectedColor : itemRect.Contains(evt.mousePosition) ? HoverColor : i % 2 == 0 ? EvenRowColor : OddRowColor;
                EditorGUI.DrawRect(itemRect, bg);

                Rect insertRect = new(itemRect.x + 6f, itemRect.y + 3f, InsertFieldWidth, itemRect.height - 6f);
                string insertText = _insertTexts.TryGetValue(row, out string currentInsertText) ? currentInsertText : string.Empty;
                string controlName = $"insert_{row.GetHashCode()}";
                GUI.SetNextControlName(controlName);
                string newInsertText = EditorGUI.TextField(insertRect, insertText);
                if (newInsertText != insertText)
                {
                    if (string.IsNullOrWhiteSpace(newInsertText))
                        _insertTexts.Remove(row);
                    else
                        _insertTexts[row] = newInsertText;
                }

                bool isFocused = GUI.GetNameOfFocusedControl() == controlName;
                bool submitByEnter = isFocused && evt.type == EventType.KeyDown &&
                    (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter);
                bool submitByBlur = !isFocused && !string.IsNullOrWhiteSpace(newInsertText) && newInsertText == insertText;
                if ((submitByEnter || submitByBlur) && int.TryParse(newInsertText, out int insertTo))
                {
                    moveRow = row;
                    moveToIndex = Mathf.Clamp(insertTo - 1, 0, _rows.Count - 1);
                    _insertTexts.Remove(row);
                    if (submitByEnter)
                    {
                        evt.Use();
                        CrystalMagic.Editor.EditorFocusUtility.ClearTextFocus();
                    }
                }

                string label = $"[{row.Id}] {(string.IsNullOrWhiteSpace(row.Name) ? "Unnamed" : row.Name)}";
                GUI.Label(new Rect(insertRect.xMax + 8f, itemRect.y + 4f, itemRect.width - insertRect.width - 12f, itemRect.height - 4f),
                    label, isSelected ? EditorStyles.whiteLabel : EditorStyles.label);

                if (evt.type == EventType.MouseDown && itemRect.Contains(evt.mousePosition) && !insertRect.Contains(evt.mousePosition))
                {
                    if (_selectedIndex != i)
                        CrystalMagic.Editor.EditorFocusUtility.ClearTextFocus();
                    _selectedIndex = i;
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
            Rect rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, DividerColor);
        }

        private void DrawDetailPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Select a skill effect on the left", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            SkillAdditionData row = _rows[_selectedIndex];
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"[{row.Id}] {row.Name}", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _detailScrollPos = EditorGUILayout.BeginScrollView(_detailScrollPos);
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = LabelWidth;

            EditorGUI.BeginChangeCheck();
            DrawSectionHeader("Basic");
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.IntField("Id", row.Id);
            row.Name = EditorGUILayout.TextField("Name", row.Name ?? string.Empty);
            row.IconPath = EditorGUILayout.TextField("Icon Path", row.IconPath ?? string.Empty);
            EditorGUILayout.LabelField("Description");
            row.Description = EditorGUILayout.TextArea(row.Description ?? string.Empty, GUILayout.MinHeight(48f), GUILayout.MaxHeight(80f));
            if (EditorGUI.EndChangeCheck())
                _isDirty = true;

            DrawSectionHeader("Modifiers");
            DrawModifierList(row);

            DrawSectionHeader("Followup Effects");
            DrawFollowupEffectList(row);

            DrawSectionHeader("Cast Tasks");
            DrawCastTaskList(row);

            DrawSectionHeader("Effect Chain");
            row.EffectChain ??= Array.Empty<EffectData>();
            row.EffectChain = DrawEffectChainInline("__addition_root__", row.EffectChain, ref _addEffectTypeIndex);

            EditorGUIUtility.labelWidth = previousLabelWidth;
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawModifierList(SkillAdditionData row)
        {
            row.Modifiers ??= new List<SkillModifierEntry>();

            int removeAt = -1;
            for (int i = 0; i < row.Modifiers.Count; i++)
            {
                SkillModifierEntry entry = row.Modifiers[i];

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginHorizontal();
                int channelIndex = Array.IndexOf(EditableSkillModifierChannels, entry.Channel);
                channelIndex = EditorGUILayout.Popup(Mathf.Max(0, channelIndex), SkillModifierChannelDisplayNames, GUILayout.MinWidth(180f));
                entry.Channel = EditableSkillModifierChannels[Mathf.Clamp(channelIndex, 0, EditableSkillModifierChannels.Length - 1)];

                float previousLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 46f;
                entry.Factor = EditorGUILayout.FloatField("Factor", entry.Factor, GUILayout.MinWidth(90f));
                entry.Bonus = EditorGUILayout.FloatField("Bonus", entry.Bonus, GUILayout.MinWidth(90f));
                EditorGUIUtility.labelWidth = previousLabelWidth;

                if (GUILayout.Button("Delete", GUILayout.Width(52f)))
                    removeAt = i;

                EditorGUILayout.EndHorizontal();

                if (EditorGUI.EndChangeCheck())
                {
                    row.Modifiers[i] = entry;
                    _isDirty = true;
                }
            }

            if (GUILayout.Button("+ Add Modifier", GUILayout.Width(120f)))
            {
                row.Modifiers.Add(new SkillModifierEntry());
                _isDirty = true;
            }

            if (removeAt >= 0)
            {
                row.Modifiers.RemoveAt(removeAt);
                _isDirty = true;
            }
        }

        private void DrawFollowupEffectList(SkillAdditionData row)
        {
            row.FollowupEffects ??= new List<SkillFollowupEffectData>();

            int removeAt = -1;
            for (int i = 0; i < row.FollowupEffects.Count; i++)
            {
                SkillFollowupEffectData followup = row.FollowupEffects[i] ?? new SkillFollowupEffectData();
                followup.EnsureDefaults();
                EditorGUI.BeginChangeCheck();

                EditorGUILayout.BeginVertical("box");
                DrawFollowupFilter(followup);
                DrawFollowupConsumeRule(followup);
                DrawFollowupModifierRule(followup);

                if (GUILayout.Button("Delete Followup Effect", GUILayout.Width(148f)))
                    removeAt = i;

                EditorGUILayout.EndVertical();

                if (EditorGUI.EndChangeCheck())
                {
                    row.FollowupEffects[i] = followup;
                    _isDirty = true;
                }
            }

            if (GUILayout.Button("+ Add Followup Effect", GUILayout.Width(140f)))
            {
                row.FollowupEffects.Add(new SkillFollowupEffectData());
                _isDirty = true;
            }

            if (removeAt >= 0)
            {
                row.FollowupEffects.RemoveAt(removeAt);
                _isDirty = true;
            }
        }

        private void DrawFollowupFilter(SkillFollowupEffectData followup)
        {
            followup.EnsureDefaults();

            IReadOnlyList<FactoryTypeInfo> filterTypeInfos = SkillFollowupFilterRegistry.FilterTypeInfos;
            if (filterTypeInfos == null || filterTypeInfos.Count == 0)
            {
                EditorGUILayout.HelpBox("No followup filters registered.", MessageType.Warning);
                return;
            }

            string currentKey = SkillFollowupFilterRegistry.GetFilterKey(followup.Filter);
            int selectedIndex = GetFactoryTypeIndex(filterTypeInfos, currentKey);
            string[] displayNames = GetFactoryDisplayNames(filterTypeInfos);
            int nextIndex = EditorGUILayout.Popup("Filter", selectedIndex, displayNames);
            if (nextIndex != selectedIndex)
            {
                followup.Filter = SkillFollowupFilterRegistry.CreateFilterData(filterTypeInfos[Mathf.Clamp(nextIndex, 0, filterTypeInfos.Count - 1)].Key);
                followup.Filter?.EnsureDefaults();
                _isDirty = true;
            }

            if (followup.Filter is RuntimeTypeFollowupFilterData runtimeTypeFilter)
                DrawFollowupRuntimeTypeField(runtimeTypeFilter);
            else
                DrawSerializableObjectFields(followup.Filter, "FollowupFilter");

            followup.Filter?.EnsureDefaults();
        }

        private void DrawCastTaskList(SkillAdditionData row)
        {
            row.CastTasks ??= new List<SkillCastTaskData>();

            EditorGUILayout.BeginHorizontal();
            _addCastTaskTypeIndex = EditorGUILayout.Popup(_addCastTaskTypeIndex, CastTaskNames, GUILayout.Width(190f));
            if (GUILayout.Button("+ Add", GUILayout.Width(66f)))
            {
                row.CastTasks.Add((SkillCastTaskData)Activator.CreateInstance(CastTaskTypes[_addCastTaskTypeIndex]));
                _isDirty = true;
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2f);

            int removeAt = -1;
            for (int i = 0; i < row.CastTasks.Count; i++)
            {
                SkillCastTaskData task = row.CastTasks[i];
                if (task == null)
                    continue;

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                int taskTypeIndex = Mathf.Max(0, Array.IndexOf(CastTaskTypes, task.GetType()));
                EditorGUILayout.LabelField($"{CastTaskNames[taskTypeIndex]} #{i + 1}", EditorStyles.boldLabel);
                if (GUILayout.Button("Delete Task", GUILayout.Width(92f)))
                    removeAt = i;
                EditorGUILayout.EndHorizontal();

                DrawHookPointField(task);
                DrawCastTaskFields(task);
                EditorGUILayout.EndVertical();

                if (EditorGUI.EndChangeCheck())
                {
                    row.CastTasks[i] = task;
                    _isDirty = true;
                }
            }

            if (removeAt >= 0)
            {
                row.CastTasks.RemoveAt(removeAt);
                _isDirty = true;
            }
        }

        private void DrawHookPointField(SkillCastTaskData task)
        {
            int hookIndex = Mathf.Max(0, Array.IndexOf(SkillCastHookPointValues, task.HookPoint));
            hookIndex = EditorGUILayout.Popup("Hook Point", hookIndex, SkillCastHookPointDisplayNames);
            task.HookPoint = SkillCastHookPointValues[Mathf.Clamp(hookIndex, 0, SkillCastHookPointValues.Length - 1)];
        }

        private void DrawCastTaskFields(SkillCastTaskData task)
        {
            switch (task)
            {
                case DoubleExecuteSkillCastTaskData doubleExecuteTaskData:
                    doubleExecuteTaskData.DelaySeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Delay Seconds", doubleExecuteTaskData.DelaySeconds));
                    doubleExecuteTaskData.RuntimeModifiers ??= new List<SkillModifierEntry>();
                    DrawModifierEntries("Runtime Modifiers", doubleExecuteTaskData.RuntimeModifiers);
                    break;

                case ApplyRuntimeBuffSkillCastTaskData runtimeBuffTaskData:
                    runtimeBuffTaskData.BuffId = EditorGUILayout.IntField("Buff Id", runtimeBuffTaskData.BuffId);
                    runtimeBuffTaskData.StackCount = Mathf.Max(1, EditorGUILayout.IntField("Stack Count", runtimeBuffTaskData.StackCount));
                    break;

                case JumpArcSkillCastTaskData jumpArcTaskData:
                    jumpArcTaskData.DurationSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Duration Seconds", jumpArcTaskData.DurationSeconds));
                    jumpArcTaskData.ArcHeight = Mathf.Max(0f, EditorGUILayout.FloatField("Arc Height", jumpArcTaskData.ArcHeight));
                    break;

                case TurnToTargetSkillCastTaskData turnToTargetTaskData:
                    turnToTargetTaskData.DurationSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Duration Seconds", turnToTargetTaskData.DurationSeconds));
                    turnToTargetTaskData.TurnRateDegreesPerSecond = Mathf.Max(0f, EditorGUILayout.FloatField("Turn Rate Degrees Per Second", turnToTargetTaskData.TurnRateDegreesPerSecond));
                    break;

                case RepeatCastWithRetargetSkillCastTaskData repeatCastTaskData:
                    repeatCastTaskData.AdditionalCastCount = Mathf.Max(0, EditorGUILayout.IntField("Additional Cast Count", repeatCastTaskData.AdditionalCastCount));
                    repeatCastTaskData.IntervalSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Interval Seconds", repeatCastTaskData.IntervalSeconds));
                    repeatCastTaskData.RetargetBeforeEachCast = EditorGUILayout.Toggle("Retarget Before Each Cast", repeatCastTaskData.RetargetBeforeEachCast);
                    break;
            }
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

            if (GUI.Button(new Rect(buttonX, headerRect.y + 2f, 22f, 18f), expanded ? "v" : ">", EditorStyles.miniButton))
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
            bool deleted = GUI.Button(new Rect(buttonX + 78f, headerRect.y + 2f, 24f, 18f), "X", EditorStyles.miniButton);
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
                DrawModifierEntries(label, modifiers);
                return modifiers;
            }

            if (fieldType == typeof(List<SkillFollowupModifierSetData>))
            {
                List<SkillFollowupModifierSetData> modifierSets = value as List<SkillFollowupModifierSetData> ?? new List<SkillFollowupModifierSetData>();
                DrawSequenceModifierSets(modifierSets);
                return modifierSets;
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

        private void DrawSerializableObjectFields(object target, string parentKey)
        {
            if (target == null)
                return;

            FieldInfo[] fields = target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.IsStatic)
                    continue;

                string label = EditorLabelUtility.GetLabel(field);
                object currentValue = field.GetValue(target);
                EditorGUI.BeginChangeCheck();
                object nextValue = DrawEffectField(field.FieldType, label, currentValue, $"{parentKey}/{field.Name}");
                if (!EditorGUI.EndChangeCheck())
                    continue;

                field.SetValue(target, nextValue);
                _isDirty = true;
            }
        }

        private void DrawFollowupConsumeRule(SkillFollowupEffectData followup)
        {
            followup.EnsureDefaults();

            IReadOnlyList<FactoryTypeInfo> ruleTypeInfos = SkillFollowupConsumeRuleRegistry.RuleTypeInfos;
            if (ruleTypeInfos == null || ruleTypeInfos.Count == 0)
            {
                EditorGUILayout.HelpBox("No consume rules registered.", MessageType.Warning);
                return;
            }

            string currentKey = SkillFollowupConsumeRuleRegistry.GetRuleKey(followup.ConsumeRule);
            int selectedIndex = GetFactoryTypeIndex(ruleTypeInfos, currentKey);
            string[] displayNames = GetFactoryDisplayNames(ruleTypeInfos);
            int nextIndex = EditorGUILayout.Popup("Consume Rule", selectedIndex, displayNames);
            if (nextIndex != selectedIndex)
            {
                followup.ConsumeRule = SkillFollowupConsumeRuleRegistry.CreateRuleData(ruleTypeInfos[Mathf.Clamp(nextIndex, 0, ruleTypeInfos.Count - 1)].Key);
                followup.ConsumeRule?.EnsureDefaults();
                _isDirty = true;
            }

            DrawSerializableObjectFields(followup.ConsumeRule, "FollowupConsumeRule");
            followup.ConsumeRule?.EnsureDefaults();
        }

        private void DrawFollowupModifierRule(SkillFollowupEffectData followup)
        {
            followup.EnsureDefaults();

            IReadOnlyList<FactoryTypeInfo> ruleTypeInfos = SkillFollowupModifierRuleRegistry.RuleTypeInfos;
            if (ruleTypeInfos == null || ruleTypeInfos.Count == 0)
            {
                EditorGUILayout.HelpBox("No modifier rules registered.", MessageType.Warning);
                return;
            }

            string currentKey = SkillFollowupModifierRuleRegistry.GetRuleKey(followup.ModifierRule);
            int selectedIndex = GetFactoryTypeIndex(ruleTypeInfos, currentKey);
            string[] displayNames = GetFactoryDisplayNames(ruleTypeInfos);
            int nextIndex = EditorGUILayout.Popup("Modifier Rule", selectedIndex, displayNames);
            if (nextIndex != selectedIndex)
            {
                followup.ModifierRule = SkillFollowupModifierRuleRegistry.CreateRuleData(ruleTypeInfos[Mathf.Clamp(nextIndex, 0, ruleTypeInfos.Count - 1)].Key);
                followup.ModifierRule?.EnsureDefaults();
                _isDirty = true;
            }

            DrawSerializableObjectFields(followup.ModifierRule, "FollowupModifierRule");
            followup.ModifierRule?.EnsureDefaults();
        }

        private void DrawFollowupRuntimeTypeField(RuntimeTypeFollowupFilterData filter)
        {
            string currentKey = filter.EffectiveRuntimeType;
            IReadOnlyList<FactoryTypeInfo> runtimeTypeInfos = SkillRegistry.SkillRuntimeTypeInfos;
            if (runtimeTypeInfos == null || runtimeTypeInfos.Count == 0)
            {
                filter.RuntimeType = EditorGUILayout.TextField("Runtime Type", currentKey);
                return;
            }

            string[] displayNames = new string[runtimeTypeInfos.Count];
            int selectedIndex = -1;
            for (int i = 0; i < runtimeTypeInfos.Count; i++)
            {
                FactoryTypeInfo info = runtimeTypeInfos[i];
                displayNames[i] = $"{info.DisplayName} ({info.Key})";
                if (string.Equals(info.Key, currentKey, StringComparison.Ordinal))
                    selectedIndex = i;
            }

            if (selectedIndex < 0)
            {
                filter.RuntimeType = EditorGUILayout.TextField("Runtime Type", currentKey);
                return;
            }

            int nextIndex = EditorGUILayout.Popup("Runtime Type", selectedIndex, displayNames);
            filter.RuntimeType = runtimeTypeInfos[Mathf.Clamp(nextIndex, 0, runtimeTypeInfos.Count - 1)].Key;
        }

        private static int GetFactoryTypeIndex(IReadOnlyList<FactoryTypeInfo> typeInfos, string key)
        {
            if (typeInfos == null || typeInfos.Count == 0)
                return 0;

            for (int i = 0; i < typeInfos.Count; i++)
            {
                if (string.Equals(typeInfos[i].Key, key, StringComparison.Ordinal))
                    return i;
            }

            return 0;
        }

        private static string[] GetFactoryDisplayNames(IReadOnlyList<FactoryTypeInfo> typeInfos)
        {
            string[] displayNames = new string[typeInfos.Count];
            for (int i = 0; i < typeInfos.Count; i++)
                displayNames[i] = $"{typeInfos[i].DisplayName} ({typeInfos[i].Key})";

            return displayNames;
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
                if (GUILayout.Button("Delete Set", GUILayout.Width(88f)))
                    removeAt = i;
                EditorGUILayout.EndHorizontal();

                DrawModifierEntries("Modifiers", modifierSets[i].Modifiers);
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

        private void DrawModifierEntries(string label, List<SkillModifierEntry> modifiers)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            int removeAt = -1;
            for (int i = 0; i < modifiers.Count; i++)
            {
                SkillModifierEntry entry = modifiers[i];
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginHorizontal();
                int channelIndex = Array.IndexOf(EditableSkillModifierChannels, entry.Channel);
                channelIndex = EditorGUILayout.Popup(Mathf.Max(0, channelIndex), SkillModifierChannelDisplayNames, GUILayout.MinWidth(180f));
                entry.Channel = EditableSkillModifierChannels[Mathf.Clamp(channelIndex, 0, EditableSkillModifierChannels.Length - 1)];

                float previousLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 46f;
                entry.Factor = EditorGUILayout.FloatField("Factor", entry.Factor, GUILayout.MinWidth(90f));
                entry.Bonus = EditorGUILayout.FloatField("Bonus", entry.Bonus, GUILayout.MinWidth(90f));
                EditorGUIUtility.labelWidth = previousLabelWidth;

                if (GUILayout.Button("Delete", GUILayout.Width(52f)))
                    removeAt = i;

                EditorGUILayout.EndHorizontal();

                if (EditorGUI.EndChangeCheck())
                {
                    modifiers[i] = entry;
                    _isDirty = true;
                }
            }

            if (GUILayout.Button("+ Add Modifier", GUILayout.Width(120f)))
            {
                modifiers.Add(new SkillModifierEntry());
                _isDirty = true;
            }

            if (removeAt >= 0)
            {
                modifiers.RemoveAt(removeAt);
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
                _conditionAddSourceIndices[sourceKey] = EditorGUI.Popup(sourceRect, _conditionAddSourceIndices[sourceKey], _sourceTypeDisplayNames);
            else
                EditorGUI.LabelField(sourceRect, "No ISource", EditorStyles.miniLabel);

            if (_compareTypeNames.Length > 0)
                _conditionAddCompareIndices[compareKey] = EditorGUI.Popup(compareRect, _conditionAddCompareIndices[compareKey], _compareTypeDisplayNames);

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

            if (expectsValueWidth)
                condition.CompareValue = EditorGUI.FloatField(valueRect, condition.CompareValue);

            if (EditorGUI.EndChangeCheck())
                _isDirty = true;

            GUI.color = new Color(1f, 0.5f, 0.5f);
            bool keep = !GUI.Button(deleteRect, "X");
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
    }
}
