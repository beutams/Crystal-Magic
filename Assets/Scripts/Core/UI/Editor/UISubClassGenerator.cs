using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CrystalMagic.Editor.UI
{
    [InitializeOnLoad]
    internal static class UISubClassAutoAttach
    {
        static UISubClassAutoAttach()
        {
            EditorApplication.delayCall += UISubClassGenerator.TryAttachPendingComponent;
        }
    }

    public static class UISubClassGenerator
    {
        private const string ToolsMenuPath = "Tools/UI/Generate Sub UIView";
        private const string HierarchyMenuPath = "GameObject/Tools/Generate Sub UIView";
        private const string PendingStageAssetKey = "CrystalMagic.UISub.PendingStageAsset";
        private const string PendingNodePathKey = "CrystalMagic.UISub.PendingNodePath";
        private const string PendingViewClassKey = "CrystalMagic.UISub.PendingViewClass";

        [MenuItem(ToolsMenuPath, false, 20)]
        private static void GenerateFromTools()
        {
            Generate();
        }

        [MenuItem(ToolsMenuPath, true)]
        private static bool ValidateGenerateFromTools()
        {
            return ValidateGenerate();
        }

        [MenuItem(HierarchyMenuPath, false, 20)]
        private static void GenerateFromHierarchy()
        {
            Generate();
        }

        [MenuItem(HierarchyMenuPath, true)]
        private static bool ValidateGenerateFromHierarchy()
        {
            return ValidateGenerate();
        }

        private static void Generate()
        {
            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            GameObject selected = Selection.activeGameObject;
            if (prefabStage == null || selected == null)
                return;

            Transform prefabRoot = prefabStage.prefabContentsRoot.transform;
            Transform selectedTransform = selected.transform;
            if (selectedTransform == prefabRoot)
            {
                Debug.LogWarning("[UISubClassGenerator] Select a child node inside Prefab Mode to generate a Sub UIView.");
                return;
            }

            string ownerUiName = prefabRoot.name;
            string baseName = BuildSubTypeName(prefabRoot, selectedTransform);
            string outputDir = Path.Combine("Assets/Scripts/UI", ownerUiName, "Sub");
            string dataClassName = $"{baseName}Data";
            string viewClassName = $"{baseName}View";

            UIDataGenerator.GenerateForTransform(selectedTransform, dataClassName, outputDir);
            WriteIfMissing(Path.Combine(outputDir, $"{viewClassName}.cs"), BuildViewCode(viewClassName, dataClassName));

            SavePendingAttach(prefabStage.assetPath, BuildRelativeNodePath(prefabRoot, selectedTransform), viewClassName);

            AssetDatabase.Refresh();
            TryAttachPendingComponent();

            Debug.Log($"[UISubClassGenerator] Generated {viewClassName} and {dataClassName} for {selected.name}");
        }

        private static bool ValidateGenerate()
        {
            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            GameObject selected = Selection.activeGameObject;
            return prefabStage != null
                && selected != null
                && selected.transform != prefabStage.prefabContentsRoot.transform
                && selected.transform.IsChildOf(prefabStage.prefabContentsRoot.transform);
        }

        internal static void TryAttachPendingComponent()
        {
            string stageAssetPath = SessionState.GetString(PendingStageAssetKey, string.Empty);
            string nodePath = SessionState.GetString(PendingNodePathKey, string.Empty);
            string viewClassName = SessionState.GetString(PendingViewClassKey, string.Empty);

            if (string.IsNullOrEmpty(stageAssetPath) || string.IsNullOrEmpty(nodePath) || string.IsNullOrEmpty(viewClassName))
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryAttachPendingComponent;
                return;
            }

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null || !string.Equals(prefabStage.assetPath, stageAssetPath, StringComparison.OrdinalIgnoreCase))
                return;

            Type viewType = FindTypeByName(viewClassName);
            if (viewType == null || !typeof(MonoBehaviour).IsAssignableFrom(viewType))
            {
                EditorApplication.delayCall += TryAttachPendingComponent;
                return;
            }

            Transform target = FindByRelativeNodePath(prefabStage.prefabContentsRoot.transform, nodePath);
            if (target == null)
            {
                ClearPendingAttach();
                Debug.LogWarning($"[UISubClassGenerator] Failed to find target node for pending component attach: {nodePath}");
                return;
            }

            if (target.GetComponent(viewType) == null)
            {
                Undo.AddComponent(target.gameObject, viewType);
                EditorUtility.SetDirty(target.gameObject);
            }

            Selection.activeGameObject = target.gameObject;
            ClearPendingAttach();
        }

        private static void SavePendingAttach(string stageAssetPath, string nodePath, string viewClassName)
        {
            SessionState.SetString(PendingStageAssetKey, stageAssetPath);
            SessionState.SetString(PendingNodePathKey, nodePath);
            SessionState.SetString(PendingViewClassKey, viewClassName);
        }

        private static void ClearPendingAttach()
        {
            SessionState.EraseString(PendingStageAssetKey);
            SessionState.EraseString(PendingNodePathKey);
            SessionState.EraseString(PendingViewClassKey);
        }

        private static void WriteIfMissing(string filePath, string content)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            if (File.Exists(filePath))
            {
                Debug.Log($"[UISubClassGenerator] {filePath} already exists, skipped");
                return;
            }

            File.WriteAllText(filePath, content, Encoding.UTF8);
            Debug.Log($"[UISubClassGenerator] Generated {filePath}");
        }

        private static string BuildViewCode(string viewClassName, string dataClassName)
        {
            StringBuilder sb = new();
            sb.AppendLine("using CrystalMagic.Core;");
            sb.AppendLine();
            sb.AppendLine($"public class {viewClassName} : UISubView<{dataClassName}>");
            sb.AppendLine("{");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string BuildSubTypeName(Transform root, Transform target)
        {
            string prefabName = UIDataGenerator.SanitizeTypeName(root.name);
            string gameObjectName = UIDataGenerator.SanitizeTypeName(target.name);
            int sameNameIndex = GetSameNameSiblingIndex(target);

            if (sameNameIndex > 0)
                gameObjectName += $"_{sameNameIndex}";

            return $"{prefabName}_{gameObjectName}";
        }

        private static string BuildRelativeNodePath(Transform root, Transform target)
        {
            if (target == root)
                return string.Empty;

            string currentSegment = $"{target.name}#{GetSameNameSiblingIndex(target)}";
            if (target.parent == root)
                return currentSegment;

            return $"{BuildRelativeNodePath(root, target.parent)}/{currentSegment}";
        }

        private static Transform FindByRelativeNodePath(Transform root, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return root;

            Transform current = root;
            string[] segments = relativePath.Split('/');
            foreach (string segment in segments)
            {
                int separatorIndex = segment.LastIndexOf('#');
                if (separatorIndex <= 0 || separatorIndex >= segment.Length - 1)
                    return null;

                string childName = segment.Substring(0, separatorIndex);
                if (!int.TryParse(segment.Substring(separatorIndex + 1), out int sameNameIndex))
                    return null;

                current = FindChildByNameIndex(current, childName, sameNameIndex);
                if (current == null)
                    return null;
            }

            return current;
        }

        private static Transform FindChildByNameIndex(Transform parent, string childName, int sameNameIndex)
        {
            int matchIndex = 0;
            foreach (Transform child in parent)
            {
                if (child.name != childName)
                    continue;

                if (matchIndex == sameNameIndex)
                    return child;

                matchIndex++;
            }

            return null;
        }

        private static int GetSameNameSiblingIndex(Transform transform)
        {
            int index = 0;
            Transform parent = transform.parent;
            if (parent == null)
                return index;

            foreach (Transform sibling in parent)
            {
                if (sibling == transform)
                    break;

                if (sibling.name == transform.name)
                    index++;
            }

            return index;
        }

        private static Type FindTypeByName(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(typeName);
                if (type != null)
                    return type;

                try
                {
                    foreach (Type candidate in assembly.GetTypes())
                    {
                        if (candidate.Name == typeName)
                            return candidate;
                    }
                }
                catch
                {
                }
            }

            return null;
        }
    }
}
