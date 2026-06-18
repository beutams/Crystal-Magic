using System;
using System.Collections.Generic;
using System.Text;
using CrystalMagic.Editor;
using CrystalMagic.Game.Data;
using UnityEditor;
using UnityEngine;

public static class NPCInteractionNodeRegistryGenerator
{
    private const string NodeDataOutputPath = "Assets/Scripts/Game/Unit/NPCInteraction/NPCInteractionNodeDataRegistry.cs";
    private const string RunnerOutputPath = "Assets/Scripts/Game/Unit/NPCInteraction/NPCInteractionNodeRunnerRegistry.cs";

    [MenuItem("Tools/Registry/NPC Interaction Node")]
    public static void Generate()
    {
        List<FactoryRegistryGeneratorUtility.MappedType> nodeDataTypes =
            FactoryRegistryGeneratorUtility.CollectMappedTypes(typeof(NPCInteractionNodeData), subclassOnly: true);
        List<Type> runnerTypes = RegistryGeneratorUtility.CollectTypes(typeof(NPCInteractionNodeRunner), subclassOnly: true);

        string nodeDataContent = BuildNodeDataRegistry(nodeDataTypes);
        string runnerContent = BuildRunnerRegistry(nodeDataTypes, runnerTypes);
        RegistryGeneratorUtility.WriteFile(NodeDataOutputPath, nodeDataContent);
        RegistryGeneratorUtility.WriteFile(RunnerOutputPath, runnerContent);
        AssetDatabase.Refresh();

        Debug.Log($"[NPCInteractionNodeRegistryGenerator] Generated {NodeDataOutputPath} and {RunnerOutputPath}.");
    }

