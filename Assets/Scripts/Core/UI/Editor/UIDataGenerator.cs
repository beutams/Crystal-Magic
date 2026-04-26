using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.UI
{
    /// <summary>
    /// UIData 代码生成器
    /// Project 视图右键 Prefab → Assets/Tools/Generate UIData
    /// 命名：路径各段净化后以 _ 连接；同名兄弟追加 _1/_2...
    /// 查找：唯一名用 Find(path)，同名兄弟用 FindAt(parent, name, index)
    /// 输出：Assets/Scripts/UI/{PrefabName}/{PrefabName}Data.cs（与对应 UI 的 MVC 同目录）
    /// </summary>
    public static class UIDataGenerator
    {
        private const string ToolsMenuPath = "Tools/UI/Generate UIData";
        private const string HierarchyMenuPath = "GameObject/Tools/Generate UIData";

        private struct Entry
        {
            public string FieldName;
            public string FindPath;
            public bool IsDup;
            public string ParentPath;
            public string ChildName;
            public int DupIndex;
        }

        // ─────────────────────────────────────────
        [MenuItem(ToolsMenuPath, false, 800)]
        private static void MenuGenerateFromTools()
        {
            GenerateFromSelection();
        }

        [MenuItem(ToolsMenuPath, true)]
        private static bool MenuValidateFromTools() => IsValidSelection();

        [MenuItem(HierarchyMenuPath, false, 800)]
        private static void MenuGenerateFromHierarchy()
        {
            GenerateFromSelection();
        }

        [MenuItem(HierarchyMenuPath, true)]
        private static bool MenuValidateFromHierarchy() => IsValidSelection();

        // ─────────────────────────────────────────
        //  公共入口（供其他工具调用）
        // ─────────────────────────────────────────
        /// <summary>
        /// 为指定 Prefab 生成 UIData 文件，返回生成的类名
        /// </summary>
        public static string GenerateForPrefab(GameObject prefab)
        {
            string className = prefab.name + "Data";
            string outputDir = Path.Combine("Assets/Scripts/UI", prefab.name);
            return GenerateForTransform(prefab.transform, className, outputDir);
        }

        public static string GenerateForTransform(Transform root, string className, string outputDir)
        {
            List<Entry> entries = new();
            Dictionary<string, int> dupCounter = new();
            HashSet<string> seenFields = new();

            CollectChildren(root, "", "", entries, dupCounter, seenFields);

            if (entries.Count == 0)
            {
                Debug.LogWarning($"[UIDataGenerator] {root.name} has no children, generating empty UIData");
            }

            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            string filePath = Path.Combine(outputDir, $"{className}.cs");
            File.WriteAllText(filePath, BuildCode(className, entries), Encoding.UTF8);
            Debug.Log($"[UIDataGenerator] Generated {filePath}  ({entries.Count} fields)");

            return className;
        }

        // ─────────────────────────────────────────
        //  递归收集子物体
        // ─────────────────────────────────────────
        private static void CollectChildren(
            Transform t,
            string parentFindPath,
            string parentFieldPrefix,
            List<Entry> entries,
            Dictionary<string, int> dupCounter,
            HashSet<string> seenFields)
        {
            foreach (Transform child in t)
            {
                string rawName = child.name;
                string dupKey = $"{parentFindPath}|{rawName}";

                dupCounter.TryGetValue(dupKey, out int dupIdx);
                dupCounter[dupKey] = dupIdx + 1;

                string cleanSegment = SanitizeSegment(rawName);
                if (dupIdx > 0)
                    cleanSegment += $"_{dupIdx}";

                string fieldName = string.IsNullOrEmpty(parentFieldPrefix)
                    ? cleanSegment
                    : $"{parentFieldPrefix}_{cleanSegment}";

                if (seenFields.Contains(fieldName))
                {
                    int suffix = 2;
                    while (seenFields.Contains($"{fieldName}_{suffix}")) suffix++;
                    fieldName = $"{fieldName}_{suffix}";
                }
                seenFields.Add(fieldName);

                string findPath = string.IsNullOrEmpty(parentFindPath)
                    ? rawName
                    : $"{parentFindPath}/{rawName}";

                entries.Add(new Entry
                {
                    FieldName = fieldName,
                    FindPath = findPath,
                    IsDup = dupIdx > 0,
                    ParentPath = parentFindPath,
                    ChildName = rawName,
                    DupIndex = dupIdx
                });

                CollectChildren(child, findPath, fieldName, entries, dupCounter, seenFields);
            }
        }

        // ─────────────────────────────────────────
        //  代码生成
        // ─────────────────────────────────────────
        private static string BuildCode(string className, List<Entry> entries)
        {
            StringBuilder sb = new();
            sb.AppendLine("// AUTO-GENERATED — DO NOT EDIT MANUALLY");
            sb.AppendLine("// Right-click Prefab → Assets/Tools/Generate UIData to regenerate");
            sb.AppendLine();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using CrystalMagic.Core;");
            sb.AppendLine();
            sb.AppendLine($"public class {className} : UIData");
            sb.AppendLine("{");

            foreach (var e in entries)
                sb.AppendLine($"    public UINode {e.FieldName};");

            sb.AppendLine();
            sb.AppendLine("    public override void Bind(Transform root)");
            sb.AppendLine("    {");
            foreach (var e in entries)
            {
                if (!e.IsDup)
                    sb.AppendLine($"        {e.FieldName} = UINode.From(Find(root, \"{e.FindPath}\"));");
                else
                    sb.AppendLine($"        {e.FieldName} = UINode.From(FindAt(root, \"{e.ParentPath}\", \"{e.ChildName}\", {e.DupIndex}));");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        // ─────────────────────────────────────────
        //  净化单段名称为合法 C# 标识符
        // ─────────────────────────────────────────
        public static string SanitizeTypeName(string raw)
        {
            return SanitizeSegment(raw);
        }

        private static string SanitizeSegment(string raw)
        {
            StringBuilder sb = new();
            bool nextUpper = false;

            foreach (char c in raw)
            {
                if (char.IsLetter(c))
                {
                    sb.Append(nextUpper ? char.ToUpper(c) : c);
                    nextUpper = false;
                }
                else if (char.IsDigit(c))
                {
                    if (sb.Length == 0) sb.Append('_');
                    sb.Append(c);
                    nextUpper = false;
                }
                else if (c == '_')
                {
                    sb.Append('_');
                    nextUpper = false;
                }
                else if (c == ' ' || c == '-')
                {
                    nextUpper = true;
                }
            }

            if (sb.Length > 0 && char.IsLower(sb[0]))
                sb[0] = char.ToUpper(sb[0]);

            return sb.Length > 0 ? sb.ToString() : "_";
        }

        // ─────────────────────────────────────────
        public static bool IsPrefabSelected()
        {
            if (Selection.activeObject == null) return false;
            return AssetDatabase.GetAssetPath(Selection.activeObject).EndsWith(".prefab");
        }

        private static void GenerateFromSelection()
        {
            GameObject target = GetSelectedGameObject();
            if (target == null)
                return;

            GenerateForPrefab(target);
            AssetDatabase.Refresh();
        }

        private static bool IsValidSelection()
        {
            return GetSelectedGameObject() != null;
        }

        private static GameObject GetSelectedGameObject()
        {
            if (Selection.activeGameObject != null)
                return Selection.activeGameObject;

            return Selection.activeObject as GameObject;
        }
    }
}
