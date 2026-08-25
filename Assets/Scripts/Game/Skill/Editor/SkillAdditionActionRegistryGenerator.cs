using System;
using System.Collections.Generic;
using System.Text;
using CrystalMagic.Editor;
using CrystalMagic.Game.Data;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Skill
{
    public static class SkillAdditionActionRegistryGenerator
    {
        private const string OutputPath = "Assets/Scripts/Game/Skill/SkillAdditionActionRegistry.cs";

        [MenuItem("Tools/Registry/Skill Addition Action")]
        public static void Generate()
        {
            List<FactoryRegistryGeneratorUtility.MappedType> dataTypes =
                FactoryRegistryGeneratorUtility.CollectMappedTypes(typeof(SkillAdditionActionData), subclassOnly: true);
            List<FactoryRegistryGeneratorUtility.MappedType> runtimeTypes =
                FactoryRegistryGeneratorUtility.CollectMappedTypes(typeof(CrystalMagic.Game.Skill.SkillAdditionAction), subclassOnly: true);

            RegistryGeneratorUtility.WriteFile(OutputPath, BuildRegistry(dataTypes, runtimeTypes));
            AssetDatabase.Refresh();
            Debug.Log($"[SkillAdditionActionRegistryGenerator] Generated {OutputPath}.");
        }

        private static string BuildRegistry(
            List<FactoryRegistryGeneratorUtility.MappedType> dataTypes,
            List<FactoryRegistryGeneratorUtility.MappedType> runtimeTypes)
        {
            StringBuilder builder = new();
            builder.AppendLine("// AUTO-GENERATED - DO NOT EDIT MANUALLY");
            builder.AppendLine("// Use menu: Tools/Registry/Skill Addition Action");
            builder.AppendLine();
            builder.AppendLine("using CrystalMagic.Game.Data;");
            builder.AppendLine();
            builder.AppendLine("namespace CrystalMagic.Game.Skill");
            builder.AppendLine("{");
            builder.AppendLine("    public static class SkillAdditionActionRegistry");
            builder.AppendLine("    {");
            builder.AppendLine("        private static readonly SkillAdditionActionFactory s_factory = CreateFactory();");
            builder.AppendLine();
            builder.AppendLine("        public static SkillAdditionAction Create(SkillAdditionActionData data, SkillAdditionActionContext context)");
            builder.AppendLine("        {");
            builder.AppendLine("            return s_factory.CreateAction(data, context);");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        private static SkillAdditionActionFactory CreateFactory()");
            builder.AppendLine("        {");
            builder.AppendLine("            SkillAdditionActionFactory factory = new();");

            for (int i = 0; i < dataTypes.Count; i++)
            {
                FactoryRegistryGeneratorUtility.MappedType dataType = dataTypes[i];
                FactoryRegistryGeneratorUtility.MappedType runtimeType = FindByKey(dataType.Mapping.Key, runtimeTypes);
                if (runtimeType == null)
                {
                    Debug.LogWarning($"[SkillAdditionActionRegistryGenerator] Missing runtime for {dataType.Type.Name}.");
                    continue;
                }

                string dataReference = FactoryRegistryGeneratorUtility.TypeReference(dataType.Type);
                string runtimeReference = FactoryRegistryGeneratorUtility.TypeReference(runtimeType.Type);
                builder.AppendLine($"            factory.Register(typeof({dataReference}), static request => new {runtimeReference}(({dataReference})request.Data, request.Context));");
            }

            builder.AppendLine("            return factory;");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
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
