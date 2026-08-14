using System;
using System.Collections.Generic;
using System.Text;
using CrystalMagic.Editor;
using CrystalMagic.Game.Data;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Unit
{
    public static class StateScriptRegistryGenerator
    {
        private const string OutputPath = "Assets/Scripts/Game/Unit/StateScript/StateScriptRegistry.cs";

        [MenuItem("Tools/Registry/State Script")]
        public static void Generate()
        {
            List<FactoryRegistryGeneratorUtility.MappedType> dataTypes =
                FactoryRegistryGeneratorUtility.CollectMappedTypes(typeof(StateScriptNodeData), subclassOnly: true);
            List<FactoryRegistryGeneratorUtility.MappedType> runtimeTypes =
                FactoryRegistryGeneratorUtility.CollectMappedTypes(typeof(StateScriptNode), subclassOnly: true);

            RegistryGeneratorUtility.WriteFile(OutputPath, BuildRegistry(dataTypes, runtimeTypes));
            AssetDatabase.Refresh();
            Debug.Log($"[StateScriptRegistryGenerator] Generated {OutputPath}.");
        }

        private static string BuildRegistry(
            List<FactoryRegistryGeneratorUtility.MappedType> dataTypes,
            List<FactoryRegistryGeneratorUtility.MappedType> runtimeTypes)
        {
            StringBuilder builder = new();
            builder.AppendLine("// AUTO-GENERATED - DO NOT EDIT MANUALLY");
            builder.AppendLine("// Use menu: Tools/Registry/State Script");
            builder.AppendLine();
            builder.AppendLine("using System;");
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine("using CrystalMagic.Game.Data;");
            builder.AppendLine();
            builder.AppendLine("public static class StateScriptRegistry");
            builder.AppendLine("{");

            AppendMetadata(builder, dataTypes);
            AppendDataRegistrations(builder, dataTypes);
            AppendRuntimeRegistrations(builder, dataTypes, runtimeTypes);

            builder.AppendLine("}");
            return builder.ToString();
        }

        private static void AppendMetadata(
            StringBuilder builder,
            List<FactoryRegistryGeneratorUtility.MappedType> dataTypes)
        {
            string defaultKey = dataTypes.Count > 0 ? dataTypes[0].Mapping.Key : string.Empty;
            builder.AppendLine("    private static readonly Dictionary<string, Type> s_nodeDataTypes = new(StringComparer.Ordinal)");
            builder.AppendLine("    {");
            for (int i = 0; i < dataTypes.Count; i++)
            {
                FactoryRegistryGeneratorUtility.MappedType mapped = dataTypes[i];
                builder.AppendLine($"        {{ {FactoryRegistryGeneratorUtility.Literal(mapped.Mapping.Key)}, typeof({FactoryRegistryGeneratorUtility.TypeReference(mapped.Type)}) }},");
            }
            builder.AppendLine("    };");
            builder.AppendLine();

            builder.AppendLine("    private static readonly Dictionary<Type, string> s_nodeDataKeys = new()");
            builder.AppendLine("    {");
            for (int i = 0; i < dataTypes.Count; i++)
            {
                FactoryRegistryGeneratorUtility.MappedType mapped = dataTypes[i];
                builder.AppendLine($"        {{ typeof({FactoryRegistryGeneratorUtility.TypeReference(mapped.Type)}), {FactoryRegistryGeneratorUtility.Literal(mapped.Mapping.Key)} }},");
            }
            builder.AppendLine("    };");
            builder.AppendLine();

            builder.AppendLine("    private static readonly Dictionary<string, string> s_nodeDataDisplayNames = new(StringComparer.Ordinal)");
            builder.AppendLine("    {");
            for (int i = 0; i < dataTypes.Count; i++)
            {
                FactoryRegistryGeneratorUtility.MappedType mapped = dataTypes[i];
                builder.AppendLine($"        {{ {FactoryRegistryGeneratorUtility.Literal(mapped.Mapping.Key)}, {FactoryRegistryGeneratorUtility.Literal(FactoryRegistryGeneratorUtility.DisplayName(mapped))} }},");
            }
            builder.AppendLine("    };");
            builder.AppendLine();

            builder.AppendLine("    private static readonly FactoryTypeInfo[] s_nodeDataTypeInfos =");
            builder.AppendLine("    {");
            for (int i = 0; i < dataTypes.Count; i++)
            {
                FactoryRegistryGeneratorUtility.MappedType mapped = dataTypes[i];
                builder.AppendLine($"        new({FactoryRegistryGeneratorUtility.Literal(mapped.Mapping.Key)}, {FactoryRegistryGeneratorUtility.Literal(FactoryRegistryGeneratorUtility.DisplayName(mapped))}, typeof({FactoryRegistryGeneratorUtility.TypeReference(mapped.Type)}), {mapped.Mapping.Order}),");
            }
            builder.AppendLine("    };");
            builder.AppendLine();

            builder.AppendLine($"    public static string DefaultNodeDataKey => {FactoryRegistryGeneratorUtility.Literal(defaultKey)};");
            builder.AppendLine("    public static IReadOnlyList<FactoryTypeInfo> NodeDataTypeInfos => s_nodeDataTypeInfos;");
            builder.AppendLine();
            builder.AppendLine("    public static bool ContainsNodeDataKey(string key) => s_nodeDataTypes.ContainsKey(key ?? string.Empty);");
            builder.AppendLine("    public static bool TryGetNodeDataType(string key, out Type type) => s_nodeDataTypes.TryGetValue(key ?? string.Empty, out type);");
            builder.AppendLine("    public static bool TryGetNodeDataKey(Type type, out string key) => s_nodeDataKeys.TryGetValue(type, out key);");
            builder.AppendLine("    public static string GetNodeDataDisplayName(string key) => s_nodeDataDisplayNames.TryGetValue(key ?? string.Empty, out string displayName) ? displayName : key ?? \"Unknown\";");
            builder.AppendLine();
        }

        private static void AppendDataRegistrations(
            StringBuilder builder,
            List<FactoryRegistryGeneratorUtility.MappedType> dataTypes)
        {
            builder.AppendLine("    public static void RegisterAll(StateScriptNodeDataFactory factory)");
            builder.AppendLine("    {");
            builder.AppendLine("        if (factory == null)");
            builder.AppendLine("            return;");
            builder.AppendLine();
            for (int i = 0; i < dataTypes.Count; i++)
            {
                FactoryRegistryGeneratorUtility.MappedType mapped = dataTypes[i];
                builder.AppendLine($"        factory.Register({FactoryRegistryGeneratorUtility.Literal(mapped.Mapping.Key)}, static () => new {FactoryRegistryGeneratorUtility.TypeReference(mapped.Type)}());");
            }
            builder.AppendLine("    }");
            builder.AppendLine();
        }

        private static void AppendRuntimeRegistrations(
            StringBuilder builder,
            List<FactoryRegistryGeneratorUtility.MappedType> dataTypes,
            List<FactoryRegistryGeneratorUtility.MappedType> runtimeTypes)
        {
            builder.AppendLine("    public static void RegisterAll(StateScriptNodeRuntimeFactory factory)");
            builder.AppendLine("    {");
            builder.AppendLine("        if (factory == null)");
            builder.AppendLine("            return;");
            builder.AppendLine();
            for (int i = 0; i < dataTypes.Count; i++)
            {
                FactoryRegistryGeneratorUtility.MappedType dataType = dataTypes[i];
                FactoryRegistryGeneratorUtility.MappedType runtimeType = FindByKey(dataType.Mapping.Key, runtimeTypes);
                if (runtimeType == null)
                {
                    Debug.LogWarning($"[StateScriptRegistryGenerator] Missing runtime for {dataType.Type.Name}.");
                    continue;
                }

                string dataReference = FactoryRegistryGeneratorUtility.TypeReference(dataType.Type);
                string runtimeReference = FactoryRegistryGeneratorUtility.TypeReference(runtimeType.Type);
                builder.AppendLine($"        factory.Register(typeof({dataReference}), static request => new {runtimeReference}(({dataReference})request.Data, request.Runtime));");
            }
            builder.AppendLine("    }");
        }

        private static FactoryRegistryGeneratorUtility.MappedType FindByKey(
            string key,
            List<FactoryRegistryGeneratorUtility.MappedType> runtimeTypes)
        {
            for (int i = 0; i < runtimeTypes.Count; i++)
            {
                if (string.Equals(runtimeTypes[i].Mapping.Key, key, StringComparison.Ordinal))
                    return runtimeTypes[i];
            }

            return null;
        }
    }
}
