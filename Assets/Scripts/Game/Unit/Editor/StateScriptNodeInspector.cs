using System;
using System.Collections.Generic;
using System.Linq;
using CrystalMagic.Game.Data;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Unit
{
    public static class StateScriptNodeInspector
    {
        public static void Draw(StateScriptNodeData node, UnitSourceSchema sourceSchema, Action onChanged)
        {
            if (node == null)
            {
                EditorGUILayout.HelpBox("Select a node to inspect its data.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Type", StateScriptNodeDataRegistry.GetDisplayName(node.Type));
            EditorGUILayout.SelectableLabel(node.Guid ?? string.Empty, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.Space(6f);
            if (node is SetValueStateScriptNodeData setValue)
            {
                EditorGUI.BeginChangeCheck();
                DrawSetValue(setValue, sourceSchema, onChanged);
                if (EditorGUI.EndChangeCheck())
                    onChanged?.Invoke();
                return;
            }

            if (node is RequestSkillActionNodeData requestSkill)
            {
                EditorGUI.BeginChangeCheck();
                DrawRequestSkill(requestSkill, sourceSchema, onChanged);
                if (EditorGUI.EndChangeCheck())
                    onChanged?.Invoke();
                return;
            }

            if (node is PublishGameEventStateScriptNodeData publishGameEvent)
            {
                EditorGUI.BeginChangeCheck();
                DrawPublishGameEvent(publishGameEvent, sourceSchema, onChanged);
                if (EditorGUI.EndChangeCheck())
                    onChanged?.Invoke();
                return;
            }

            if (node is TimerStateScriptNodeData timer)
            {
                EditorGUI.BeginChangeCheck();
                timer.Duration ??= TimerStateScriptNodeData.CreateDefaultDurationExpression();
                EditorGUILayout.LabelField("Duration (Seconds)", EditorStyles.miniBoldLabel);
                StateScriptValueExpressionDrawer.Draw(timer.Duration, UnitValueCategory.Number, sourceSchema, onChanged);
                if (EditorGUI.EndChangeCheck())
                    onChanged?.Invoke();
                return;
            }

            if (node is NumberMonitorStateScriptNodeData numberMonitor)
            {
                EditorGUI.BeginChangeCheck();
                numberMonitor.Value ??= NumberMonitorStateScriptNodeData.CreateDefaultValueExpression();
                EditorGUILayout.LabelField("Observed Value (Number)", EditorStyles.miniBoldLabel);
                StateScriptValueExpressionDrawer.Draw(numberMonitor.Value, UnitValueCategory.Number, sourceSchema, onChanged);
                if (EditorGUI.EndChangeCheck())
                    onChanged?.Invoke();
                return;
            }

            if (node is KeepStateScriptNodeData keep)
            {
                EditorGUI.BeginChangeCheck();
                keep.DurationSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Duration Seconds", keep.DurationSeconds));
                if (EditorGUI.EndChangeCheck())
                    onChanged?.Invoke();
                EditorGUILayout.HelpBox("Keep requires a Start pulse every frame until its duration completes. Missing Start stops the State.", MessageType.Info);
                return;
            }

            if (node is CompareStateScriptNodeData compare)
            {
                compare.Condition ??= new ConditionConfig();
                DrawComparatorCondition(compare.Condition, sourceSchema, onChanged);
                return;
            }

            if (node is MonitorStateScriptNodeData monitor)
            {
                monitor.Condition ??= new ConditionConfig();
                DrawComparatorCondition(monitor.Condition, sourceSchema, onChanged);
                return;
            }

            EditorGUILayout.HelpBox("This first version only provides the StateScript structure. Concrete State, Bool, and Action nodes will add their own configuration here.", MessageType.Info);
        }

        private static void DrawSetValue(
            SetValueStateScriptNodeData setValue,
            UnitSourceSchema sourceSchema,
            Action onChanged)
        {
            List<UnitSourceSetSchemaEntry> entries = (sourceSchema?.Sets ?? Enumerable.Empty<UnitSourceSetSchemaEntry>())
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .ToList();
            if (entries.Count == 0)
            {
                EditorGUILayout.HelpBox("No setter accessors are registered.", MessageType.Warning);
                return;
            }

            StateScriptAccessorDropdown.Draw(
                "Setter",
                setValue.SetterKey,
                entries.Select(entry => entry.Key),
                "(Select setter)",
                selectedKey =>
                {
                    if (string.Equals(setValue.SetterKey, selectedKey, StringComparison.Ordinal))
                        return;

                    setValue.SetterKey = selectedKey;
                    GUI.changed = true;
                    onChanged?.Invoke();
                });
            if (string.IsNullOrWhiteSpace(setValue.SetterKey))
            {
                return;
            }

            int setterIndex = entries.FindIndex(entry =>
                string.Equals(entry.Key, setValue.SetterKey, StringComparison.Ordinal));
            if (setterIndex < 0)
            {
                EditorGUILayout.HelpBox($"'{setValue.SetterKey}' is not writable by this unit.", MessageType.Warning);
                return;
            }

            UnitSourceSetSchemaEntry setter = entries[setterIndex];
            setValue.SetterKey = setter.Key;
            if (setter.RequiresKey)
            {
                setValue.Key = EditorGUILayout.TextField("Key", setValue.Key ?? string.Empty);
                if (string.IsNullOrWhiteSpace(setValue.Key))
                    EditorGUILayout.HelpBox($"Setter '{setter.Key}' requires a key.", MessageType.Warning);
            }

            List<ValueExpression> values = setValue.GetOrCreateValues(setter.Parameters.Count);
            if (values.Count != setter.Parameters.Count)
            {
                EditorGUILayout.HelpBox(
                    $"Setter '{setter.Key}' requires {setter.Parameters.Count} inputs, but this node has {values.Count}.",
                    MessageType.Error);
                return;
            }

            for (int i = 0; i < setter.Parameters.Count; i++)
            {
                ComparatorParameterDefinition parameter = setter.Parameters[i];
                values[i] ??= new ValueExpression
                {
                    Literal = StateScriptValueExpressionDrawer.CreateDefaultLiteral(parameter.Category),
                };
                EditorGUILayout.LabelField($"{parameter.Name} ({parameter.Category})", EditorStyles.miniBoldLabel);
                StateScriptValueExpressionDrawer.Draw(values[i], parameter.Category, sourceSchema, onChanged);
            }
        }

        private static void DrawRequestSkill(
            RequestSkillActionNodeData requestSkill,
            UnitSourceSchema sourceSchema,
            Action onChanged)
        {
            requestSkill.SkillId ??= RequestSkillActionNodeData.CreateDefaultSkillIdExpression();
            EditorGUILayout.LabelField("Skill ID (Number)", EditorStyles.miniBoldLabel);
            StateScriptValueExpressionDrawer.Draw(requestSkill.SkillId, UnitValueCategory.Number, sourceSchema, onChanged);
        }

        private static void DrawPublishGameEvent(
            PublishGameEventStateScriptNodeData publishGameEvent,
            UnitSourceSchema sourceSchema,
            Action onChanged)
        {
            publishGameEvent.EventName = EditorGUILayout.TextField("Event Name", publishGameEvent.EventName ?? string.Empty);
            publishGameEvent.Reference ??= PublishGameEventStateScriptNodeData.CreateDefaultReferenceExpression();
            EditorGUILayout.LabelField("Reference", EditorStyles.miniBoldLabel);
            StateScriptValueExpressionDrawer.Draw(publishGameEvent.Reference, UnitValueCategory.Any, sourceSchema, onChanged);
        }

        private static void DrawComparatorCondition(
            ConditionConfig condition,
            UnitSourceSchema sourceSchema,
            Action onChanged)
        {
            EditorGUI.BeginChangeCheck();
            condition.ConditionType = ConditionType.Necessary;
            StateScriptValueExpressionDrawer.DrawCondition(condition, sourceSchema, onChanged);
            if (EditorGUI.EndChangeCheck())
                onChanged?.Invoke();
        }

    }
}
