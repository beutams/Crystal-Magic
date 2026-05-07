using UnityEditor;
using UnityEditor.SceneManagement;

namespace CrystalMagic.Editor
{
    /// <summary>
    /// 确保每次 Play 都从 Start 场景开始，无论当前打开的是哪个场景
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromStartScene
    {
        private const string StartScenePath = "Assets/Scenes/Start.unity";

        static PlayFromStartScene()
        {
            EditorSceneManager.playModeStartScene =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(StartScenePath);
        }
    }
}
