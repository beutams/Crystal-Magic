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
        [MenuItem("Assets/Tools/Generate UI Class", false, 801)]
        private static void Generate()
        {
            GameObject prefab = Selection.activeGameObject;
            if (prefab == null) return;

            string className = prefab.name;
            string dataClassName = UIDataGenerator.GenerateForPrefab(prefab);

            string outputDir = Path.Combine("Assets/Scripts/UI", prefab.name);
            string filePath = Path.Combine(outputDir, $"{className}.cs");

            if (File.Exists(filePath))
            {
                Debug.Log($"[UIClassGenerator] {filePath} already exists, skipped");
                AssetDatabase.Refresh();
                return;
            }

            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            File.WriteAllText(filePath, BuildCode(className, dataClassName), Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log($"[UIClassGenerator] Generated {filePath}");
        }

        [MenuItem("Assets/Tools/Generate UI Class", true)]
        private static bool Validate() => UIDataGenerator.IsPrefabSelected();

        // ─────────────────────────────────────────
        private static string BuildCode(string className, string dataClassName)
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
    }
}
