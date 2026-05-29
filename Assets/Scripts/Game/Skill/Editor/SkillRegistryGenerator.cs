using System.Collections.Generic;
using System.Text;
using CrystalMagic.Editor;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Skill
{
    public static class SkillRegistryGenerator
    {
        private const string OutputPath = "Assets/Scripts/Game/Skill/SkillRegistry.cs";

        [MenuItem("Tools/Registry/Skill Runtime")]
        public static void Generate()
        {
            List<FactoryRegistryGeneratorUtility.MappedType> runtimeTypes =
                FactoryRegistryGeneratorUtility.CollectMappedTypes(typeof(CrystalMagic.Game.Skill.Skill), subclassOnly: true);

            string content = BuildRegistry(runtimeTypes);
            RegistryGeneratorUtility.WriteFile(OutputPath, content);
            AssetDatabase.Refresh();

            Debug.Log($"[SkillRegistryGenerator] Generated {OutputPath}.");
        }

        private static string BuildRegistry(List<FactoryRegistryGeneratorUtility.MappedType> runtimeTypes)
        {
            StringBuilder sb = new();
            sb.AppendLine("// AUTO-GENERATED - DO NOT EDIT MANUALLY");
            sb.AppendLine("// Use menu: Tools/Registry/Skill Runtime");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace CrystalMagic.Game.Skill");
            sb.AppendLine("{");
            sb.AppendLine("    public static class SkillRegistry");
            sb.AppendLine("    {");

            AppendMetadata(sb, runtimeTypes);
            AppendRegistration(sb, runtimeTypes);

            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void AppendMetadata(StringBuilder sb, List<FactoryRegistryGeneratorUtility.MappedType> runtimeTypes)
        {
            string defaultKey = runtimeTypes.Count > 0 ? runtimeTypes[0].Mapping.Key : string.Empty;

            sb.AppendLine("        private static readonly string[] s_skillRuntimeTypeOrder =");
            sb.AppendLine("        {");
            for (int i = 0; i < runtimeTypes.Count; i++)
                sb.AppendLine($"            {FactoryRegistryGeneratorUtility.Literal(runtimeTypes[i].Mapping.Key)},");
            sb.AppendLine("        };");
            sb.AppendLine();

            sb.AppendLine("        private static readonly Dictionary<string, Type> s_skillRuntimeTypes = new(StringComparer.Ordinal)");
            sb.AppendLine("        {");
            for (int i = 0; i < runtimeTypes.Count; i++)
            {
                FactoryRegistryGeneratorUtility.MappedType mapped = runtimeTypes[i];
                sb.AppendLine($"            {{ {FactoryRegistryGeneratorUtility.Literal(mapped.Mapping.Key)}, typeof({FactoryRegistryGeneratorUtility.TypeReference(mapped.Type)}) }},");
            }

            sb.AppendLine("        };");
            sb.AppendLine();

            sb.AppendLine("        private static readonly Dictionary<Type, string> s_skillRuntimeKeys = new()");
            sb.AppendLine("        {");
            for (int i = 0; i < runtimeTypes.Count; i++)
            {
                FactoryRegistryGeneratorUtility.MappedType mapped = runtimeTypes[i];
                sb.AppendLine($"            {{ typeof({FactoryRegistryGeneratorUtility.TypeReference(mapped.Type)}), {FactoryRegistryGeneratorUtility.Literal(mapped.Mapping.Key)} }},");
            }

            sb.AppendLine("        };");
            sb.AppendLine();

            sb.AppendLine("        private static readonly Dictionary<string, string> s_skillRuntimeDisplayNames = new(StringComparer.Ordinal)");
            sb.AppendLine("        {");
            for (int i = 0; i < runtimeTypes.Count; i++)
            {
                FactoryRegistryGeneratorUtility.MappedType mapped = runtimeTypes[i];
                sb.AppendLine($"            {{ {FactoryRegistryGeneratorUtility.Literal(mapped.Mapping.Key)}, {FactoryRegistryGeneratorUtility.Literal(FactoryRegistryGeneratorUtility.DisplayName(mapped))} }},");
            }

            sb.AppendLine("        };");
            sb.AppendLine();

            sb.AppendLine("        private static readonly FactoryTypeInfo[] s_skillRuntimeTypeInfos =");
            sb.AppendLine("        {");
            for (int i = 0; i < runtimeTypes.Count; i++)
            {
                FactoryRegistryGeneratorUtility.MappedType mapped = runtimeTypes[i];
                sb.AppendLine($"            new({FactoryRegistryGeneratorUtility.Literal(mapped.Mapping.Key)}, {FactoryRegistryGeneratorUtility.Literal(FactoryRegistryGeneratorUtility.DisplayName(mapped))}, typeof({FactoryRegistryGeneratorUtility.TypeReference(mapped.Type)}), {mapped.Mapping.Order}),");
            }

            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine($"        public static string DefaultSkillRuntimeTypeKey => {FactoryRegistryGeneratorUtility.Literal(defaultKey)};");
            sb.AppendLine();
            sb.AppendLine("        public static IReadOnlyList<string> SkillRuntimeTypeOrder => s_skillRuntimeTypeOrder;");
            sb.AppendLine();
            sb.AppendLine("        public static IReadOnlyList<FactoryTypeInfo> SkillRuntimeTypeInfos => s_skillRuntimeTypeInfos;");
            sb.AppendLine();
            sb.AppendLine("        public static bool ContainsSkillRuntimeKey(string key)");
            sb.AppendLine("        {");
            sb.AppendLine("            return s_skillRuntimeTypes.ContainsKey(key ?? string.Empty);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static bool TryGetSkillRuntimeType(string key, out Type type)");
            sb.AppendLine("        {");
            sb.AppendLine("            return s_skillRuntimeTypes.TryGetValue(key ?? string.Empty, out type);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static bool TryGetSkillRuntimeKey(Type type, out string key)");
            sb.AppendLine("        {");
            sb.AppendLine("            return s_skillRuntimeKeys.TryGetValue(type, out key);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static string GetSkillRuntimeDisplayName(string key)");
            sb.AppendLine("        {");
            sb.AppendLine("            return s_skillRuntimeDisplayNames.TryGetValue(key ?? string.Empty, out string displayName)");
            sb.AppendLine("                ? displayName");
            sb.AppendLine("                : key ?? \"Unknown\";");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private static void AppendRegistration(StringBuilder sb, List<FactoryRegistryGeneratorUtility.MappedType> runtimeTypes)
        {
            sb.AppendLine("        public static void RegisterAll(SkillFactory factory)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (factory == null)");
            sb.AppendLine("                return;");
            sb.AppendLine();
            for (int i = 0; i < runtimeTypes.Count; i++)
            {
                FactoryRegistryGeneratorUtility.MappedType mapped = runtimeTypes[i];
                sb.AppendLine($"            factory.Register({FactoryRegistryGeneratorUtility.Literal(mapped.Mapping.Key)}, static data => new {FactoryRegistryGeneratorUtility.TypeReference(mapped.Type)}(data));");
            }

            sb.AppendLine("        }");
        }
    }
}
