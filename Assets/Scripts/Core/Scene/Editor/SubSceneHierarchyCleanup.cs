using UnityEditor;
using UnityEditor.SceneManagement;

namespace CrystalMagic.Editor
{
    // Runtime unloads can leave closed authoring scenes in the editor setup.
    // Remove these entries before the next Play Mode backup can retain them.
    [InitializeOnLoad]
    internal static class SubSceneHierarchyCleanup
    {
        static SubSceneHierarchyCleanup()
        {
            EditorApplication.delayCall += CleanupAfterRestore;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                PrepareForPlayMode();
            else if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += CleanupAfterRestore;
        }

        private static void CleanupAfterRestore()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                PrepareForPlayMode();
        }

        internal static void PrepareForPlayMode()
        {
            if (EditorApplication.isPlaying)
                return;

            // Authoring SubScenes can be unloaded during play. Recreate native
            // scenes on restore instead of retaining their scene managers.
            var options = EditorSettings.enterPlayModeOptions;
            if ((options & EnterPlayModeOptions.DisableSceneReload) != 0)
                EditorSettings.enterPlayModeOptions = options & ~EnterPlayModeOptions.DisableSceneReload;
            RemoveUnloadedSubScenes();
        }

        internal static int RemoveUnloadedSubScenes()
        {
            if (EditorApplication.isPlaying)
                return 0;

            int removed = 0;
            // Closing removes the entry, so traverse the hierarchy backwards.
            for (int i = EditorSceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isSubScene && !scene.isLoaded &&
                    EditorSceneManager.CloseScene(scene, true))
                    removed++;
            }
            return removed;
        }
    }
}
