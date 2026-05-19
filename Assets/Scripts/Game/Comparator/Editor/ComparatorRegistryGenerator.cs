using System.Collections.Generic;
using System.Reflection;
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

            string content = BuildRegistry(sources, compareTypes);
            RegistryGeneratorUtility.WriteFile(OutputPath, content);
            AssetDatabase.Refresh();

            Debug.Log($"[ComparatorRegistryGenerator] Generated {OutputPath}.");
        }

        private static string BuildRegistry(
            List<FactoryRegistryGeneratorUtility.MappedType> sources,
            List<FactoryRegistryGeneratorUtility.MappedType> compareTypes)
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
            sb.AppendLine();

            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                sb.AppendLine($"        factory.RegisterSource({FactoryRegistryGeneratorUtility.Literal(source.Mapping.Key)}, static () => new {FactoryRegistryGeneratorUtility.TypeReference(source.Type)}());");
            }

            if (sources.Count > 0 && compareTypes.Count > 0)
                sb.AppendLine();

            for (int i = 0; i < compareTypes.Count; i++)
            {
                var compareType = compareTypes[i];
                FactoryInputMemberAttribute input = compareType.Type.GetCustomAttribute<FactoryInputMemberAttribute>(false);
                if (input != null && FactoryRegistryGeneratorUtility.HasWritableFloatMember(compareType.Type, input.MemberName))
                {
                    sb.AppendLine($"        factory.RegisterCompareType({FactoryRegistryGeneratorUtility.Literal(compareType.Mapping.Key)}, static value => new {FactoryRegistryGeneratorUtility.TypeReference(compareType.Type)} {{ {input.MemberName} = value }});");
                }
                else
                {
                    sb.AppendLine($"        factory.RegisterCompareType({FactoryRegistryGeneratorUtility.Literal(compareType.Mapping.Key)}, static _ => new {FactoryRegistryGeneratorUtility.TypeReference(compareType.Type)}());");
                }
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
