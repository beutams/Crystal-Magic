using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using CrystalMagic.Core;

namespace CrystalMagic.Editor.Data
{
    /// <summary>
    /// 自动生成 DataTableRegistry.cs
    /// 扫描所有 DataRow 子类，并在 Resources/Data/ 中查找对应 JSON 文件
    /// 文件命名约定：{TypeName}Table.json
    /// </summary>
    public static class DataTableRegistryGenerator
    {
        private const string OutputPath = "Assets/Scripts/Core/Data/DataTableRegistry.cs";
        private const string ResourcesDataPath = "Assets/Res/Data";

        [MenuItem("Tools/Registry/Data Table")]
        public static void Generate()
        {
            List<(Type type, string resourcePath)> found = FindTablesWithFiles();

            if (found.Count == 0)
            {
                Debug.LogWarning("[DataTableRegistryGenerator] No DataRow subclasses with matching JSON files found.");
            }

            WriteRegistry(found);
            AssetDatabase.Refresh();
            Debug.Log($"[DataTableRegistryGenerator] Generated {OutputPath} with {found.Count} table(s)");
        }

        // ─────────────────────────────────────────
        //  扫描 DataRow 子类，并检查 Resources/Data 中是否存在对应 JSON
        // ─────────────────────────────────────────
        private static List<(Type, string)> FindTablesWithFiles()
        {
            // 收集所有 DataRow 子类
            List<Type> rowTypes = new();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (Type type in assembly.GetTypes())
                    {
                        if (!type.IsAbstract && !type.IsInterface
                            && typeof(DataRow).IsAssignableFrom(type)
                            && type != typeof(DataRow))
                        {
                            rowTypes.Add(type);
                        }
                    }
                }
                catch { }
            }

            rowTypes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

            // 检查 Resources/Data/{TypeName}Table.json，不存在则创建空文件
            if (!Directory.Exists(ResourcesDataPath))
                Directory.CreateDirectory(ResourcesDataPath);

            List<(Type, string)> result = new();
            foreach (Type type in rowTypes)
            {
                string fullPath = $"{ResourcesDataPath}/{type.Name}Table.json";
                string resourcePath = $"{type.Name}Table"; // 只传表名，路径由 AssetPathHelper 统一处理
                if (!File.Exists(fullPath))
                {
                    File.WriteAllText(fullPath, "{\n  \"Rows\": []\n}", Encoding.UTF8);
                    Debug.Log($"[DataTableRegistryGenerator] Created empty table: {fullPath}");
                }
                result.Add((type, resourcePath));
            }

            return result;
        }

        // ─────────────────────────────────────────
        //  写入 DataTableRegistry.cs
        // ─────────────────────────────────────────
        private static void WriteRegistry(List<(Type type, string resourcePath)> tables)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("// AUTO-GENERATED — DO NOT EDIT MANUALLY");
            sb.AppendLine("// Use menu: Crystal Magic / Generate Data Registry");
            sb.AppendLine($"// Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            // 收集所有需要的命名空间
            HashSet<string> namespaces = new() { "CrystalMagic.Core" };
            foreach ((Type type, _) in tables)
            {
                if (!string.IsNullOrEmpty(type.Namespace))
                    namespaces.Add(type.Namespace);
            }

            foreach (string ns in namespaces)
                sb.AppendLine($"using {ns};");
            sb.AppendLine();

            sb.AppendLine("namespace CrystalMagic.Core");
            sb.AppendLine("{");
            sb.AppendLine("    public static class DataTableRegistry");
            sb.AppendLine("    {");
            sb.AppendLine("        public static void RegisterAll(DataComponent component)");
            sb.AppendLine("        {");

            if (tables.Count == 0)
            {
                sb.AppendLine("            // 未找到任何匹配的配置表文件");
                sb.AppendLine("            // 请在 Resources/Data/ 下放置 {TypeName}Table.json 后重新生成");
            }
            else
            {
                foreach ((Type type, string resourcePath) in tables)
                    sb.AppendLine($"            component.LoadTable<{type.Name}>(\"{resourcePath}\");");
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            string dir = Path.GetDirectoryName(OutputPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(OutputPath, sb.ToString(), Encoding.UTF8);
        }
    }
}
