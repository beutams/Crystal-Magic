using System;
using System.Collections.Generic;
using System.IO;
using CrystalMagic.Core;
using CrystalMagic.Editor.EffectGraph;
using CrystalMagic.Editor.Unit;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Data
{
    public sealed class SkillAdditionEditorWindow : EditorWindow
    {
        private const string DataPath = "Assets/Res/Data/SkillAdditionDataTable.json";
        private static readonly JsonSerializerSettings s_jsonSettings = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
            Converters = new List<JsonConverter>
            {
                new StateScriptUnitValueConverter(),
            },
        };

        private readonly List<SkillAdditionData> _rows = new();
        private Vector2 _rowScrollPosition;
        private int _selectedIndex = -1;
        private string _status = string.Empty;
        private bool _isDirty;
        private UnitSourceSchema _sourceSchema;

        private sealed class TableWrapper
        {
            public List<SkillAdditionData> Rows = new();
        }

        [MenuItem("Tools/Data/Skill Addition Editor")]
        public static void Open()
        {
            SkillAdditionEditorWindow window = GetWindow<SkillAdditionEditorWindow>("Skill Addition Editor");
            window.minSize = new Vector2(820f, 500f);
            window.Show();
        }

        private void OnEnable()
        {
            _sourceSchema = UnitSourceSchemaFactory.CreateForAllSources();
            Load();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            using (new EditorGUI.DisabledScope(!_isDirty))
            {
                if (GUILayout.Button(_isDirty ? "Save *" : "Save", EditorStyles.toolbarButton))
                    Save();
            }
            if (GUILayout.Button("Add", EditorStyles.toolbarButton))
                AddRow();
            using (new EditorGUI.DisabledScope(_selectedIndex < 0))
            {
                if (GUILayout.Button("Copy", EditorStyles.toolbarButton))
                    DuplicateSelectedRow();
                if (GUILayout.Button("Delete", EditorStyles.toolbarButton))
                    DeleteSelectedRow();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawRowList();
            DrawDetail();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(_status))
                EditorGUILayout.HelpBox(_status, MessageType.Info);
        }

        private void DrawRowList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(260f));
            EditorGUILayout.LabelField("Additions", EditorStyles.boldLabel);
            _rowScrollPosition = EditorGUILayout.BeginScrollView(_rowScrollPosition);
            for (int i = 0; i < _rows.Count; i++)
            {
                SkillAdditionData row = _rows[i];
                string label = $"[{row.Id}] {row.NameKey}";
                if (GUILayout.Toggle(_selectedIndex == i, label, "Button"))
                    Select(i);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawDetail()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
            {
                EditorGUILayout.HelpBox("Select or add an Addition. The old Followup/CastTask model is not supported.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            SkillAdditionData row = _rows[_selectedIndex];
            EditorGUI.BeginChangeCheck();
            row.NameKey = EditorGUILayout.TextField("Name Key", row.NameKey);
            row.DescriptionKey = EditorGUILayout.TextField("Description Key", row.DescriptionKey);
            row.IconPath = EditorGUILayout.TextField("Icon Path", row.IconPath);
            if (EditorGUI.EndChangeCheck())
            {
                MarkDirty();
            }

            DrawCallbacks(row);
            EditorGUILayout.EndVertical();
        }

        private void DrawCallbacks(SkillAdditionData row)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Callbacks", EditorStyles.boldLabel);
            row.Callbacks ??= new List<SkillAdditionCallbackData>();
            if (GUILayout.Button("Add Callback", GUILayout.Width(120f)))
            {
                row.Callbacks.Add(new SkillAdditionCallbackData());
                MarkDirty();
            }

            int removeCallback = -1;
            for (int callbackIndex = 0; callbackIndex < row.Callbacks.Count; callbackIndex++)
            {
                SkillAdditionCallbackData callback = row.Callbacks[callbackIndex] ?? new SkillAdditionCallbackData();
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUI.BeginChangeCheck();
                callback.EventName = EditorGUILayout.TextField("Event Name", callback.EventName ?? string.Empty);
                if (EditorGUI.EndChangeCheck())
                    MarkDirty();

                callback.Actions ??= new List<SkillAdditionActionData>();
                DrawActions(row, callback, callbackIndex);
                if (GUILayout.Button("Delete Callback", GUILayout.Width(120f)))
                    removeCallback = callbackIndex;
                row.Callbacks[callbackIndex] = callback;
                EditorGUILayout.EndVertical();
            }

            if (removeCallback >= 0)
            {
                row.Callbacks.RemoveAt(removeCallback);
                MarkDirty();
            }
        }

        private void DrawActions(SkillAdditionData row, SkillAdditionCallbackData callback, int callbackIndex)
        {
            EditorGUILayout.LabelField($"Actions ({callback.Actions.Count})", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Modify Skill", GUILayout.Width(110f)))
            {
                callback.Actions.Add(new ModifyCurrentSkillAdditionActionData());
                MarkDirty();
            }
            if (GUILayout.Button("+ Set Value", GUILayout.Width(100f)))
            {
                callback.Actions.Add(new SetSourceValueSkillAdditionActionData());
                MarkDirty();
            }
            if (GUILayout.Button("+ Execute Effects", GUILayout.Width(130f)))
            {
                callback.Actions.Add(new ExecuteEffectsSkillAdditionActionData());
                MarkDirty();
            }
            if (GUILayout.Button("+ Replay", GUILayout.Width(80f)))
            {
                callback.Actions.Add(new ReplayCurrentSkillAdditionActionData());
                MarkDirty();
            }
            EditorGUILayout.EndHorizontal();

            int removeAction = -1;
            for (int actionIndex = 0; actionIndex < callback.Actions.Count; actionIndex++)
            {
                SkillAdditionActionData action = callback.Actions[actionIndex];
                if (action == null)
                    continue;

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField(action.GetType().Name, EditorStyles.boldLabel);
                switch (action)
                {
                    case ModifyCurrentSkillAdditionActionData modify:
                        modify.Modifiers ??= new List<SkillAdditionModifierExpressionData>();
                        DrawModifiers(modify.Modifiers);
                        break;
                    case SetSourceValueSkillAdditionActionData setValue:
                        DrawSetSourceValue(setValue);
                        break;
                    case ExecuteEffectsSkillAdditionActionData executeEffects:
                        executeEffects.Effects ??= Array.Empty<EffectData>();
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"{executeEffects.Effects.Length} effect(s)");
                        if (GUILayout.Button("Edit Effect Graph", GUILayout.Width(150f)))
                            EffectGraphWindow.Open(CreateEffectBinding(row, callbackIndex, actionIndex, executeEffects, MarkDirty));
                        EditorGUILayout.EndHorizontal();
                        break;
                }

                if (GUILayout.Button("Delete Action", GUILayout.Width(100f)))
                    removeAction = actionIndex;
                EditorGUILayout.EndVertical();
            }

            if (removeAction >= 0)
            {
                callback.Actions.RemoveAt(removeAction);
                MarkDirty();
            }
        }

        private void DrawSetSourceValue(SetSourceValueSkillAdditionActionData setValue)
        {
            _sourceSchema ??= UnitSourceSchemaFactory.CreateForAllSources();
            List<UnitSourceSetSchemaEntry> setters = new(_sourceSchema.Sets);
            if (setters.Count == 0)
            {
                EditorGUILayout.HelpBox("No unit source setters are available.", MessageType.Warning);
                return;
            }

            int selectedIndex = setters.FindIndex(setter =>
                string.Equals(setter.Key, setValue.SetterKey, StringComparison.Ordinal));
            if (selectedIndex < 0)
            {
                EditorGUILayout.HelpBox(
                    $"Setter '{setValue.SetterKey}' is not available in the current source schema.",
                    MessageType.Warning);
                return;
            }

            string[] options = setters.ConvertAll(setter => setter.Key).ToArray();
            EditorGUI.BeginChangeCheck();
            selectedIndex = EditorGUILayout.Popup("Setter", selectedIndex, options);
            UnitSourceSetSchemaEntry setter = setters[selectedIndex];
            setValue.SetterKey = setter.Key;
            if (setter.RequiresKey)
                setValue.Key = EditorGUILayout.TextField("Key", setValue.Key ?? string.Empty);
            else
                setValue.Key = string.Empty;

            setValue.Values ??= new List<ValueExpression>();
            while (setValue.Values.Count < setter.Parameters.Count)
            {
                setValue.Values.Add(new ValueExpression
                {
                    Literal = StateScriptValueExpressionDrawer.CreateDefaultLiteral(
                        setter.Parameters[setValue.Values.Count].Category),
                });
            }

            if (setValue.Values.Count > setter.Parameters.Count)
                setValue.Values.RemoveRange(setter.Parameters.Count, setValue.Values.Count - setter.Parameters.Count);

            for (int index = 0; index < setter.Parameters.Count; index++)
            {
                ComparatorParameterDefinition parameter = setter.Parameters[index];
                setValue.Values[index] ??= new ValueExpression
                {
                    Literal = StateScriptValueExpressionDrawer.CreateDefaultLiteral(parameter.Category),
                };
                EditorGUILayout.LabelField($"{parameter.Name} ({parameter.Category})", EditorStyles.miniBoldLabel);
                StateScriptValueExpressionDrawer.Draw(
                    setValue.Values[index],
                    parameter.Category,
                    _sourceSchema,
                    MarkDirty);
            }

            if (EditorGUI.EndChangeCheck())
                MarkDirty();
        }

        private void DrawModifiers(List<SkillAdditionModifierExpressionData> modifiers)
        {
            modifiers ??= new List<SkillAdditionModifierExpressionData>();
            if (GUILayout.Button("Add Modifier", GUILayout.Width(100f)))
            {
                modifiers.Add(new SkillAdditionModifierExpressionData());
                MarkDirty();
            }

            int removeAt = -1;
            for (int index = 0; index < modifiers.Count; index++)
            {
                SkillAdditionModifierExpressionData modifier = modifiers[index];
                modifier.Factor ??= new ValueExpression { Literal = UnitValue.FromFloat(0f) };
                modifier.Bonus ??= new ValueExpression { Literal = UnitValue.FromFloat(0f) };
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginHorizontal();
                modifier.Channel = (SkillModifierChannel)EditorGUILayout.EnumPopup(modifier.Channel);
                float factor = EditorGUILayout.FloatField("Factor", GetLiteralNumber(modifier.Factor), GUILayout.MinWidth(120f));
                float bonus = EditorGUILayout.FloatField("Bonus", GetLiteralNumber(modifier.Bonus), GUILayout.MinWidth(120f));
                if (GUILayout.Button("Delete", GUILayout.Width(60f)))
                    removeAt = index;
                EditorGUILayout.EndHorizontal();
                if (EditorGUI.EndChangeCheck())
                {
                    modifier.Factor = CreateLiteralNumberExpression(factor);
                    modifier.Bonus = CreateLiteralNumberExpression(bonus);
                    modifiers[index] = modifier;
                    MarkDirty();
                }
            }

            if (removeAt >= 0)
            {
                modifiers.RemoveAt(removeAt);
                MarkDirty();
            }
        }

        private static float GetLiteralNumber(ValueExpression expression)
        {
            return expression?.Kind == ValueExpressionKind.Literal && expression.Literal.TryGetNumber(out float value)
                ? value
                : 0f;
        }

        private static ValueExpression CreateLiteralNumberExpression(float value)
        {
            return new ValueExpression
            {
                Kind = ValueExpressionKind.Literal,
                Literal = UnitValue.FromFloat(value),
            };
        }

        internal static EffectGraphBinding CreateEffectBinding(
            SkillAdditionData row,
            int callbackIndex,
            int actionIndex,
            ExecuteEffectsSkillAdditionActionData action,
            Action markDirty)
        {
            return new EffectGraphBinding(
                $"SkillAddition:{row?.Id ?? -1}:Callbacks[{callbackIndex}].Actions[{actionIndex}].Effects",
                $"Skill Addition [{row?.Id ?? -1}] Callback {callbackIndex} Execute Effects {actionIndex}",
                () => action?.Effects ?? Array.Empty<EffectData>(),
                effects => { if (action != null) action.Effects = effects; },
                markDirty ?? (() => { }));
        }

        private void Load()
        {
            _rows.Clear();
            _selectedIndex = -1;
            _isDirty = false;
            try
            {
                if (File.Exists(DataPath))
                {
                    TableWrapper wrapper = JsonConvert.DeserializeObject<TableWrapper>(DataFileUtility.ReadJsonText(DataPath), s_jsonSettings);
                    if (wrapper?.Rows != null)
                        _rows.AddRange(wrapper.Rows);
                }

                NormalizeIds();
                _status = $"Loaded {_rows.Count} row(s).";
            }
            catch (Exception exception)
            {
                _status = $"Load failed: {exception.Message}";
                Debug.LogException(exception);
            }
        }

        private void Save()
        {
            try
            {
                NormalizeIds();
                string directory = Path.GetDirectoryName(DataPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                DataFileUtility.WriteJsonText(DataPath, JsonConvert.SerializeObject(new TableWrapper { Rows = _rows }, s_jsonSettings));
                AssetDatabase.Refresh();
                _isDirty = false;
                _status = $"Saved {_rows.Count} row(s).";
            }
            catch (Exception exception)
            {
                _status = $"Save failed: {exception.Message}";
                Debug.LogException(exception);
            }
        }

        private void AddRow()
        {
            _rows.Add(new SkillAdditionData
            {
                Id = _rows.Count,
                NameKey = $"skill_addition.new_{_rows.Count}.name",
                Callbacks = new List<SkillAdditionCallbackData>(),
            });
            Select(_rows.Count - 1);
            MarkDirty();
        }

        private void DuplicateSelectedRow()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
                return;

            string json = JsonConvert.SerializeObject(_rows[_selectedIndex], s_jsonSettings);
            SkillAdditionData copy = JsonConvert.DeserializeObject<SkillAdditionData>(json, s_jsonSettings);
            if (copy == null)
                return;

            _rows.Add(copy);
            NormalizeIds();
            Select(_rows.Count - 1);
            MarkDirty();
        }

        private void DeleteSelectedRow()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
                return;

            _rows.RemoveAt(_selectedIndex);
            NormalizeIds();
            _selectedIndex = -1;
            MarkDirty();
        }

        private void Select(int index)
        {
            if (index < 0 || index >= _rows.Count)
                return;

            _selectedIndex = index;
        }

        private void MarkDirty()
        {
            _isDirty = true;
        }

        private void NormalizeIds()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i] ??= new SkillAdditionData();
                _rows[i].Id = i;
                _rows[i].Callbacks ??= new List<SkillAdditionCallbackData>();
            }
        }
    }
}
