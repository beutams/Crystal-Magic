using CrystalMagic.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Scenes;

namespace CrystalMagic.Editor.Map
{
    // Editor-only backdrop for arranging the SubScene's NPCs. It is never saved,
    // baked, or allowed to survive entering Play Mode alongside the managed map.
    [InitializeOnLoad]
    public static class SceneMapPreviewUtility
    {
        private static GameObject _preview;

        static SceneMapPreviewUtility()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) Clear();
            };
            EditorSceneManager.sceneClosing += (scene, _) =>
            {
                if (_preview != null && _preview.scene == scene) Clear();
            };
        }

        [MenuItem("Tools/Map/Preview Town Layout (Editor Only)")]
        public static void PreviewTown() =>
            Preview(SceneMapLayout.Town, TownState.SubSceneName, "城镇", "PlayerTown");

        [MenuItem("Tools/Map/Preview Training Layout (Editor Only)")]
        public static void PreviewTraining() =>
            Preview(SceneMapLayout.Training, TrainingState.SubSceneName, "训练场", "Player");

        private static void Preview(SceneMapLayout layout, string subSceneName, string label, string playerName)
        {
            if (!CanPreview()) return;
            SceneComponent host = Object.FindFirstObjectByType<SceneComponent>(FindObjectsInactive.Include);
            if (host == null)
            {
                Debug.LogError("请先打开 Start 场景，再预览地图布局。");
                return;
            }
            CreatePreview(host.gameObject.scene, layout, label);

            foreach (SubScene subScene in Object.FindObjectsByType<SubScene>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (subScene.name == subSceneName && !subScene.IsLoaded)
                    Unity.Scenes.Editor.SubSceneUtility.EditScene(subScene);
            if (TryGetVisibleBounds(_preview, out Bounds bounds))
                SceneView.lastActiveSceneView?.Frame(bounds, false);
            Debug.Log($"{label}预览已打开。请在 {subSceneName} 中摆放物体和 {playerName}，并保存子场景；预览底图不保存，进入 Play 前自动移除。");
        }

        internal static GameObject CreatePreview(Scene scene, SceneMapLayout layout, string label)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(layout.PrefabPath);
            if (prefab == null) throw new System.InvalidOperationException($"找不到地图显示 Prefab：{layout.PrefabPath}");
            Clear();
            _preview = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            _preview.name = $"[Editor Preview] {label} Map (not saved)";
            _preview.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            layout.Apply(_preview.transform);
            return _preview;
        }

        internal static bool TryGetVisibleBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                if (found) bounds.Encapsulate(renderer.bounds);
                else { bounds = renderer.bounds; found = true; }
            }
            return found;
        }

        [MenuItem("Tools/Map/Preview Town Layout (Editor Only)", true)]
        [MenuItem("Tools/Map/Preview Training Layout (Editor Only)", true)]
        private static bool CanPreview() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("Tools/Map/Clear Map Preview")]
        public static void Clear()
        {
            if (_preview != null) Object.DestroyImmediate(_preview);
            _preview = null;
        }
    }
}
