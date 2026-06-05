using System.Collections.Generic;
using System.Text;
using CrystalMagic.Editor;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Skill
{
    public static class SkillFollowupFilterRegistryGenerator
    {
        private const string OutputPath = "Assets/Scripts/Game/Skill/SkillFollowupFilterRegistry.cs";

        [MenuItem("Tools/Registry/Skill Followup Filter")]
        public static void Generate()
        {
            List<FactoryRegistryGeneratorUtility.MappedType> dataTypes =
                FactoryRegistryGeneratorUtility.CollectMappedTypes(typeof(CrystalMagic.Game.Data.SkillFollowupFilterData), subclassOnly: true);
            List<FactoryRegistryGeneratorUtility.MappedType> runtimeTypes =
                FactoryRegistryGeneratorUtility.CollectMappedTypes(typeof(CrystalMagic.Game.Skill.SkillFollowupFilter), subclassOnly: true);

            string content = BuildRegistry(dataTypes, runtimeTypes);
            RegistryGeneratorUtility.WriteFile(OutputPath, content);
            AssetDatabase.Refresh();

            Debug.Log($"[SkillFollowupFilterRegistryGenerator] Generated {OutputPath}.");
        }

        private static string BuildRegistry(
            List<FactoryRegistryGeneratorUtility.MappedType> dataTypes,
            List<FactoryRegistryGeneratorUtility.MappedType> runtimeTypes)
        {
            StringBuilder sb = new();
            sb.AppendLine("// AUTO-GENERATED - DO NOT EDIT MANUALLY");
            sb.AppendLine("// Use menu: Tools/Registry/Skill Followup Filter");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using CrystalMagic.Game.Data;");
            sb.AppendLine();
            sb.AppendLine("namespace CrystalMagic.Game.Skill");
            sb.AppendLine("{");
            sb.AppendLine("    public static class SkillFollowupFilterRegistry");
            sb.AppendLine("    {");

            AppendMetadata(sb, dataTypes, runtimeTypes);
            AppendRegistration(sb, dataTypes, runtimeTypes);

            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void AppendMetadata(
            StringBuilder sb,
            List<FactoryRegistryGeneratorUtility.MappedType> dataTypes,
            List<FactoryRegistryGeneratorUtility.MappedType> runtimeTypes)
        {
            List<FactoryRegistryGeneratorUtility.MappedType> validTypes = CollectValidPairs(dataTypes, runtimeTypes);
            string defaultKey = validTypes.Count > 0 ? validTypes[0].Mapping.Key : string.Empty;

            sb.AppendLine("        private static readonly string[] s_filterKeyOrder =");
            sb.AppendLine("        {");
            for (int i = 0; i < validTypes.Count; i++)
                sb.AppendLine($"            {FactoryRegistryGeneratorUtility.Literal(validTypes[i].Mapping.Key)},");
            sb.AppendLine("        };");
            sb.AppendLine();

            sb.AppendLine("        private static readonly Dictionary<string, Type> s_filterDataTypes = new(StringComparer.Ordinal)");
            sb.AppendLine("        {");
            for (int i = 0; i < validTypes.Count; i++)
            {
                FactoryRegistryGeneratorUtility.MappedType mapped = validTypes[i];
                sb.AppendLine($"            {{ {FactoryRegistryGeneratorUtility.Literal(mapped.Mapping.Key)}, typeof({FactoryRegistryGeneratorUtility.TypeReference(mapped.Type)}) }},");
            }
            sb.AppendLine("        };");
            sb.AppendLine();

            sb.AppendLine("        private static readonly Dictionary<string, Type> s_filterRuntimeTypes = new(StringComparer.Ordinal)");
            sb.AppendLine("        {");
            for (int i = 0; i < validTypes.Count; i++)
            {
                FactoryRegistryGeneratorUtility.MappedType mapped = FindByKey(runtimeTypes, validTypes[i].Mapping.Key);
                sb.AppendLine($"            {{ {FactoryRegistryGeneratorUtility.Literal(mapped.Mapping.Key)}, typeof({FactoryRegistryGeneratorUtility.TypeReference(mapped.Type)}) }},");
            }
            sb.AppendLine("        };");
            sb.AppendLine();

            sb.AppendLine("        private static readonly Dictionary<Type, string> s_filterDataKeys = new()");
            sb.AppendLine("        {");
            for (int i = 0; i < validTypes.Count; i++)
            {
                FactoryRegistryGeneratorUtility.MappedType mapped = validTypes[i];
                sb.AppendLine($"            {{ typeof({FactoryRegistryGeneratorUtility.TypeReference(mapped.Type)}), {FactoryRegistryGeneratorUtility.Literal(mapped.Mapping.Key)} }},");
            }
            sb.AppendLine("        };");
            sb.AppendLine();

            sb.AppendLine("        private static readonly Dictionary<Type, string> s_filterRuntimeKeys = new()");
            sb.AppendLine("        {");
            for (int i = 0; i < validTypes.Count; i++)
            {
                FactoryRegistryGeneratorUtility.MappedType mapped = FindByKey(runtimeTypes, validTypes[i].Mapping.Key);
                sb.AppendLine($"            {{ typeof({FactoryRegistryGeneratorUtility.TypeReference(mapped.Type)}), {FactoryRegistryGeneratorUtility.Literal(mapped.Mapping.Key)} }},");
            }
            sb.AppendLine("        };");
            sb.AppendLine();

            sb.AppendLine("        private static readonly Dictionary<string, string> s_filterDisplayNames = new(StringComparer.Ordinal)");
            sb.AppendLine("        {");
            for (int i = 0; i < validTypes.Count; i++)
            {
                FactoryRegistryGeneratorUtility.MappedType mapped = validTypes[i];
                sb.AppendLine($"            {{ {FactoryRegistryGeneratorUtility.Literal(mapped.Mapping.Key)}, {FactoryRegistryGeneratorUtility.Literal(FactoryRegistryGeneratorUtility.DisplayName(mapped))} }},");
            }
            sb.AppendLine("        };");
            sb.AppendLine();

            sb.AppendLine("        private static readonly FactoryTypeInfo[] s_filterTypeInfos =");
            sb.AppendLine("        {");
            for (int i = 0; i < validTypes.Count; i++)
            {
                FactoryRegistryGeneratorUtility.MappedType mapped = validTypes[i];
                sb.AppendLine($"            new({FactoryRegistryGeneratorUtility.Literal(mapped.Mapping.Key)}, {FactoryRegistryGeneratorUtility.Literal(FactoryRegistryGeneratorUtility.DisplayName(mapped))}, typeof({FactoryRegistryGeneratorUtility.TypeReference(mapped.Type)}), {mapped.Mapping.Order}),");
            }
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine($"        public static string DefaultFilterKey => {FactoryRegistryGeneratorUtility.Literal(defaultKey)};");
            sb.AppendLine();
            sb.AppendLine("        public static IReadOnlyList<string> FilterKeyOrder => s_filterKeyOrder;");
            sb.AppendLine();
            sb.AppendLine("        public static IReadOnlyList<FactoryTypeInfo> FilterTypeInfos => s_filterTypeInfos;");
            sb.AppendLine();
            sb.AppendLine("        public static bool TryGetFilterDataType(string key, out Type type)");
            sb.AppendLine("        {");
            sb.AppendLine("            return s_filterDataTypes.TryGetValue(key ?? string.Empty, out type);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static bool TryGetFilterRuntimeType(string key, out Type type)");
            sb.AppendLine("        {");
            sb.AppendLine("            return s_filterRuntimeTypes.TryGetValue(key ?? string.Empty, out type);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static bool TryGetFilterKey(Type type, out string key)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (type != null && s_filterDataKeys.TryGetValue(type, out key))");
            sb.AppendLine("                return true;");
            sb.AppendLine();
            sb.AppendLine("            if (type != null && s_filterRuntimeKeys.TryGetValue(type, out key))");
            sb.AppendLine("                return true;");
            sb.AppendLine();
            sb.AppendLine("            key = null;");
            sb.AppendLine("            return false;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static string GetFilterKey(SkillFollowupFilterData filterData)");
            sb.AppendLine("        {");
            sb.AppendLine("            return filterData != null && s_filterDataKeys.TryGetValue(filterData.GetType(), out string key)");
            sb.AppendLine("                ? key");
            sb.AppendLine("                : string.Empty;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static string GetDisplayName(string key)");
            sb.AppendLine("        {");
            sb.AppendLine("            return s_filterDisplayNames.TryGetValue(key ?? string.Empty, out string displayName)");
            sb.AppendLine("                ? displayName");
            sb.AppendLine("                : key ?? \"Unknown\";");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static SkillFollowupFilterData CreateFilterData(string key)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (!TryGetFilterDataType(key, out Type type))");
            sb.AppendLine("                return null;");
            sb.AppendLine();
            sb.AppendLine("            return Activator.CreateInstance(type) as SkillFollowupFilterData;");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private static void AppendRegistration(
            StringBuilder sb,
            List<FactoryRegistryGeneratorUtility.MappedType> dataTypes,
            List<FactoryRegistryGeneratorUtility.MappedType> runtimeTypes)
        {
            List<FactoryRegistryGeneratorUtility.MappedType> validTypes = CollectValidPairs(dataTypes, runtimeTypes);

            sb.AppendLine("        public static void RegisterAll(SkillFollowupFilterFactory factory)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (factory == null)");
            sb.AppendLine("                return;");
            sb.AppendLine();
            for (int i = 0; i < validTypes.Count; i++)
            {
                FactoryRegistryGeneratorUtility.MappedType mapped = FindByKey(runtimeTypes, validTypes[i].Mapping.Key);
                sb.AppendLine($"            factory.Register({FactoryRegistryGeneratorUtility.Literal(mapped.Mapping.Key)}, static () => new {FactoryRegistryGeneratorUtility.TypeReference(mapped.Type)}());");
            }
            sb.AppendLine("        }");
        }

        private static List<FactoryRegistryGeneratorUtility.MappedType> CollectValidPairs(
            List<FactoryRegistryGeneratorUtility.MappedType> dataTypes,
            List<FactoryRegistryGeneratorUtility.MappedType> runtimeTypes)
        {
            List<FactoryRegistryGeneratorUtility.MappedType> validTypes = new();
            for (int i = 0; i < dataTypes.Count; i++)
            {
                FactoryRegistryGeneratorUtility.MappedType mapped = dataTypes[i];
                if (FindByKey(runtimeTypes, mapped.Mapping.Key) == null)
                    continue;

                validTypes.Add(mapped);
            }

            return validTypes;
        }

        private static FactoryRegistryGeneratorUtility.MappedType FindByKey(
            List<FactoryRegistryGeneratorUtility.MappedType> mappedTypes,
            string key)
        {
            for (int i = 0; i < mappedTypes.Count; i++)
            {
                if (mappedTypes[i].Mapping.Key == key)
                    return mappedTypes[i];
            }

            return null;
        }
    }
}
