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
        public SceneMapPresentation MapPresentation { get; } = new();

        public void ShowMapPresentation(string sceneName)
        {
            if (MapPresentation.SceneName == sceneName && MapPresentation.Root != null) return;
            MapPresentation.Clear();
            SceneMapLayout layout;
            if (sceneName == TownState.SceneName)
                layout = SceneMapLayout.Town;
            else if (sceneName == TrainingState.SceneName)
                layout = SceneMapLayout.Training;
            else return; // Dungeon registers its generated root after loading.

            string path = layout.PrefabPath;
            PoolComponent.Instance.EnsurePool(path, maxSize: 1);
            GameObject root = PoolComponent.Instance.Get(path);
            if (root == null) throw new System.InvalidOperationException($"Map prefab could not be loaded: {path}");
            int boundsOwner = root.GetInstanceID();
            MapPresentation.Replace(sceneName, root, () =>
            {
                // The camera may already be destroyed during application teardown.
                if (CameraComponent.TryGetInstance(out CameraComponent camera))
                    camera.ClearWorldBounds(boundsOwner);
                if (root != null) PoolComponent.Instance.Release(root);
                // Maps are large and exclusive: release the inactive instance and
                // resource reference, rather than caching every visited map.
                PoolComponent.Instance.DestroyPool(path);
            });
            layout.Apply(root.transform);
            CameraComponent.Instance.SetWorldBounds(boundsOwner, SceneMapLayout.GetCameraWorldBounds(root));
        }

        public void SynchronizeMapPresentation(GameState state)
        {
            if (state is TownState) ShowMapPresentation(TownState.SceneName);
            else if (state is TrainingState) ShowMapPresentation(TrainingState.SceneName);
            else if (state is DungeonState || state is OnlineBattleState || state is OnlineBattlePreparationState)
            {
                if (MapPresentation.SceneName != DungeonState.SceneName) MapPresentation.Clear();
            }
            else
            {
                MapPresentation.Clear();
                if (state is MainMenuState)
                    DeactivateUnselectedSubScenes(System.Array.Empty<string>());
            }
        }
#if UNITY_EDITOR
        private SubSceneImportWarmup _importWarmup;
        private int _warmupRequestedFrame = -1;
        private readonly Dictionary<int, AsyncOperation> _subSceneObjectUnloads = new();
#endif

        public override int Priority => 20;

        public void PreloadStandaloneWorld()
        {
            RequestWorldPreload(GameWorldRole.Standalone);
            PrewarmSubSceneImports();
        }

        public void PreloadOnlineWorlds()
        {
            RequestWorldPreload(GameWorldRole.Client);
            RequestWorldPreload(GameWorldRole.Server);
        }

        private void RequestWorldPreload(GameWorldRole role)
        {
            if (!TryGetSubSceneGuid(DungeonState.RegistrySubSceneName, out Unity.Entities.Hash128 guid))
                throw new System.InvalidOperationException("The common entity registry SubScene is unavailable.");
            GameWorldPreload.Request(role, guid);
        }

        public System.Collections.IEnumerator WaitForWorldPreloadCoroutine(GameWorldRole role)
        {
            RequestWorldPreload(role);
            using var timing = SceneLoadTiming.Measure($"Await prepared World: {role}");
            while (!GameWorldPreload.IsReady(role))
            {
                string error = GameWorldPreload.GetError(role);
                if (error != null)
                    throw new System.InvalidOperationException($"{role} World preload failed: {error}");
                yield return null;
            }
        }

        public void PrewarmSubSceneImports()
        {
#if UNITY_EDITOR
            if (_importWarmup == null && _warmupRequestedFrame < 0)
                _warmupRequestedFrame = Time.frameCount;
#endif
        }

        private void Update()
        {
            GameWorldPreload.Tick();
#if UNITY_EDITOR
            try
            {
                // Give the menu its first frame before starting the streaming World.
                if (_warmupRequestedFrame >= 0 && Time.frameCount > _warmupRequestedFrame)
                {
                    _warmupRequestedFrame = -1;
                    SubScene[] subScenes = Object.FindObjectsByType<SubScene>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    if (subScenes.Length > 0)
                    {
                        _importWarmup = new SubSceneImportWarmup();
                        foreach (SubScene subScene in subScenes)
                            _importWarmup.Request(subScene.SceneGUID, subScene.name);
                    }
                }

                if (_importWarmup == null)
                    return;

                _importWarmup.Tick();
                if (_importWarmup.IsComplete)
                    ReleaseImportWarmup();
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[SubSceneImportWarmup] Background preparation stopped: {exception}");
                ReleaseImportWarmup();
            }
#endif
        }

