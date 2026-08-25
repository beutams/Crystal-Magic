using System.Collections.Generic;
using System.Text;
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
            List<FactoryRegistryGeneratorUtility.MappedType> sources =
                FactoryRegistryGeneratorUtility.CollectMappedTypes(typeof(ISource), subclassOnly: false);
            List<FactoryRegistryGeneratorUtility.MappedType> compareTypes =
                FactoryRegistryGeneratorUtility.CollectMappedTypes(typeof(ICompareType), subclassOnly: false);
            List<FactoryRegistryGeneratorUtility.MappedType> operations =
                FactoryRegistryGeneratorUtility.CollectMappedTypes(typeof(IValueOperation), subclassOnly: false);

            string content = BuildRegistry(sources, compareTypes, operations);
            RegistryGeneratorUtility.WriteFile(OutputPath, content);
            AssetDatabase.Refresh();

            Debug.Log($"[ComparatorRegistryGenerator] Generated {OutputPath}.");
        }

        private static string BuildRegistry(
            List<FactoryRegistryGeneratorUtility.MappedType> sources,
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

            AppendFactories(sb, "RegisterSource", sources);
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
}
