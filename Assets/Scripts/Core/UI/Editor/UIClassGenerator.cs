using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.UI
{
    /// <summary>
    /// UIBase 子类生成器
    /// Project 视图右键 Prefab → Assets/Tools/Generate UI Class
    /// - 同步生成 UIData（始终覆盖）
    /// - 若 UI 类文件已存在则跳过，不覆盖
    /// 输出：Assets/Scripts/UI/{PrefabName}/{PrefabName}.cs（与 UIData 同目录）
    /// </summary>
    public static class UIClassGenerator
    {
        private const string HierarchyMenuPath = "GameObject/Tools/Generate UI Class";

        [MenuItem(HierarchyMenuPath, false, 801)]
        private static void GenerateFromHierarchy()
        {
            GameObject prefab = GetSelectedGameObject();
            if (prefab == null) return;

            string className = prefab.name;
            string dataClassName = UIDataGenerator.GenerateForPrefab(prefab);

            string outputDir = Path.Combine("Assets/Scripts/UI", prefab.name);
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            WriteIfMissing(Path.Combine(outputDir, $"{className}.cs"), BuildViewCode(className, dataClassName));
            WriteIfMissing(Path.Combine(outputDir, $"{className}Controller.cs"), BuildControllerCode(className));
            WriteIfMissing(Path.Combine(outputDir, $"{className}Model.cs"), BuildModelCode(className));
            AssetDatabase.Refresh();
            Debug.Log($"[UIClassGenerator] Generated MVC files for {className}");
        }

        [MenuItem(HierarchyMenuPath, true)]
        private static bool ValidateGenerateFromHierarchy() => GetSelectedGameObject() != null;

        // ─────────────────────────────────────────
        private static void WriteIfMissing(string filePath, string content)
        {
            if (File.Exists(filePath))
            {
                Debug.Log($"[UIClassGenerator] {filePath} already exists, skipped");
                return;
            }

            File.WriteAllText(filePath, content, Encoding.UTF8);
            Debug.Log($"[UIClassGenerator] Generated {filePath}");
        }

        private static string BuildViewCode(string className, string dataClassName)
        {
            StringBuilder sb = new();
            sb.AppendLine("using CrystalMagic.Core;");
            sb.AppendLine();
            sb.AppendLine($"public class {className} : UIBase<{dataClassName}>");
            sb.AppendLine("{");
            sb.AppendLine("    protected override void OnInit()");
            sb.AppendLine("    {");
            sb.AppendLine("        base.OnInit();");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public override void OnOpen()");
            sb.AppendLine("    {");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public override void OnClose()");
            sb.AppendLine("    {");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string BuildControllerCode(string className)
        {
            StringBuilder sb = new();
            sb.AppendLine("namespace CrystalMagic.UI");
            sb.AppendLine("{");
            sb.AppendLine($"    public sealed class {className}Controller : UIControllerBase<{className}, {className}Model>");
            sb.AppendLine("    {");
            sb.AppendLine($"        public {className}Controller({className} view, {className}Model model)");
            sb.AppendLine("            : base(view, model)");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        protected override void OnOpen()");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string BuildModelCode(string className)
        {
            StringBuilder sb = new();
            sb.AppendLine("namespace CrystalMagic.UI");
            sb.AppendLine("{");
            sb.AppendLine($"    public sealed class {className}Model : UIModelBase");
            sb.AppendLine("    {");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static GameObject GetSelectedGameObject()
        {
            if (Selection.activeGameObject != null)
                return Selection.activeGameObject;

            return Selection.activeObject as GameObject;
        }
    }
}
