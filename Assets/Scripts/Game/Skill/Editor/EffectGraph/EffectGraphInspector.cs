using System;
using System.Collections.Generic;
using System.Reflection;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.EffectGraph
{
    internal static class EffectGraphInspector
    {
        public static bool DrawEffect(EffectGraphModel model, EffectData effect)
        {
            if (effect == null)
                return false;

            bool changed = false;
            EditorGUILayout.LabelField(EffectGraphTypeRegistry.GetDisplayName(effect), EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);
            effect.Conditions ??= new List<ConditionConfig>();
            changed |= DrawConditions(effect.Conditions);

            FieldInfo[] fields = effect.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            for (int index = 0; index < fields.Length; index++)
            {
                FieldInfo field = fields[index];
                if (field.IsStatic)
                    continue;

                if (model.IsNestedEffectArrayField(field))
                {
                    EditorGUILayout.LabelField(EditorLabelUtility.GetLabel(field), "Edited by its connected container", EditorStyles.miniLabel);
                    continue;
                }

                object oldValue = field.GetValue(effect);
                object newValue = DrawValue(field.FieldType, EditorLabelUtility.GetLabel(field), oldValue);
                if (Equals(oldValue, newValue))
                    continue;

                field.SetValue(effect, newValue);
                changed = true;
            }

            return changed;
        }

        private static object DrawValue(Type type, string label, object value)
        {
            if (type == typeof(int))
                return EditorGUILayout.IntField(label, value is int number ? number : 0);
            if (type == typeof(float))
                return EditorGUILayout.FloatField(label, value is float number ? number : 0f);
            if (type == typeof(bool))
                return EditorGUILayout.Toggle(label, value is bool flag && flag);
            if (type == typeof(string))
                return EditorGUILayout.TextField(label, value as string ?? string.Empty);
            if (type == typeof(Vector3))
                return EditorGUILayout.Vector3Field(label, value is Vector3 vector ? vector : Vector3.zero);
            if (type == typeof(LayerMask))
            {
                LayerMask mask = value is LayerMask layerMask ? layerMask : default;
                return new LayerMask { value = EditorGUILayout.IntField($"{label} (bitmask)", mask.value) };
            }
            if (type.IsEnum)
                return EditorGUILayout.EnumPopup(label, value as Enum ?? (Enum)Activator.CreateInstance(type));
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return EditorGUILayout.ObjectField(label, value as UnityEngine.Object, type, false);
            if (type == typeof(List<SkillModifierEntry>))
                return DrawModifiers(label, value as List<SkillModifierEntry> ?? new List<SkillModifierEntry>());

            EditorGUILayout.LabelField(label, value?.ToString() ?? "(None)");
            return value;
        }

        private static bool DrawConditions(List<ConditionConfig> conditions)
        {
            conditions ??= new List<ConditionConfig>();
            bool changed = false;
            EditorGUILayout.LabelField($"Conditions ({conditions.Count})", EditorStyles.boldLabel);

            int removeAt = -1;
            for (int index = 0; index < conditions.Count; index++)
            {
                ConditionConfig condition = conditions[index] ?? new ConditionConfig();
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                condition.ConditionType = (ConditionType)EditorGUILayout.EnumPopup("Type", condition.ConditionType);
                condition.SourceType = EditorGUILayout.TextField("Source", condition.SourceType);
                condition.CompareType = EditorGUILayout.TextField("Compare", condition.CompareType);
                condition.SourceParam = EditorGUILayout.IntField("Source Param", condition.SourceParam);
                condition.CompareValue = EditorGUILayout.FloatField("Value", condition.CompareValue);
                if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                    removeAt = index;
                EditorGUILayout.EndVertical();
                if (EditorGUI.EndChangeCheck())
                {
                    conditions[index] = condition;
                    changed = true;
                }
            }

            if (removeAt >= 0)
            {
                conditions.RemoveAt(removeAt);
                changed = true;
            }

            if (GUILayout.Button("Add Condition", GUILayout.Width(100f)))
            {
                conditions.Add(new ConditionConfig { SourceParam = -1, ConditionType = ConditionType.Necessary });
                changed = true;
            }

            return changed;
        }

        private static List<SkillModifierEntry> DrawModifiers(string label, List<SkillModifierEntry> modifiers)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            int removeAt = -1;
            for (int index = 0; index < modifiers.Count; index++)
            {
                SkillModifierEntry entry = modifiers[index];
                EditorGUILayout.BeginHorizontal();
                entry.Channel = (SkillModifierChannel)EditorGUILayout.EnumPopup(entry.Channel);
                entry.Factor = EditorGUILayout.FloatField(entry.Factor);
                entry.Bonus = EditorGUILayout.FloatField(entry.Bonus);
                if (GUILayout.Button("×", GUILayout.Width(24f)))
                    removeAt = index;
                EditorGUILayout.EndHorizontal();
                modifiers[index] = entry;
            }

            if (removeAt >= 0)
                modifiers.RemoveAt(removeAt);
            if (GUILayout.Button("Add Modifier", GUILayout.Width(100f)))
                modifiers.Add(new SkillModifierEntry());

            return modifiers;
        }
    }
}
