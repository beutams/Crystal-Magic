using System;
using System.Collections.Generic;
using System.Reflection;
using CrystalMagic.Editor;
using CrystalMagic.Editor.Skill;
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
            changed |= ConditionListEditor.Draw(
                effect.Conditions,
                $"EffectGraph.{effect.GetType().FullName}.Conditions",
                EffectConditionSourceSchema.Get());

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

                if (field.FieldType == typeof(List<ConditionConfig>))
                {
                    List<ConditionConfig> conditions = field.GetValue(effect) as List<ConditionConfig> ?? new List<ConditionConfig>();
                    if (field.GetValue(effect) == null)
                    {
                        field.SetValue(effect, conditions);
                        changed = true;
                    }

                    EditorGUILayout.LabelField(EditorLabelUtility.GetLabel(field), EditorStyles.boldLabel);
                    changed |= ConditionListEditor.Draw(
                        conditions,
                        $"EffectGraph.{effect.GetType().FullName}.{field.Name}",
                        EffectConditionSourceSchema.Get());
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
