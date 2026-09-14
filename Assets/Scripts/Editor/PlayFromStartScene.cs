using UnityEditor;
using UnityEditor.SceneManagement;

namespace CrystalMagic.Editor
{
    /// <summary>
    /// 取消强制从 Start 场景播放，以便直接运行当前打开的测试场景。
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromStartScene
    {
        // private const string StartScenePath = "Assets/Scenes/Start.unity";

        static PlayFromStartScene()
        {
            // EditorSceneManager.playModeStartScene =
            //     AssetDatabase.LoadAssetAtPath<SceneAsset>(StartScenePath);
            EditorSceneManager.playModeStartScene = null;
        }
    }
}
