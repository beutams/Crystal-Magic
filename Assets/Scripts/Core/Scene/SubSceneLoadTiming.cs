using System;
using Unity.Entities;
using Unity.Scenes;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace CrystalMagic.Core
{
    /// <summary>Observes public ECS streaming state without forcing imports or retaining ECS buffers.</summary>
    internal sealed class SubSceneLoadTiming
    {
        private readonly string _name;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private string _lastState;
        private bool _hasLoggedIdentity;
        private double _stateStarted;
        private double _lastPoll;
        private double _nextReport;
        private double _maxPollInterval;
        private double _worldUpdateTotal;
        private double _maxWorldUpdate;
        private int _polls;

        public SubSceneLoadTiming(string name) => _name = name;

        public void Observe(SubScene subScene, double worldUpdateMilliseconds = 0)
        {
            double now = _clock.Elapsed.TotalMilliseconds;
            double pollInterval = now - _lastPoll;
            _maxPollInterval = Math.Max(_maxPollInterval, pollInterval);
            _worldUpdateTotal += worldUpdateMilliseconds;
            _maxWorldUpdate = Math.Max(_maxWorldUpdate, worldUpdateMilliseconds);
            _polls++;

            World world = GameWorldManager.GameWorld;
            if (!_hasLoggedIdentity && subScene != null)
            {
                _hasLoggedIdentity = true;
                string identity = $"{_name} Guid={subScene.SceneGUID} World={world?.Name ?? "None"} Role={GameWorldManager.Role}";
#if UNITY_EDITOR
                identity += $" Asset={UnityEditor.AssetDatabase.GUIDToAssetPath(subScene.SceneGUID.ToString())}";
#endif
                SceneLoadTiming.Mark("SUBSCENE", identity);
            }

            string state;
            if (subScene == null)
                state = "MissingSubScene";
            else if (!subScene.SceneGUID.IsValid)
                state = "InvalidSceneGuid";
            else if (world == null || !world.IsCreated)
                state = "MissingWorld";
            else
                state = DescribeScene(world, SceneSystem.GetSceneEntity(world.Unmanaged, subScene.SceneGUID));

            if (state != _lastState)
            {
                if (_lastState != null)
                    SceneLoadTiming.Mark("SUBSCENE STATE END", $"{_name} {_lastState} ObservedDuration={SceneLoadTiming.Milliseconds(now - _stateStarted)}");
                _lastState = state;
                _stateStarted = now;
                _nextReport = 0;
            }

            if (now >= _nextReport)
            {
                string detail = $"{_name} State={state} Wait={SceneLoadTiming.Milliseconds(now)} " +
                    $"StateElapsed={SceneLoadTiming.Milliseconds(now - _stateStarted)} " +
                    $"PollInterval={SceneLoadTiming.Milliseconds(pollInterval)} " +
                    $"MaxPollInterval={SceneLoadTiming.Milliseconds(_maxPollInterval)} " +
                    $"ManualWorldUpdate={SceneLoadTiming.Milliseconds(worldUpdateMilliseconds)}";
#if UNITY_EDITOR
                detail += $" EditorUpdating={UnityEditor.EditorApplication.isUpdating} EditorCompiling={UnityEditor.EditorApplication.isCompiling} EditorPaused={UnityEditor.EditorApplication.isPaused}";
#endif
                SceneLoadTiming.Mark("SUBSCENE WAIT", detail);
                _nextReport = now + 1000;
            }
            _lastPoll = now;
        }

        public void Complete(string result)
        {
            double now = _clock.Elapsed.TotalMilliseconds;
            SceneLoadTiming.Mark("SUBSCENE RESULT", $"{_name} Result={result} Wait={SceneLoadTiming.Milliseconds(now)} " +
                $"State={_lastState} StateElapsed={SceneLoadTiming.Milliseconds(now - _stateStarted)} Polls={_polls} " +
                $"MaxPollInterval={SceneLoadTiming.Milliseconds(_maxPollInterval)} " +
                $"ManualWorldUpdateTotal={SceneLoadTiming.Milliseconds(_worldUpdateTotal)} " +
                $"MaxManualWorldUpdate={SceneLoadTiming.Milliseconds(_maxWorldUpdate)}");
        }

        internal static string DescribeScene(World world, Entity sceneEntity)
        {
            EntityManager manager = world.EntityManager;
            if (sceneEntity == Entity.Null || !manager.Exists(sceneEntity))
                return "AwaitingSceneRegistration";

            bool requested = manager.HasComponent<RequestSceneLoaded>(sceneEntity);
            string flags = requested ? manager.GetComponentData<RequestSceneLoaded>(sceneEntity).LoadFlags.ToString() : "None";
            // GetSceneStreamingState assumes a section buffer exists when there is no load request.
            if (!manager.HasBuffer<ResolvedSectionEntity>(sceneEntity))
                return requested ? $"ImportAndHeader Flags={flags}" : "NotRequested";

            DynamicBuffer<ResolvedSectionEntity> sections = manager.GetBuffer<ResolvedSectionEntity>(sceneEntity, true);
            int loaded = 0, loading = 0, pending = 0, failed = 0, unloaded = 0;
            foreach (ResolvedSectionEntity section in sections)
            {
                switch (SceneSystem.GetSectionStreamingState(world.Unmanaged, section.SectionEntity))
                {
                    case SceneSystem.SectionStreamingState.Loaded: loaded++; break;
                    case SceneSystem.SectionStreamingState.Loading: loading++; break;
                    case SceneSystem.SectionStreamingState.LoadRequested: pending++; break;
                    case SceneSystem.SectionStreamingState.FailedToLoad: failed++; break;
                    default: unloaded++; break;
                }
            }
            return $"{SceneSystem.GetSceneStreamingState(world.Unmanaged, sceneEntity)} Flags={flags} " +
                $"Sections={sections.Length} Loaded={loaded} Loading={loading} Pending={pending} Failed={failed} Unloaded={unloaded}";
        }
    }
}
