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
                DrawSetValue(setValue, sourceSchema);
                if (EditorGUI.EndChangeCheck())
                    onChanged?.Invoke();
                return;
            }

            if (node is TimerStateScriptNodeData timer)
            {
                EditorGUI.BeginChangeCheck();
                timer.DurationSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Duration Seconds", timer.DurationSeconds));
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
                compare.Conditions ??= new List<ConditionConfig>();
                DrawComparatorConditions(compare.Conditions, sourceSchema, onChanged);
                return;
            }

            if (node is MonitorStateScriptNodeData monitor)
            {
                monitor.Conditions ??= new List<ConditionConfig>();
                DrawComparatorConditions(monitor.Conditions, sourceSchema, onChanged);
                return;
            }

            EditorGUILayout.HelpBox("This first version only provides the StateScript structure. Concrete State, Bool, and Action nodes will add their own configuration here.", MessageType.Info);
        }

        private static void DrawSetValue(SetValueStateScriptNodeData setValue, UnitSourceSchema sourceSchema)
        {
            List<UnitSourceSetSchemaEntry> entries = (sourceSchema?.Sets ?? Enumerable.Empty<UnitSourceSetSchemaEntry>())
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .ToList();
            if (entries.Count == 0)
            {
                EditorGUILayout.HelpBox("No setter accessors are registered.", MessageType.Warning);
                return;
            }

            string[] options = new string[entries.Count + 1];
            options[0] = "(Select setter)";
            for (int i = 0; i < entries.Count; i++)
                options[i + 1] = entries[i].Key;

            int selectedIndex = entries.FindIndex(entry => string.Equals(entry.Key, setValue.SetterKey, StringComparison.Ordinal)) + 1;
            selectedIndex = EditorGUILayout.Popup("Setter", Mathf.Max(0, selectedIndex), options);
            if (selectedIndex <= 0)
            {
                if (!string.IsNullOrWhiteSpace(setValue.SetterKey))
                    EditorGUILayout.HelpBox($"'{setValue.SetterKey}' is not writable by this unit.", MessageType.Warning);
                return;
            }

            UnitSourceSetSchemaEntry setter = entries[selectedIndex - 1];
            setValue.SetterKey = setter.Key;
            setValue.Arguments ??= new List<UnitValue>();
            while (setValue.Arguments.Count < setter.Parameters.Count)
            {
                setValue.Arguments.Add(StateScriptValueExpressionDrawer.CreateDefaultLiteral(
                    setter.Parameters[setValue.Arguments.Count].Category));
            }

            if (setValue.Arguments.Count > setter.Parameters.Count)
                setValue.Arguments.RemoveRange(setter.Parameters.Count, setValue.Arguments.Count - setter.Parameters.Count);

            for (int i = 0; i < setter.Parameters.Count; i++)
            {
                ComparatorParameterDefinition parameter = setter.Parameters[i];
                EditorGUILayout.LabelField($"{parameter.Name} ({parameter.Category})", EditorStyles.miniBoldLabel);
                setValue.Arguments[i] = StateScriptValueExpressionDrawer.DrawLiteralValue(
                    setValue.Arguments[i], parameter.Category);
            }
        }

        private static void DrawComparatorConditions(
            List<ConditionConfig> conditions,
            UnitSourceSchema sourceSchema,
            Action onChanged)
        {
            EditorGUI.BeginChangeCheck();
            StateScriptValueExpressionDrawer.DrawConditionList(conditions, sourceSchema);
            if (EditorGUI.EndChangeCheck())
                onChanged?.Invoke();
        }

    }
}
