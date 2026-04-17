using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Scenes;

namespace CrystalMagic.Core {

    public class SceneComponent : GameComponent<SceneComponent>
    {
        private string _currentSceneName;

        public override int Priority => 20;

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

        public System.Collections.IEnumerator WaitForSubSceneLoadedCoroutine(string subSceneName, float timeoutSeconds = 10f)
        {
            if (string.IsNullOrEmpty(subSceneName))
                yield break;

            float startTime = Time.realtimeSinceStartup;
            bool hasLoggedWaiting = false;

            while (true)
            {
                SubScene targetSubScene = FindSubScene(subSceneName);
                if (targetSubScene != null && targetSubScene.IsLoaded)
                {
                    Debug.Log($"[SceneComponent] SubScene loaded: {subSceneName}");
                    yield break;
                }

                if (!hasLoggedWaiting)
                {
                    Debug.Log($"[SceneComponent] Waiting for SubScene: {subSceneName}");
                    hasLoggedWaiting = true;
                }

                if (timeoutSeconds > 0f && Time.realtimeSinceStartup - startTime >= timeoutSeconds)
                {
                    Debug.LogWarning($"[SceneComponent] Wait SubScene timeout: {subSceneName}");
                    yield break;
                }

                yield return null;
            }
        }

        public bool IsSubSceneLoaded(string subSceneName)
        {
            SubScene targetSubScene = FindSubScene(subSceneName);
            return targetSubScene != null && targetSubScene.IsLoaded;
        }

        public string GetCurrentSceneName() => _currentSceneName;

        public override void Cleanup()
        {
            _currentSceneName = null;
            base.Cleanup();
        }

        private SubScene FindSubScene(string subSceneName)
        {
            SubScene[] subScenes = Object.FindObjectsOfType<SubScene>(true);
            for (int i = 0; i < subScenes.Length; i++)
            {
                SubScene subScene = subScenes[i];
                if (subScene == null)
                    continue;

                if (subScene.name == subSceneName || subScene.gameObject.name == subSceneName)
                    return subScene;
            }

            return null;
        }
    }
}
