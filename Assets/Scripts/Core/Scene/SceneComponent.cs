using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Unity.Scenes;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace CrystalMagic.Core {

    public class SceneComponent : GameComponent<SceneComponent>
    {
        private string _currentSceneName;
        private readonly HashSet<Unity.Entities.Hash128> _explicitlyLoadedSubScenes = new();

        public override int Priority => 20;

        public void LoadScene(string sceneName)
        {
            if (_currentSceneName == sceneName)
            {
                Debug.LogWarning($"Scene '{sceneName}' is already loaded");
                return;
            }

            Debug.Log($"[SceneComponent] Loading scene: {sceneName}");
            GameWorldManager.PrepareForSceneLoad(sceneName);
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            _currentSceneName = sceneName;
        }

        public System.Collections.IEnumerator LoadSceneAsyncCoroutine(
            string sceneName,
            System.Action onComplete = null,
            bool forceReload = false,
            System.Action<float> onProgress = null)
        {
            using var timing = SceneLoadTiming.Measure($"Unity main scene load: {sceneName}");
            if (!forceReload && _currentSceneName == sceneName)
            {
                Debug.LogWarning($"Scene '{sceneName}' is already loaded");
                onProgress?.Invoke(1f);
                onComplete?.Invoke();
                yield break;
            }

            Debug.Log($"[SceneComponent] Loading scene async: {sceneName}");
            using (SceneLoadTiming.Measure($"Prepare World for {sceneName}"))
                GameWorldManager.PrepareForSceneLoad(sceneName);

            AsyncOperation asyncLoad;
            using (SceneLoadTiming.Measure($"Request Unity scene load: {sceneName}"))
                asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

            while (!asyncLoad.isDone)
            {
                float progress = asyncLoad.progress >= 0.9f
                    ? 1f
                    : Mathf.Clamp01(asyncLoad.progress / 0.9f);
                onProgress?.Invoke(progress);
                yield return null;
            }

            _currentSceneName = sceneName;
            onProgress?.Invoke(1f);
            Debug.Log($"[SceneComponent] Scene loaded: {sceneName}");
            onComplete?.Invoke();
        }

        public System.Collections.IEnumerator LoadAdditiveSceneAsyncCoroutine(string sceneName, System.Action onComplete = null)
        {
            using var timing = SceneLoadTiming.Measure($"Unity additive scene load: {sceneName}");
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                onComplete?.Invoke();
                yield break;
            }

            Scene loadedScene = SceneManager.GetSceneByName(sceneName);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                Debug.LogWarning($"[SceneComponent] Additive scene '{sceneName}' is already loaded");
                onComplete?.Invoke();
                yield break;
            }

            Debug.Log($"[SceneComponent] Loading additive scene async: {sceneName}");
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            Debug.Log($"[SceneComponent] Additive scene loaded: {sceneName}");
            onComplete?.Invoke();
        }

        public System.Collections.IEnumerator WaitForSubSceneLoadedCoroutine(string subSceneName, float timeoutSeconds = 10f)
        {
            if (string.IsNullOrEmpty(subSceneName))
                yield break;

            float startTime = Time.realtimeSinceStartup;
            bool hasLoggedWaiting = false;
            using var timing = SceneLoadTiming.Measure($"Wait SubScene load: {subSceneName} Timeout={timeoutSeconds}s");
            SubSceneLoadTiming diagnostics = new(subSceneName);
            diagnostics.Observe(FindSubScene(subSceneName));

            while (true)
            {
                long updateStarted = Stopwatch.GetTimestamp();
                GameWorldManager.UpdateGameWorld();
                double updateMilliseconds = (Stopwatch.GetTimestamp() - updateStarted) * 1000d / Stopwatch.Frequency;
                SubScene targetSubScene = FindSubScene(subSceneName);
                diagnostics.Observe(targetSubScene, updateMilliseconds);
                if (IsSubSceneContentLoaded(targetSubScene))
                {
                    diagnostics.Complete("Loaded");
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
                    diagnostics.Complete("Timeout");
                    Debug.LogWarning($"[SceneComponent] Wait SubScene timeout: {subSceneName}");
                    yield break;
                }

                yield return null;
            }
        }

        public System.Collections.IEnumerator WaitForSubSceneUnloadedCoroutine(string subSceneName, float timeoutSeconds = 10f)
        {
            if (string.IsNullOrEmpty(subSceneName))
                yield break;

            float startTime = Time.realtimeSinceStartup;
            bool hasLoggedWaiting = false;
            using var timing = SceneLoadTiming.Measure($"Wait SubScene unload: {subSceneName} Timeout={timeoutSeconds}s");

            while (true)
            {
                GameWorldManager.UpdateGameWorld();
                SubScene targetSubScene = FindSubScene(subSceneName);
                if (!IsSubSceneContentLoaded(targetSubScene))
                {
                    Debug.Log($"[SceneComponent] SubScene unloaded: {subSceneName}");
                    yield break;
                }

                if (!hasLoggedWaiting)
                {
                    Debug.Log($"[SceneComponent] Waiting for SubScene unload: {subSceneName}");
                    hasLoggedWaiting = true;
                }

                if (timeoutSeconds > 0f && Time.realtimeSinceStartup - startTime >= timeoutSeconds)
                {
                    SceneLoadTiming.Mark("SUBSCENE UNLOAD TIMEOUT", subSceneName);
                    Debug.LogWarning($"[SceneComponent] Wait SubScene unload timeout: {subSceneName}");
                    yield break;
                }

                yield return null;
            }
        }

        public bool IsSubSceneLoaded(string subSceneName)
        {
            return IsSubSceneContentLoaded(FindSubScene(subSceneName));
        }

        public bool TryGetSubSceneGuid(string subSceneName, out Unity.Entities.Hash128 sceneGuid)
        {
            SubScene targetSubScene = FindSubScene(subSceneName);
            if (targetSubScene == null || !targetSubScene.SceneGUID.IsValid)
            {
                sceneGuid = default;
                return false;
            }

            sceneGuid = targetSubScene.SceneGUID;
            return true;
        }

        public void SetSubScenesActive(IReadOnlyList<string> activeSubSceneNames)
        {
            HashSet<string> activeNames = new();
            if (activeSubSceneNames != null)
            {
                for (int i = 0; i < activeSubSceneNames.Count; i++)
                {
                    string subSceneName = activeSubSceneNames[i];
                    if (!string.IsNullOrWhiteSpace(subSceneName))
                    {
                        activeNames.Add(subSceneName);
                    }
                }
            }

            SubScene[] subScenes = Object.FindObjectsByType<SubScene>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < subScenes.Length; i++)
            {
                SubScene subScene = subScenes[i];
                if (subScene == null || subScene.gameObject == null)
                    continue;

                bool shouldBeActive = activeNames.Contains(subScene.name) || activeNames.Contains(subScene.gameObject.name);
                if (subScene.gameObject.activeSelf != shouldBeActive)
                {
                    using (SceneLoadTiming.Measure($"SubScene.SetActive {subScene.name} Active={shouldBeActive}"))
                        subScene.gameObject.SetActive(shouldBeActive);
                }

                if (shouldBeActive)
                {
                    using (SceneLoadTiming.Measure($"Register / request SubScene: {subScene.name}"))
                        EnsureSubSceneRegistered(subScene);
                }
                else
                {
                    using (SceneLoadTiming.Measure($"Unload explicit SubScene: {subScene.name}"))
                        UnloadExplicitSubScene(subScene);
                }
            }
        }

        public string GetCurrentSceneName() => _currentSceneName;

        public override void Cleanup()
        {
            _currentSceneName = null;
            _explicitlyLoadedSubScenes.Clear();
            GameWorldManager.Shutdown();
            base.Cleanup();
        }

        private void EnsureSubSceneRegistered(SubScene subScene)
        {
            World world = GameWorldManager.GameWorld;
            if (subScene == null || !subScene.SceneGUID.IsValid || world == null || !world.IsCreated)
                return;

            EntityManager entityManager = world.EntityManager;
            Entity sceneEntity = SceneSystem.GetSceneEntity(world.Unmanaged, subScene.SceneGUID);
            if (sceneEntity == Entity.Null)
            {
                sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, subScene.SceneGUID);
                if (sceneEntity == Entity.Null)
                    return;

                if (!entityManager.HasComponent<SubScene>(sceneEntity))
                    entityManager.AddComponentObject(sceneEntity, subScene);
                _explicitlyLoadedSubScenes.Add(subScene.SceneGUID);
                Debug.Log($"[SceneComponent] Registered SubScene in current World: {subScene.name}");
                return;
            }

            if (!SceneSystem.IsSceneLoaded(world.Unmanaged, sceneEntity))
            {
                SceneSystem.LoadSceneAsync(world.Unmanaged, sceneEntity);
            }
        }

        private void UnloadExplicitSubScene(SubScene subScene)
        {
            if (subScene == null || !_explicitlyLoadedSubScenes.Remove(subScene.SceneGUID))
                return;

            World world = GameWorldManager.GameWorld;
            if (world != null && world.IsCreated)
            {
                SceneSystem.UnloadScene(
                    world.Unmanaged,
                    subScene.SceneGUID,
                    SceneSystem.UnloadParameters.DestroyMetaEntities);
            }
        }

        private SubScene FindSubScene(string subSceneName)
        {
            SubScene[] subScenes = Object.FindObjectsByType<SubScene>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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

        private static bool IsSubSceneContentLoaded(SubScene subScene)
        {
            World world = GameWorldManager.GameWorld;
            if (subScene == null ||
                !subScene.SceneGUID.IsValid ||
                world == null ||
                !world.IsCreated)
            {
                return false;
            }

            Entity sceneEntity = SceneSystem.GetSceneEntity(world.Unmanaged, subScene.SceneGUID);
            return sceneEntity != Entity.Null && SceneSystem.IsSceneLoaded(world.Unmanaged, sceneEntity);
        }
    }
}
