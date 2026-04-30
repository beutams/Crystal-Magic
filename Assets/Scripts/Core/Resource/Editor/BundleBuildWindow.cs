using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Resource
{
    public class BundleBuildWindow : EditorWindow
    {
        private BundleBuildConfigData _config;
        private Vector2 _scrollPosition;
        private string _statusText = string.Empty;

        [MenuItem("Tools/Build/AssetBundle Builder")]
        public static void Open()
        {
            BundleBuildWindow window = GetWindow<BundleBuildWindow>("AssetBundle Builder");
            window.minSize = new Vector2(560f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            _config = BundleBuildUtility.LoadConfig();
            _statusText = BundleBuildUtility.ConfigPath;
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (_config == null)
            {
                EditorGUILayout.HelpBox("Bundle build config not loaded.", MessageType.Warning);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawGeneralSettings();
            EditorGUILayout.Space(8f);
            DrawRules();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(52f)))
            {
                _config = BundleBuildUtility.LoadConfig();
                _statusText = $"Loaded: {BundleBuildUtility.ConfigPath}";
            }

            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(52f)))
            {
                BundleBuildUtility.SaveConfig(_config);
                _statusText = $"Saved: {BundleBuildUtility.ConfigPath}";
            }

            if (GUILayout.Button("Build", EditorStyles.toolbarButton, GUILayout.Width(52f)))
            {
                bool success = BundleBuildUtility.Build(_config);
                _statusText = success
                    ? $"Build completed: {BundleBuildUtility.GetOutputPath(_config)}"
                    : "Build failed. Check Console for details.";
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(_statusText, EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawGeneralSettings()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
            _config.OutputRootFolder = EditorGUILayout.TextField("Output Root", _config.OutputRootFolder ?? string.Empty);
            _config.BuildTarget = (BuildTarget)EditorGUILayout.EnumPopup("Build Target", _config.BuildTarget);
            _config.BuildOptions = (BuildAssetBundleOptions)EditorGUILayout.EnumFlagsField("Build Options", _config.BuildOptions);
            _config.CatalogBundleName = EditorGUILayout.TextField("Catalog Bundle", _config.CatalogBundleName ?? string.Empty);
            _config.CatalogAssetName = EditorGUILayout.TextField("Catalog Asset", _config.CatalogAssetName ?? string.Empty);
            _config.TempCatalogAssetPath = EditorGUILayout.TextField("Temp Catalog Asset", _config.TempCatalogAssetPath ?? string.Empty);
            EditorGUILayout.EndVertical();
        }

        private void DrawRules()
        {
            _config.Rules ??= new List<BundleBuildRuleData>();

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Rules", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Add Rule", GUILayout.Width(90f)))
            {
                _config.Rules.Add(new BundleBuildRuleData());
            }
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < _config.Rules.Count; i++)
            {
                DrawRule(i, _config.Rules[i]);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRule(int index, BundleBuildRuleData rule)
        {
            if (rule == null)
                return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            rule.Enabled = EditorGUILayout.Toggle(rule.Enabled, GUILayout.Width(18f));
            EditorGUILayout.LabelField($"Rule {index + 1}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            GUI.enabled = index > 0;
            if (GUILayout.Button("Up", GUILayout.Width(40f)))
            {
                BundleBuildRuleData previous = _config.Rules[index - 1];
                _config.Rules[index - 1] = _config.Rules[index];
                _config.Rules[index] = previous;
                GUI.FocusControl(null);
            }

            GUI.enabled = index < _config.Rules.Count - 1;
            if (GUILayout.Button("Down", GUILayout.Width(50f)))
            {
                BundleBuildRuleData next = _config.Rules[index + 1];
                _config.Rules[index + 1] = _config.Rules[index];
                _config.Rules[index] = next;
                GUI.FocusControl(null);
            }

            GUI.enabled = true;
            if (GUILayout.Button("Delete", GUILayout.Width(60f)))
            {
                _config.Rules.RemoveAt(index);
                GUI.FocusControl(null);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.EndHorizontal();

            rule.FolderPath = EditorGUILayout.TextField("Folder", rule.FolderPath ?? string.Empty);
            rule.BundleName = EditorGUILayout.TextField("Bundle Name", rule.BundleName ?? string.Empty);
            rule.PackingMode = (BundlePackingMode)EditorGUILayout.EnumPopup("Packing Mode", rule.PackingMode);
            rule.IncludeSubfolders = EditorGUILayout.Toggle("Include Subfolders", rule.IncludeSubfolders);
            EditorGUILayout.EndVertical();
        }
    }
}
