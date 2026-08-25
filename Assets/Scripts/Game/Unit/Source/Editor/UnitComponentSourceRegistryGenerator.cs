using System;
using System.Collections.Generic;
using System.Text;
using CrystalMagic.Editor;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Unit
{
    public static class UnitComponentSourceRegistryGenerator
    {
        private const string OutputPath = "Assets/Scripts/Game/Unit/Source/UnitComponentSourceRegistry.cs";

        [MenuItem("Tools/Registry/Unit Component Sources")]
        public static void Generate()
        {
            List<Type> sourceTypes = RegistryGeneratorUtility.CollectTypes(
                typeof(UnitComponentSource),
                subclassOnly: true);

            string content = BuildRegistry(sourceTypes);
            RegistryGeneratorUtility.WriteFile(OutputPath, content);
            AssetDatabase.Refresh();

            Debug.Log($"[UnitComponentSourceRegistryGenerator] Generated {OutputPath}.");
        }

        private static string BuildRegistry(List<Type> sourceTypes)
        {
            StringBuilder sb = new();
            sb.AppendLine("// AUTO-GENERATED - DO NOT EDIT MANUALLY");
            sb.AppendLine("// Use menu: Tools/Registry/Unit Component Sources");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("public static class UnitComponentSourceRegistry");
            sb.AppendLine("{");
            sb.AppendLine("    private static readonly UnitComponentSource[] s_sources =");
            sb.AppendLine("    {");
            for (int i = 0; i < sourceTypes.Count; i++)
            {
                sb.AppendLine($"        new {RegistryGeneratorUtility.GetFriendlyTypeName(sourceTypes[i])}(),");
            }
            sb.AppendLine("    };");
            sb.AppendLine();
            sb.AppendLine("    public static IReadOnlyList<UnitComponentSource> Sources => s_sources;");
            sb.AppendLine();
            sb.AppendLine("    public static void BindAll(in UnitSourceBindingContext context, UnitSourceAccessTable table)");
            sb.AppendLine("    {");
            sb.AppendLine("        for (int i = 0; i < s_sources.Length; i++)");
            sb.AppendLine("            s_sources[i].Bind(context, table);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public static UnitSourceSchema CreateSchema()");
            sb.AppendLine("    {");
            sb.AppendLine("        UnitSourceSchemaBuilder builder = new();");
            sb.AppendLine("        for (int i = 0; i < s_sources.Length; i++)");
            sb.AppendLine("            s_sources[i].Describe(builder);");
            sb.AppendLine();
            sb.AppendLine("        return builder.Build();");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
