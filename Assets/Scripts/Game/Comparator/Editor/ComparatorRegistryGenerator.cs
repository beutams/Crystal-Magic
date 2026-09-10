using System;
using System.Collections.Generic;
using System.Text;
using CrystalMagic.Editor.Unit;
using CrystalMagic.Editor;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor
{
    public static class ComparatorRegistryGenerator
    {
        private const string OutputPath = "Assets/Scripts/Game/Comparator/ComparatorRegistry.cs";

        [MenuItem("Tools/Registry/Comparator")]
        public static void Generate()
        {
            List<FactoryRegistryGeneratorUtility.MappedType> compareTypes =
                FactoryRegistryGeneratorUtility.CollectMappedTypes(typeof(ICompareType), subclassOnly: false);
            List<FactoryRegistryGeneratorUtility.MappedType> operations =
                FactoryRegistryGeneratorUtility.CollectMappedTypes(typeof(IValueOperation), subclassOnly: false);

            string content = BuildRegistry(compareTypes, operations);
            RegistryGeneratorUtility.WriteFile(OutputPath, content);
            AssetDatabase.Refresh();

            Debug.Log($"[ComparatorRegistryGenerator] Generated {OutputPath}.");
        }

        private static string BuildRegistry(
            List<FactoryRegistryGeneratorUtility.MappedType> compareTypes,
            List<FactoryRegistryGeneratorUtility.MappedType> operations)
        {
            StringBuilder sb = new();
            sb.AppendLine("// AUTO-GENERATED - DO NOT EDIT MANUALLY");
            sb.AppendLine("// Use menu: Tools/Registry/Comparator");
            sb.AppendLine();
            sb.AppendLine("public static class ComparatorRegistry");
            sb.AppendLine("{");
            sb.AppendLine("    public static void RegisterAll(ComparatorFactory factory)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (factory == null)");
            sb.AppendLine("            return;");

            AppendFactories(sb, "RegisterCompareType", compareTypes);
            AppendFactories(sb, "RegisterValueOperation", operations);

            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void AppendFactories(
            StringBuilder sb,
            string registerMethod,
            List<FactoryRegistryGeneratorUtility.MappedType> types)
        {
            if (types.Count == 0)
                return;

            sb.AppendLine();
            for (int i = 0; i < types.Count; i++)
            {
                FactoryRegistryGeneratorUtility.MappedType type = types[i];
                sb.AppendLine($"        factory.{registerMethod}({FactoryRegistryGeneratorUtility.Literal(type.Mapping.Key)}, static () => new {FactoryRegistryGeneratorUtility.TypeReference(type.Type)}());");
            }
        }
    }

    public static class ConditionListEditor
    {
        public static bool Draw(
            List<ConditionConfig> conditions,
            string key,
            UnitSourceSchema sourceSchema,
            Action onChanged = null)
        {
            if (conditions == null)
                return false;

            bool changed = false;
            string foldoutKey = key ?? string.Empty;
            bool isOpen = SessionState.GetBool(foldoutKey, true);
            isOpen = EditorGUILayout.Foldout(isOpen, $"Conditions ({conditions.Count})", true);
            SessionState.SetBool(foldoutKey, isOpen);
            if (!isOpen)
                return false;

            EditorGUI.indentLevel++;
            if (GUILayout.Button("+ Add Condition", GUILayout.Width(120f)))
            {
                conditions.Add(new ConditionConfig
                {
                    ConditionType = ConditionType.Necessary,
                    CompareType = "IsTrue",
                    Inputs = new List<ValueExpression>
                    {
                        new()
                        {
                            Kind = ValueExpressionKind.Literal,
                            Literal = UnitValue.FromBool(true),
                        },
                    },
                });
                changed = true;
            }

            int removeAt = -1;
            for (int index = 0; index < conditions.Count; index++)
            {
                ConditionConfig condition = conditions[index] ?? new ConditionConfig();
                if (conditions[index] == null)
                {
                    conditions[index] = condition;
                    changed = true;
                }

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                condition.ConditionType = (ConditionType)EditorGUILayout.EnumPopup("Type", condition.ConditionType);
                StateScriptValueExpressionDrawer.DrawCondition(condition, sourceSchema, onChanged);
                if (GUILayout.Button("Remove", GUILayout.Width(80f)))
                    removeAt = index;
                EditorGUILayout.EndVertical();
                if (EditorGUI.EndChangeCheck())
                    changed = true;
            }

            if (removeAt >= 0)
            {
                conditions.RemoveAt(removeAt);
                changed = true;
            }

            EditorGUI.indentLevel--;
            if (changed)
                onChanged?.Invoke();

            return changed;
        }
    }
}
