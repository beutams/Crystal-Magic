using System;
using System.Collections.Generic;
using System.Text;
using CrystalMagic.Editor;
using CrystalMagic.Game.Data;
using UnityEditor;
using UnityEngine;

public static class NPCInteractionNodeRegistryGenerator
{
    private const string OutputPath = "Assets/Scripts/Game/Unit/NPCInteraction/NPCInteractionNodeRegistry.cs";

    [MenuItem("Tools/NPC Interaction/Generate Node Registry")]
    public static void Generate()
    {
        List<Type> nodeTypes = RegistryGeneratorUtility.CollectTypes(typeof(NPCInteractionNodeData), subclassOnly: true);
        List<Type> runnerTypes = RegistryGeneratorUtility.CollectTypes(typeof(NPCInteractionNodeRunner), subclassOnly: true);

        string content = BuildRegistry(nodeTypes, runnerTypes);
        RegistryGeneratorUtility.WriteFile(OutputPath, content);
        AssetDatabase.Refresh();

        Debug.Log($"[NPCInteractionNodeRegistryGenerator] Generated {OutputPath}. Nodes: {nodeTypes.Count}");
    }

    private static string BuildRegistry(List<Type> nodeTypes, List<Type> runnerTypes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// AUTO-GENERATED - DO NOT EDIT MANUALLY");
        sb.AppendLine("// Use menu: Tools/NPC Interaction/Generate Node Registry");
        sb.AppendLine($"// Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("using CrystalMagic.Game.Data;");
        sb.AppendLine();
        sb.AppendLine("public static class NPCInteractionNodeRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    public static void RegisterAll(NPCInteractionNodeFactory factory)");
        sb.AppendLine("    {");

        foreach (Type nodeType in nodeTypes)
        {
            Type runnerType = FindRunnerType(nodeType, runnerTypes);
            if (runnerType == null)
            {
                Debug.LogWarning($"[NPCInteractionNodeRegistryGenerator] Missing runner for node data: {nodeType.Name}");
                continue;
            }

            sb.AppendLine($"        factory.Register<{RegistryGeneratorUtility.GetFriendlyTypeName(nodeType)}>(node => new {RegistryGeneratorUtility.GetFriendlyTypeName(runnerType)}(node));");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static Type FindRunnerType(Type nodeType, List<Type> runnerTypes)
    {
        string expectedName = nodeType.Name.Replace("NodeData", "NodeRunner");
        for (int i = 0; i < runnerTypes.Count; i++)
        {
            if (runnerTypes[i].Name == expectedName)
            {
                return runnerTypes[i];
            }
        }

        return null;
    }
}
