using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor
{
    /// <summary>
    /// 自动生成 StateMachineRegistry.cs
    /// 扫描所有 AUnitState 子类、ISource 实现、ICompareType 实现，
    /// 生成对应的工厂注册代码。
    /// 菜单路径：Tools/State Machine/Generate State Machine Registry
    /// </summary>
    public static class StateMachineRegistryGenerator
    {
        private const string OutputPath = "Assets/Scripts/Game/Unit/StateMachineRegistry.cs";

        [MenuItem("Tools/State Machine/Generate State Machine Registry")]
        public static void Generate()
        {
            var states       = CollectTypes(typeof(AUnitState),   subclassOnly: true);
            var sources      = CollectTypes(typeof(ISource),      subclassOnly: false);
            var compareTypes = CollectTypes(typeof(ICompareType), subclassOnly: false);

            WriteRegistry(states, sources, compareTypes);
            AssetDatabase.Refresh();

            Debug.Log($"[StateMachineRegistryGenerator] 生成完成 → {OutputPath}\n" +
                      $"  状态: {states.Count}  ISource: {sources.Count}  ICompareType: {compareTypes.Count}");
        }

        // ─────────────────────────────────────────
        //  类型收集
        // ─────────────────────────────────────────

        private static List<Type> CollectTypes(Type baseType, bool subclassOnly)
        {
            var result = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in assembly.GetTypes())
                    {
                        if (t.IsAbstract || t.IsInterface) continue;
                        bool match = subclassOnly
                            ? t.IsSubclassOf(baseType)
                            : baseType.IsAssignableFrom(t);
                        if (match) result.Add(t);
                    }
                }
                catch { /* 跳过无法枚举的程序集 */ }
            }

            result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            return result;
        }

        // ─────────────────────────────────────────
        //  代码生成
        // ─────────────────────────────────────────

        private static void WriteRegistry(
            List<Type> states, List<Type> sources, List<Type> compareTypes)
        {
            var sb = new StringBuilder();

            sb.AppendLine("// AUTO-GENERATED — DO NOT EDIT MANUALLY");
            sb.AppendLine("// Use menu: Tools/State Machine/Generate State Machine Registry");
            sb.AppendLine($"// Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            sb.AppendLine("public static class StateMachineRegistry");
            sb.AppendLine("{");
            sb.AppendLine("    public static void RegisterAll(StateMachineFactory factory)");
            sb.AppendLine("    {");

            // ── AUnitState 子类 ──────────────────────────────────────────
            sb.AppendLine("        // ── AUnitState 子类 ──────────────────────────────────────────");
            if (states.Count == 0)
                sb.AppendLine("        // （当前无 AUnitState 子类）");
            else
                foreach (var t in states)
                    sb.AppendLine($"        factory.RegisterState<{t.Name}>();");

            sb.AppendLine();

            // ── ISource 实现 ─────────────────────────────────────────────
            sb.AppendLine("        // ── ISource 实现 ─────────────────────────────────────────────");
            if (sources.Count == 0)
                sb.AppendLine("        // （当前无 ISource 实现类）");
            else
                foreach (var t in sources)
                    sb.AppendLine($"        factory.RegisterSource<{t.Name}>();");

            sb.AppendLine();

            // ── ICompareType 实现 ────────────────────────────────────────
            sb.AppendLine("        // ── ICompareType 实现 ────────────────────────────────────────");
            if (compareTypes.Count == 0)
            {
                sb.AppendLine("        // （当前无 ICompareType 实现类）");
            }
            else
            {
                foreach (var t in compareTypes)
                {
                    // 检查是否有 public float value 字段（GreaterThan / LessThan / Equal 等）
                    var valueField = t.GetField("value",
                        BindingFlags.Public | BindingFlags.Instance);
                    bool hasValueField = valueField != null && valueField.FieldType == typeof(float);

                    string line = hasValueField
                        ? $"        factory.RegisterCompareType<{t.Name}>(v => new {t.Name} {{ value = v }});"
                        : $"        factory.RegisterCompareType<{t.Name}>(_ => new {t.Name}());";
                    sb.AppendLine(line);
                }
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            // 写文件
            string dir = Path.GetDirectoryName(OutputPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
            File.WriteAllText(OutputPath, sb.ToString(), Encoding.UTF8);
        }
    }
}