#if UNITY_EDITOR
        private static bool IsSubSceneHeaderPending(SubScene subScene)
        {
            World world = GameWorldManager.GameWorld;
            if (subScene == null || !subScene.SceneGUID.IsValid || world == null)
                return false;
            Entity scene = SceneSystem.GetSceneEntity(world.Unmanaged, subScene.SceneGUID);
            return scene != Entity.Null && world.EntityManager.HasComponent<RequestSceneLoaded>(scene) &&
                !world.EntityManager.HasBuffer<ResolvedSectionEntity>(scene);
        }

        private void ReleaseImportWarmup()
        {
            _warmupRequestedFrame = -1;
            _importWarmup?.Dispose();
            _importWarmup = null;
        }
#endif

        public void LoadScene(string sceneName)
        {
            if (_currentSceneName == sceneName)
            {
                Debug.LogWarning($"Scene '{sceneName}' is already loaded");
                return;
            }

            Debug.Log($"[SceneComponent] Loading scene: {sceneName}");
            MapPresentation.Clear();
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
            MapPresentation.Clear();
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
#if UNITY_EDITOR
            // Continue pumping the game World while the editor imports/resolves its
            // header. Content streaming gets its own budget after that phase finishes.
            bool waitingForImport = IsSubSceneHeaderPending(FindSubScene(subSceneName));
#endif
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

                float phaseTimeoutSeconds = timeoutSeconds;
#if UNITY_EDITOR
                if (waitingForImport && !IsSubSceneHeaderPending(targetSubScene))
                {
                    waitingForImport = false;
                    startTime = Time.realtimeSinceStartup;
                    SceneLoadTiming.Mark("SUBSCENE IMPORT COMPLETE", $"{subSceneName}; content streaming budget starts now.");
                }
                if (waitingForImport)
                    phaseTimeoutSeconds = Mathf.Max(timeoutSeconds, SubSceneImportWarmup.TimeoutSeconds);
#endif

                if (!hasLoggedWaiting)
                {
                    Debug.Log($"[SceneComponent] Waiting for SubScene: {subSceneName}");
                    hasLoggedWaiting = true;
                }

                if (timeoutSeconds > 0f && Time.realtimeSinceStartup - startTime >= phaseTimeoutSeconds)
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
                if (IsSubSceneFullyUnloaded(targetSubScene))
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

        private HashSet<string> DeactivateUnselectedSubScenes(IReadOnlyList<string> activeSubSceneNames)
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

            if (MapPresentation.SceneName == TownState.SceneName && !activeNames.Contains(TownState.SubSceneName) ||
                MapPresentation.SceneName == TrainingState.SceneName && !activeNames.Contains(TrainingState.SubSceneName) ||
                MapPresentation.SceneName == DungeonState.SceneName && !activeNames.Contains(DungeonState.RegistrySubSceneName))
                MapPresentation.Clear();

            SubScene[] subScenes = Object.FindObjectsByType<SubScene>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            // Request every old scene's unload before enabling a target scene.
            // FindObjectsByType has no ordering guarantee (Dungeon can precede Town).
            for (int i = 0; i < subScenes.Length; i++)
            {
                SubScene subScene = subScenes[i];
                if (subScene == null || subScene.gameObject == null)
                    continue;

                bool shouldBeActive = activeNames.Contains(subScene.name) || activeNames.Contains(subScene.gameObject.name);
                if (shouldBeActive) continue;
#if UNITY_EDITOR
                // Open SubScenes are separate Unity scenes. Disabling the SubScene
                // unloads ECS content, but leaves their authoring GameObjects alive.
                Scene editingScene = subScene.EditingScene;
                if (editingScene.IsValid() && editingScene.isLoaded &&
                    !_subSceneObjectUnloads.ContainsKey(editingScene.handle))
                {
                    AsyncOperation unload = UnloadSubSceneGameObjects(editingScene);
                    if (unload != null)
                        _subSceneObjectUnloads.Add(editingScene.handle, unload);
                }
#endif
                if (subScene.gameObject.activeSelf)
                {
                    using (SceneLoadTiming.Measure($"SubScene.SetActive {subScene.name} Active=False"))
                        subScene.gameObject.SetActive(false);
                }
                using (SceneLoadTiming.Measure($"Unload SubScene: {subScene.name}"))
                    UnloadSubScene(subScene);
            }
            return activeNames;
        }

        private void ActivateSubScenes(HashSet<string> activeNames)
        {
            SubScene[] subScenes = Object.FindObjectsByType<SubScene>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < subScenes.Length; i++)
            {
                SubScene subScene = subScenes[i];
                if (subScene == null || subScene.gameObject == null ||
                    !(activeNames.Contains(subScene.name) || activeNames.Contains(subScene.gameObject.name)))
                    continue;
                // Enabling SubScene loads into all Worlds. Keep common content
                // under explicit per-World ownership instead.
                if (subScene.name == DungeonState.RegistrySubSceneName)
                {
                    EnsureSubSceneRegistered(subScene);
                    continue;
                }
                if (!subScene.gameObject.activeSelf)
                {
                    using (SceneLoadTiming.Measure($"SubScene.SetActive {subScene.name} Active=True"))
                        subScene.gameObject.SetActive(true);
                }
                using (SceneLoadTiming.Measure($"Register / request SubScene: {subScene.name}"))
                    EnsureSubSceneRegistered(subScene);
            }
            GameWorldPreload.DiscardForeignScenes();
        }

        public System.Collections.IEnumerator SetSubScenesActiveCoroutine(IReadOnlyList<string> activeSubSceneNames)
        {
#if UNITY_EDITOR
            // SubScene enable/disable operates on every streaming World. Finish the
            // menu-only World before it can receive gameplay loads or lose its tracked
            // scene entities. Imported artifacts remain cached for the game World.
            if (_importWarmup != null || _warmupRequestedFrame >= 0)
                SceneLoadTiming.Mark("IMPORT WARMUP HANDOFF", "Game World now owns SubScene loading.");
            ReleaseImportWarmup();
#endif
            HashSet<string> activeNames = DeactivateUnselectedSubScenes(activeSubSceneNames);
#if UNITY_EDITOR
            foreach (AsyncOperation unload in _subSceneObjectUnloads.Values)
                if (!unload.isDone)
                    yield return unload;
            _subSceneObjectUnloads.Clear();
#endif
            ActivateSubScenes(activeNames);
            yield break;
        }

