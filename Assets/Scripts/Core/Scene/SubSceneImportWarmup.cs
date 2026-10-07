#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Scenes;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace CrystalMagic.Core
{
    /// <summary>Builds editor entity-scene artifacts without loading gameplay entities.</summary>
    internal sealed class SubSceneImportWarmup : IDisposable
    {
        internal const float TimeoutSeconds = 90f;

        private readonly Dictionary<Hash128, (Entity Entity, string Name)> _pending = new();
        private readonly List<Hash128> _finished = new();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        internal World World { get; private set; }
        public bool IsComplete => _pending.Count == 0;

        public SubSceneImportWarmup()
        {
            // Never replace the default World or install gameplay systems in this World.
            World = new World("SubScene Import Warmup", WorldFlags.Streaming);
            try
            {
                DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(
                    World, DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Streaming));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Request(Hash128 guid, string name)
        {
            if (!guid.IsValid || _pending.ContainsKey(guid))
                return;

            Entity entity = SceneSystem.LoadSceneAsync(World.Unmanaged, guid,
                new SceneSystem.LoadParameters { Flags = SceneLoadFlags.DisableAutoLoad });
            _pending.Add(guid, (entity, name));
            Debug.Log($"[SubSceneImportWarmup] Queued: {name}");
        }

        public bool IsPending(Hash128 guid) => _pending.ContainsKey(guid);

        public void Tick()
        {
            World.Update();
            _finished.Clear();
            foreach (var pair in _pending)
            {
                Entity entity = pair.Value.Entity;
                // SubScene.OnDisable unloads metadata in every World, including this
                // one. A cancelled request must not remain pending for 90 seconds.
                if (!World.EntityManager.Exists(entity) ||
                    !World.EntityManager.HasComponent<RequestSceneLoaded>(entity))
                {
                    Debug.Log($"[SubSceneImportWarmup] Cancelled: {pair.Value.Name}");
                    _finished.Add(pair.Key);
                }
                else if (World.EntityManager.HasBuffer<ResolvedSectionEntity>(entity))
                {
                    int count = World.EntityManager.GetBuffer<ResolvedSectionEntity>(entity, true).Length;
                    if (count > 0)
                        Debug.Log($"[SubSceneImportWarmup] Ready: {pair.Value.Name} ({_clock.Elapsed.TotalSeconds:F2}s)");
                    else
                        Debug.LogWarning($"[SubSceneImportWarmup] Failed to resolve: {pair.Value.Name}; normal scene loading will report the failure.");
                    _finished.Add(pair.Key);
                }
                else if (_clock.Elapsed.TotalSeconds >= TimeoutSeconds)
                {
                    Debug.LogWarning($"[SubSceneImportWarmup] Timed out: {pair.Value.Name}");
                    _finished.Add(pair.Key);
                }
            }
            foreach (Hash128 guid in _finished)
                _pending.Remove(guid);
        }

        public void Dispose()
        {
            if (World != null && World.IsCreated)
                World.Dispose();
            World = null;
            _pending.Clear();
        }
    }
}
#endif
