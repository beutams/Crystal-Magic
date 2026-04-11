using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrystalMagic.Core {
    /// <summary>
    /// 场景路由组件
    /// 职责：场景加载/卸载
    /// </summary>
    public class SceneComponent : GameComponent<SceneComponent>
    {
        private string _currentSceneName;

        public override int Priority => 20;

        /// <summary>
        /// 同步加载场景
        /// </summary>
        public void LoadScene(string sceneName)
        {
            if (_currentSceneName == sceneName)
            {
                Debug.LogWarning($"Scene '{sceneName}' is already loaded");
                return;
            }

            Debug.Log($"[SceneComponent] Loading scene: {sceneName}");
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            _currentSceneName = sceneName;
        }

        /// <summary>
        /// 异步加载场景协程
        /// </summary>
        public System.Collections.IEnumerator LoadSceneAsyncCoroutine(string sceneName, System.Action onComplete = null)
        {
            if (_currentSceneName == sceneName)
            {
                Debug.LogWarning($"Scene '{sceneName}' is already loaded");
                onComplete?.Invoke();
                yield break;
            }

            Debug.Log($"[SceneComponent] Loading scene async: {sceneName}");
            
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            
            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            _currentSceneName = sceneName;
            Debug.Log($"[SceneComponent] Scene loaded: {sceneName}");
            onComplete?.Invoke();
        }

        public string GetCurrentSceneName() => _currentSceneName;

        public override void Cleanup()
        {
            _currentSceneName = null;
            base.Cleanup();
        }
    }
}