#if UNITY_EDITOR
        internal static AsyncOperation UnloadSubSceneGameObjects(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            // Hide sprites, colliders and behaviours immediately, before the
            // asynchronous unload completes on a subsequent frame.
            foreach (GameObject root in scene.GetRootGameObjects())
                root.SetActive(false);
            return SceneManager.UnloadSceneAsync(scene);
        }
#endif

        private static bool IsSubSceneFullyUnloaded(SubScene subScene)
        {
            if (subScene != null && GameWorldPreload.IsCommonScene(GameWorldManager.GameWorld, subScene.SceneGUID))
                return true;
#if UNITY_EDITOR
            if (subScene != null && subScene.IsLoaded)
                return false;
#endif
            return !IsSubSceneContentLoaded(subScene);
        }

        public string GetCurrentSceneName() => _currentSceneName;

        public override void Cleanup()
        {
            MapPresentation.Clear();
#if UNITY_EDITOR
            ReleaseImportWarmup();
            _subSceneObjectUnloads.Clear();
#endif
            _currentSceneName = null;
            GameWorldManager.Shutdown();
            base.Cleanup();
        }

        private void EnsureSubSceneRegistered(SubScene subScene)
        {
            World world = GameWorldManager.GameWorld;
            if (subScene == null || !subScene.SceneGUID.IsValid || world == null || !world.IsCreated)
                return;

            EntityManager entityManager = world.EntityManager;
            SceneSystem.LoadParameters loadParameters = default;
#if UNITY_EDITOR
            // SubScene.OnEnable uses this flag in Play Mode. An explicit request
            // must keep it: default parameters replace the existing request and
            // leave foreground loading dependent on the background importer.
            loadParameters.Flags = SceneLoadFlags.BlockOnImport;
#endif
            Entity sceneEntity = SceneSystem.GetSceneEntity(world.Unmanaged, subScene.SceneGUID);
            if (sceneEntity == Entity.Null)
            {
                sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, subScene.SceneGUID, loadParameters);
                if (sceneEntity == Entity.Null)
                    return;

                if (!entityManager.HasComponent<SubScene>(sceneEntity))
                    entityManager.AddComponentObject(sceneEntity, subScene);
                Debug.Log($"[SceneComponent] Registered SubScene in current World: {subScene.name}");
                return;
            }

            if (!SceneSystem.IsSceneLoaded(world.Unmanaged, sceneEntity))
            {
                if (entityManager.HasComponent<RequestSceneLoaded>(sceneEntity))
                    loadParameters.Flags |= entityManager.GetComponentData<RequestSceneLoaded>(sceneEntity).LoadFlags &
                        ~SceneLoadFlags.DisableAutoLoad;
                SceneSystem.LoadSceneAsync(world.Unmanaged, sceneEntity, loadParameters);
            }
        }

        private void UnloadSubScene(SubScene subScene)
        {
            if (subScene == null || !subScene.SceneGUID.IsValid)
                return;

            World world = GameWorldManager.GameWorld;
            if (GameWorldPreload.IsCommonScene(world, subScene.SceneGUID))
                return;
            UnloadSubScene(world, subScene.SceneGUID);
        }

        internal static void UnloadSubScene(World world, Unity.Entities.Hash128 sceneGuid)
        {
            if (world == null || !world.IsCreated || !sceneGuid.IsValid) return;
            // Clean auto-registered scenes too, including metadata left by a
            // previous World/enable cycle. The GUID overload unloads only one copy.
            Entity scene;
            while ((scene = SceneSystem.GetSceneEntity(world.Unmanaged, sceneGuid)) != Entity.Null)
                SceneSystem.UnloadScene(world.Unmanaged, scene, SceneSystem.UnloadParameters.DestroyMetaEntities);
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