    private static string BuildNodeDataRegistry(List<FactoryRegistryGeneratorUtility.MappedType> nodeDataTypes)
    {
        StringBuilder sb = new();
        sb.AppendLine("// AUTO-GENERATED - DO NOT EDIT MANUALLY");
        sb.AppendLine("// Use menu: Tools/Registry/NPC Interaction Node");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine("namespace CrystalMagic.Game.Data");
        sb.AppendLine("{");
        sb.AppendLine("    public static class NPCInteractionNodeDataRegistry");
        sb.AppendLine("    {");

        AppendMetadata(sb, nodeDataTypes);
        AppendNodeDataRegistration(sb, nodeDataTypes);

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string BuildRunnerRegistry(
        List<FactoryRegistryGeneratorUtility.MappedType> nodeDataTypes,
        List<Type> runnerTypes)
    {
        StringBuilder sb = new();
        sb.AppendLine("// AUTO-GENERATED - DO NOT EDIT MANUALLY");
        sb.AppendLine("// Use menu: Tools/Registry/NPC Interaction Node");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using CrystalMagic.Game.Data;");
        sb.AppendLine();
        sb.AppendLine("public static class NPCInteractionNodeRunnerRegistry");
        sb.AppendLine("{");

        AppendRunnerRegistration(sb, nodeDataTypes, runnerTypes);

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void AppendMetadata(StringBuilder sb, List<FactoryRegistryGeneratorUtility.MappedType> nodeDataTypes)
    {
        sb.AppendLine("        private static readonly string[] s_typeOrder =");
        sb.AppendLine("        {");
        for (int i = 0; i < nodeDataTypes.Count; i++)
            sb.AppendLine($"            {FactoryRegistryGeneratorUtility.Literal(nodeDataTypes[i].Mapping.Key)},");
        sb.AppendLine("        };");
        sb.AppendLine();

        sb.AppendLine("        private static readonly Dictionary<string, Type> s_types = new(StringComparer.Ordinal)");
        sb.AppendLine("        {");
        for (int i = 0; i < nodeDataTypes.Count; i++)
        {
            var mapped = nodeDataTypes[i];
            sb.AppendLine($"            {{ {FactoryRegistryGeneratorUtility.Literal(mapped.Mapping.Key)}, typeof({FactoryRegistryGeneratorUtility.TypeReference(mapped.Type)}) }},");
        }
        sb.AppendLine("        };");
        sb.AppendLine();

        sb.AppendLine("        private static readonly Dictionary<Type, string> s_keys = new()");
        sb.AppendLine("        {");
        for (int i = 0; i < nodeDataTypes.Count; i++)
        {
            var mapped = nodeDataTypes[i];
            sb.AppendLine($"            {{ typeof({FactoryRegistryGeneratorUtility.TypeReference(mapped.Type)}), {FactoryRegistryGeneratorUtility.Literal(mapped.Mapping.Key)} }},");
        }
        sb.AppendLine("        };");
        sb.AppendLine();

        sb.AppendLine("        private static readonly Dictionary<string, string> s_displayNames = new(StringComparer.Ordinal)");
        sb.AppendLine("        {");
        for (int i = 0; i < nodeDataTypes.Count; i++)
        {
            var mapped = nodeDataTypes[i];
            sb.AppendLine($"            {{ {FactoryRegistryGeneratorUtility.Literal(mapped.Mapping.Key)}, {FactoryRegistryGeneratorUtility.Literal(FactoryRegistryGeneratorUtility.DisplayName(mapped))} }},");
        }
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        public static IReadOnlyList<string> TypeOrder => s_typeOrder;");
        sb.AppendLine();
        sb.AppendLine("        public static bool ContainsKey(string key)");
        sb.AppendLine("        {");
        sb.AppendLine("            return s_types.ContainsKey(key ?? string.Empty);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public static bool TryGetNodeType(string key, out Type type)");
        sb.AppendLine("        {");
        sb.AppendLine("            return s_types.TryGetValue(key ?? string.Empty, out type);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public static bool TryGetNodeKey(Type type, out string key)");
        sb.AppendLine("        {");
        sb.AppendLine("            return s_keys.TryGetValue(type, out key);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public static string GetDisplayName(string key)");
        sb.AppendLine("        {");
        sb.AppendLine("            return s_displayNames.TryGetValue(key ?? string.Empty, out string displayName)");
        sb.AppendLine("                ? displayName");
        sb.AppendLine("                : key ?? \"Unknown\";");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void AppendNodeDataRegistration(StringBuilder sb, List<FactoryRegistryGeneratorUtility.MappedType> nodeDataTypes)
    {
        sb.AppendLine("        public static void RegisterAll(NPCInteractionNodeDataFactory factory)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (factory == null)");
        sb.AppendLine("                return;");
        sb.AppendLine();
        for (int i = 0; i < nodeDataTypes.Count; i++)
        {
            var mapped = nodeDataTypes[i];
            sb.AppendLine($"            factory.Register({FactoryRegistryGeneratorUtility.Literal(mapped.Mapping.Key)}, static () => new {FactoryRegistryGeneratorUtility.TypeReference(mapped.Type)}());");
        }
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void AppendRunnerRegistration(
        StringBuilder sb,
        List<FactoryRegistryGeneratorUtility.MappedType> nodeDataTypes,
        List<Type> runnerTypes)
    {
        sb.AppendLine("    public static void RegisterAll(NPCInteractionNodeRunnerFactory factory)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (factory == null)");
        sb.AppendLine("            return;");
        sb.AppendLine();
        for (int i = 0; i < nodeDataTypes.Count; i++)
        {
            var nodeDataType = nodeDataTypes[i];
            Type runnerType = FindRunnerType(nodeDataType.Type, runnerTypes);
            if (runnerType == null)
            {
                Debug.LogWarning($"[NPCInteractionNodeRegistryGenerator] Missing runner for node data: {nodeDataType.Type.Name}");
                continue;
            }

            sb.AppendLine($"        factory.Register(typeof({FactoryRegistryGeneratorUtility.TypeReference(nodeDataType.Type)}), static node => new {FactoryRegistryGeneratorUtility.TypeReference(runnerType)}(({FactoryRegistryGeneratorUtility.TypeReference(nodeDataType.Type)})node));");
        }
        sb.AppendLine("    }");
    }

    private static Type FindRunnerType(Type nodeType, List<Type> runnerTypes)
    {
        string expectedName = nodeType.Name.Replace("NodeData", "NodeRunner");
        for (int i = 0; i < runnerTypes.Count; i++)
        {
            if (runnerTypes[i].Name == expectedName)
                return runnerTypes[i];
        }

        return null;
    }
}
