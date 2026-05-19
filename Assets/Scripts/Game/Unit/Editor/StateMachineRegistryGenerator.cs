using System.Collections.Generic;
using System.Text;
using CrystalMagic.Editor;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor
{
    public static class StateMachineRegistryGenerator
    {
        private const string OutputPath = "Assets/Scripts/Game/Unit/StateMachineRegistry.cs";

        [MenuItem("Tools/Registry/State Machine")]
        public static void Generate()
        {
            List<FactoryRegistryGeneratorUtility.MappedType> states =
                FactoryRegistryGeneratorUtility.CollectMappedTypes(typeof(AUnitState), subclassOnly: true);

            string content = BuildRegistry(states);
            RegistryGeneratorUtility.WriteFile(OutputPath, content);
            AssetDatabase.Refresh();

            Debug.Log($"[StateMachineRegistryGenerator] Generated {OutputPath}.");
        }

        private static string BuildRegistry(List<FactoryRegistryGeneratorUtility.MappedType> states)
        {
            StringBuilder sb = new();
            sb.AppendLine("// AUTO-GENERATED - DO NOT EDIT MANUALLY");
            sb.AppendLine("// Use menu: Tools/Registry/State Machine");
            sb.AppendLine();
            sb.AppendLine("public static class StateMachineRegistry");
            sb.AppendLine("{");
            sb.AppendLine("    public static void RegisterAll(StateMachineFactory factory)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (factory == null)");
            sb.AppendLine("            return;");
            sb.AppendLine();
            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                sb.AppendLine($"        factory.Register({FactoryRegistryGeneratorUtility.Literal(state.Mapping.Key)}, static () => new {FactoryRegistryGeneratorUtility.TypeReference(state.Type)}());");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
