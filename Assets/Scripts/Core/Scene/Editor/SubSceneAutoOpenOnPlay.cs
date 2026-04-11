/*#if UNITY_EDITOR
using UnityEditor;
using Unity.Scenes;
using Unity.Scenes.Editor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrystalMagic.Editor
{
    /// <summary>
    /// SubScene 在层级里「未 Open」时，子场景 .unity 不会以 Additive 加载，与 GameObject 是否 Active 无关。
    /// 进入 Play 后以及之后通过 <see cref="SceneManager"/> 加载的场景，对勾选 AutoLoad 且尚未 Open 的 SubScene 调用
    /// <see cref="SubSceneUtility.EditScene"/>（Play 模式下等价于 LoadSceneInPlayMode），与手动点 Inspector「Open SubScenes」一致。
    /// </summary>
    [InitializeOnLoad]
    internal static class SubSceneAutoOpenOnPlay
    {
        static SubSceneAutoOpenOnPlay()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                OpenAllClosedAutoLoadSubScenesInLoadedScenes();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Application.isPlaying)
                return;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var sub in root.GetComponentsInChildren<SubScene>(true))
                    TryOpen(sub);
            }
        }

        private static void OpenAllClosedAutoLoadSubScenesInLoadedScenes()
        {
            foreach (var sub in Object.FindObjectsOfType<SubScene>(true))
                TryOpen(sub);
        }

        private static void TryOpen(SubScene sub)
        {
            if (sub == null || !sub.AutoLoadScene)
                return;
            if (!sub.gameObject.activeInHierarchy)
                return;
            if (sub.IsLoaded)
                return;

            SubSceneUtility.EditScene(sub);
        }
    }
}
#endif
*/