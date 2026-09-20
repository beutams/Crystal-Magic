using System;
using System.Collections.Generic;
using System.Text;
using CrystalMagic.Editor;
using CrystalMagic.Game.Data;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Unit
{
    public static class BehaviorTreeRegistryGenerator
    {
        private const string OutputPath = "Assets/Scripts/Game/BehaviorTree/BehaviorTreeRegistry.cs";

        [MenuItem("Tools/Registry/Behavior Tree")]
        public static void Generate()
        {
            List<FactoryRegistryGeneratorUtility.MappedType> nodeDataTypes =
                FactoryRegistryGeneratorUtility.CollectMappedTypes(typeof(BehaviorNodeData), subclassOnly: true);
            string content = BuildRegistry(nodeDataTypes);
            RegistryGeneratorUtility.WriteFile(OutputPath, content);
            AssetDatabase.Refresh();

            Debug.Log($"[BehaviorTreeRegistryGenerator] Generated {OutputPath}.");
        }

        private static string BuildRegistry(
            List<FactoryRegistryGeneratorUtility.MappedType> nodeDataTypes)
        {
            StringBuilder sb = new();
            sb.AppendLine("// AUTO-GENERATED - DO NOT EDIT MANUALLY");
            sb.AppendLine("// Use menu: Tools/Registry/Behavior Tree");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using CrystalMagic.Game.Data;");
            sb.AppendLine();
            sb.AppendLine("public static class BehaviorTreeRegistry");
            sb.AppendLine("{");

            AppendMetadata(sb, nodeDataTypes);
            AppendNodeDataRegistration(sb, nodeDataTypes);

            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void AppendMetadata(StringBuilder sb, List<FactoryRegistryGeneratorUtility.MappedType> nodeDataTypes)
        {
            string defaultKey = nodeDataTypes.Count > 0 ? nodeDataTypes[0].Mapping.Key : string.Empty;

            sb.AppendLine("    private static readonly string[] s_behaviorNodeDataTypeOrder =");
            sb.AppendLine("    {");
            for (int i = 0; i < nodeDataTypes.Count; i++)
                sb.AppendLine($"        {FactoryRegistryGeneratorUtility.Literal(nodeDataTypes[i].Mapping.Key)},");
            sb.AppendLine("    };");
            sb.AppendLine();

            sb.AppendLine("    private static readonly Dictionary<string, Type> s_behaviorNodeDataTypes = new(StringComparer.Ordinal)");
            sb.AppendLine("    {");
            for (int i = 0; i < nodeDataTypes.Count; i++)
            {
                var mapped = nodeDataTypes[i];
                sb.AppendLine($"        {{ {FactoryRegistryGeneratorUtility.Literal(mapped.Mapping.Key)}, typeof({FactoryRegistryGeneratorUtility.TypeReference(mapped.Type)}) }},");
            }
            sb.AppendLine("    };");
            sb.AppendLine();

            sb.AppendLine("    private static readonly Dictionary<Type, string> s_behaviorNodeDataKeys = new()");
            sb.AppendLine("    {");
            for (int i = 0; i < nodeDataTypes.Count; i++)
            {
                var mapped = nodeDataTypes[i];
                sb.AppendLine($"        {{ typeof({FactoryRegistryGeneratorUtility.TypeReference(mapped.Type)}), {FactoryRegistryGeneratorUtility.Literal(mapped.Mapping.Key)} }},");
            }
            sb.AppendLine("    };");
            sb.AppendLine();

            sb.AppendLine("    private static readonly Dictionary<string, string> s_behaviorNodeDataDisplayNames = new(StringComparer.Ordinal)");
            sb.AppendLine("    {");
            for (int i = 0; i < nodeDataTypes.Count; i++)
            {
                var mapped = nodeDataTypes[i];
                sb.AppendLine($"        {{ {FactoryRegistryGeneratorUtility.Literal(mapped.Mapping.Key)}, {FactoryRegistryGeneratorUtility.Literal(FactoryRegistryGeneratorUtility.DisplayName(mapped))} }},");
            }
            sb.AppendLine("    };");
            sb.AppendLine();

            sb.AppendLine("    private static readonly FactoryTypeInfo[] s_behaviorNodeDataTypeInfos =");
            sb.AppendLine("    {");
            for (int i = 0; i < nodeDataTypes.Count; i++)
            {
                var mapped = nodeDataTypes[i];
                sb.AppendLine($"        new({FactoryRegistryGeneratorUtility.Literal(mapped.Mapping.Key)}, {FactoryRegistryGeneratorUtility.Literal(FactoryRegistryGeneratorUtility.DisplayName(mapped))}, typeof({FactoryRegistryGeneratorUtility.TypeReference(mapped.Type)}), {mapped.Mapping.Order}),");
            }
            sb.AppendLine("    };");
            sb.AppendLine();

            sb.AppendLine($"    public static string DefaultBehaviorNodeDataKey => {FactoryRegistryGeneratorUtility.Literal(defaultKey)};");
            sb.AppendLine();
            sb.AppendLine("    public static IReadOnlyList<string> BehaviorNodeDataTypeOrder => s_behaviorNodeDataTypeOrder;");
            sb.AppendLine();
            sb.AppendLine("    public static IReadOnlyList<FactoryTypeInfo> BehaviorNodeDataTypeInfos => s_behaviorNodeDataTypeInfos;");
            sb.AppendLine();
            sb.AppendLine("    public static bool ContainsBehaviorNodeDataKey(string key)");
            sb.AppendLine("    {");
            sb.AppendLine("        return s_behaviorNodeDataTypes.ContainsKey(key ?? string.Empty);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public static bool TryGetBehaviorNodeDataType(string key, out Type type)");
            sb.AppendLine("    {");
            sb.AppendLine("        return s_behaviorNodeDataTypes.TryGetValue(key ?? string.Empty, out type);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public static bool TryGetBehaviorNodeDataKey(Type type, out string key)");
            sb.AppendLine("    {");
            sb.AppendLine("        return s_behaviorNodeDataKeys.TryGetValue(type, out key);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public static string GetBehaviorNodeDataDisplayName(string key)");
            sb.AppendLine("    {");
            sb.AppendLine("        return s_behaviorNodeDataDisplayNames.TryGetValue(key ?? string.Empty, out string displayName)");
            sb.AppendLine("            ? displayName");
            sb.AppendLine("            : key ?? \"Unknown\";");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        private static void AppendNodeDataRegistration(StringBuilder sb, List<FactoryRegistryGeneratorUtility.MappedType> nodeDataTypes)
        {
            sb.AppendLine("    public static void RegisterAll(BehaviorNodeDataFactory factory)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (factory == null)");
            sb.AppendLine("            return;");
            sb.AppendLine();
            for (int i = 0; i < nodeDataTypes.Count; i++)
            {
                var mapped = nodeDataTypes[i];
                sb.AppendLine($"        factory.Register({FactoryRegistryGeneratorUtility.Literal(mapped.Mapping.Key)}, static () => new {FactoryRegistryGeneratorUtility.TypeReference(mapped.Type)}());");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
        }

    }
}
