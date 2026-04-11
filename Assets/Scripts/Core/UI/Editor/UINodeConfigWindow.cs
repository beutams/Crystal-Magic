using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.UI
{
    /// <summary>
    /// UINode 组件配置编辑器
    /// 配置 UINode 中包含哪些组件类型，并生成对应的 UINode.cs
    /// 菜单：Tools/UI/UINode Config
    /// </summary>
    public class UINodeConfigWindow : EditorWindow
    {
        private const string UINodeOutputPath = "Assets/Scripts/Core/UI/UINode.cs";

        private UINodeConfig _config;
        private Vector2 _scroll;
        private bool _isDirty;
        private string _statusText = "";

        // 新增行的临时输入
        private string _newTypeName = "";
        private string _newNamespace = "";

        [MenuItem("Tools/Config/UINode Config")]
        public static void Open()
        {
            var w = GetWindow<UINodeConfigWindow>("UINode Config");
            w.minSize = new Vector2(420, 360);
            w.Show();
        }

        private void OnEnable()
        {
            _config = UINodeConfig.Load();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawComponentList();
            DrawAddRow();
        }

        // ─────────────────────────────────────────
        //  工具栏
        // ─────────────────────────────────────────
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUI.enabled = _isDirty;
            if (GUILayout.Button(_isDirty ? "保存 *" : "保存",
                EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                _config.Save();
                _isDirty = false;
                _statusText = "已保存配置";
            }
            GUI.enabled = true;

            if (GUILayout.Button("生成 UINode", EditorStyles.toolbarButton, GUILayout.Width(90)))
                GenerateUINode();

            GUILayout.FlexibleSpace();

            if (!string.IsNullOrEmpty(_statusText))
                GUILayout.Label(_statusText, EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        // ─────────────────────────────────────────
        //  组件列表
        // ─────────────────────────────────────────
        private void DrawComponentList()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("组件类型列表", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            _scroll = EditorGUILayout.BeginScrollView(_scroll,
                GUILayout.ExpandHeight(true), GUILayout.MaxHeight(position.height - 130));

            int removeIdx = -1;
            for (int i = 0; i < _config.Components.Count; i++)
            {
                var entry = _config.Components[i];
                EditorGUILayout.BeginHorizontal();

                string newType = EditorGUILayout.TextField(entry.TypeName, GUILayout.Width(160));
                string newNs = EditorGUILayout.TextField(entry.Namespace, GUILayout.ExpandWidth(true));

                if (newType != entry.TypeName || newNs != entry.Namespace)
                {
                    entry.TypeName = newType;
                    entry.Namespace = newNs;
                    _isDirty = true;
                }

                GUI.color = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(22)))
                    removeIdx = i;
                GUI.color = Color.white;

                EditorGUILayout.EndHorizontal();
            }

            if (removeIdx >= 0)
            {
                _config.Components.RemoveAt(removeIdx);
                _isDirty = true;
            }

            EditorGUILayout.EndScrollView();
        }

        // ─────────────────────────────────────────
        //  新增行
        // ─────────────────────────────────────────
        private void DrawAddRow()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("添加组件", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            _newTypeName = EditorGUILayout.TextField(_newTypeName,
                GUILayout.Width(160));
            _newNamespace = EditorGUILayout.TextField(_newNamespace,
                GUILayout.ExpandWidth(true));

            GUI.enabled = !string.IsNullOrWhiteSpace(_newTypeName);
            if (GUILayout.Button("添加", GUILayout.Width(50)))
            {
                _config.Components.Add(new UINodeComponentEntry
                {
                    TypeName = _newTypeName.Trim(),
                    Namespace = _newNamespace.Trim()
                });
                _newTypeName = "";
                _newNamespace = "";
                _isDirty = true;
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        // ─────────────────────────────────────────
        //  生成 UINode.cs
        // ─────────────────────────────────────────
        private void GenerateUINode()
        {
            // 保存配置
            _config.Save();
            _isDirty = false;

            var components = _config.Components;
            StringBuilder sb = new();

            sb.AppendLine("// AUTO-GENERATED — DO NOT EDIT MANUALLY");
            sb.AppendLine("// Use Tools/UI/UINode Config → Generate UINode to regenerate");
            sb.AppendLine();

            // using 去重
            HashSet<string> usings = new() { "UnityEngine" };
            foreach (var c in components)
                if (!string.IsNullOrEmpty(c.Namespace))
                    usings.Add(c.Namespace);
            foreach (string u in usings)
                sb.AppendLine($"using {u};");

            sb.AppendLine();
            sb.AppendLine("namespace CrystalMagic.Core");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// UI 子节点引用容器");
            sb.AppendLine("    /// 包含该节点上可能挂载的常用组件，组件不存在时为 null");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public class UINode");
            sb.AppendLine("    {");
            sb.AppendLine("        public GameObject GameObject;");

            foreach (var c in components)
                sb.AppendLine($"        public {c.TypeName} {c.TypeName};");

            sb.AppendLine();
            sb.AppendLine("        public static UINode From(GameObject go)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (go == null) return null;");
            sb.AppendLine("            var node = new UINode { GameObject = go };");
            foreach (var c in components)
                sb.AppendLine($"            node.{c.TypeName} = go.GetComponent<{c.TypeName}>();");
            sb.AppendLine("            return node;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            string dir = Path.GetDirectoryName(UINodeOutputPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(UINodeOutputPath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();

            _statusText = $"已生成 UINode.cs（{components.Count} 个组件）";
            Debug.Log($"[UINodeConfig] Generated {UINodeOutputPath}");
        }
    }
}
