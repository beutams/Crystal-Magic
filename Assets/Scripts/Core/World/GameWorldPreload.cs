using System;
using System.Collections.Generic;
using CrystalMagic.Game.Unit;
using Server;
using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;
using Hash128 = Unity.Entities.Hash128;

namespace CrystalMagic.Core
{
    // Each role keeps its own entity references and registries. A parked World is
    // never the default World and never runs gameplay or presentation systems.
    internal static class GameWorldPreload
    {
        internal const double TimeoutSeconds = 90;
        private sealed class Entry
        {
            public GameWorldRole Role;
            public Hash128 RegistryGuid;
            public World World;
            public bool Leased;
            public bool Ready;
            public string Error;
            public Stopwatch Clock;
            public int RequestedFrame;
        }

        private static readonly Dictionary<GameWorldRole, Entry> Entries = new();

        internal static void Request(GameWorldRole role, Hash128 registryGuid)
        {
            if (role == GameWorldRole.None || !registryGuid.IsValid)
                throw new ArgumentException("A preload needs a game role and the common registry scene GUID.");
            if (Entries.TryGetValue(role, out Entry existing))
            {
                if (existing.RegistryGuid != registryGuid)
                    throw new InvalidOperationException("The common registry cannot change while a World is retained.");
                if (existing.Error == null)
                {
                    if (!existing.Leased && existing.Ready && !IsRegistryLoaded(existing))
                    {
                        existing.Ready = false;
                        existing.Clock.Restart();
                        SceneSystem.LoadSceneAsync(existing.World.Unmanaged, existing.RegistryGuid);
                    }
                    return;
                }
                Entries.Remove(role);
            }
            Entries.Add(role, new Entry { Role = role, RegistryGuid = registryGuid, RequestedFrame = Time.frameCount });
            Debug.Log($"[WorldPreload] Queued: {role}");
        }

        internal static bool IsReady(GameWorldRole role) =>
            Entries.TryGetValue(role, out Entry entry) && entry.Ready;

        internal static string GetError(GameWorldRole role) =>
            Entries.TryGetValue(role, out Entry entry) ? entry.Error : null;

        internal static World GetWorld(GameWorldRole role) =>
            Entries.TryGetValue(role, out Entry entry) ? entry.World : null;

        internal static bool IsCommonScene(World world, Hash128 guid)
        {
            if (world == null) return false;
            foreach (Entry entry in Entries.Values)
                if (entry.World == world && entry.RegistryGuid == guid)
                    return true;
            return false;
        }

        internal static void Tick()
        {
            // Creating systems compiles the state-script/behaviour registries.
            // Start at most one role per frame, after the UI has appeared.
            bool created = false;
            foreach (Entry entry in Entries.Values)
            {
                if (entry.Leased || entry.Ready || entry.Error != null)
                    continue;
                try
                {
                    if (entry.World == null)
                    {
                        if (Application.isPlaying && Time.frameCount <= entry.RequestedFrame)
                            continue;
                        if (created) continue;
                        created = true;
                        Create(entry);
                    }
                    // SubScene.OnEnable registers in ALL Worlds. Remove those
                    // foreign scene requests before any streaming update.
                    UnloadSceneInstances(entry);
                    entry.World.GetExistingSystemManaged<InitializationSystemGroup>()?.Update();
                    entry.World.GetExistingSystemManaged<PlayerSkillDefinitionRegistryInitializationSystem>()?.Update();
                    if (IsRegistryLoaded(entry))
                    {
                        entry.Ready = true;
                        Debug.Log($"[WorldPreload] Ready: {entry.Role} ({entry.Clock.Elapsed.TotalSeconds:F2}s)");
                    }
                    else if (entry.Clock.Elapsed.TotalSeconds >= TimeoutSeconds)
                        throw new TimeoutException($"{entry.Role} common registry preload timed out.");
                }
                catch (Exception exception)
                {
                    entry.Error = exception.Message;
                    entry.World?.Dispose();
                    entry.World = null;
                    Debug.LogException(exception);
                }
            }
        }

        private static void Create(Entry entry)
        {
            entry.Clock = Stopwatch.StartNew();
            entry.World = GameWorldManager.CreateConfiguredWorld(entry.Role, $"PreparedWorld_{entry.Role}");
            if (entry.Role != GameWorldRole.Standalone)
                BattleSimulationSystemGroup.Prepare(entry.World, entry.Role == GameWorldRole.Server);
            SetParked(entry.World, true);
            SceneSystem.LoadSceneAsync(entry.World.Unmanaged, entry.RegistryGuid);
        }

        private static bool IsRegistryLoaded(Entry entry)
        {
            Entity scene = SceneSystem.GetSceneEntity(entry.World.Unmanaged, entry.RegistryGuid);
            return scene != Entity.Null && SceneSystem.IsSceneLoaded(entry.World.Unmanaged, scene) &&
                EntitySpawnRegistryUtility.HasRegistry(entry.World.EntityManager);
        }

        internal static bool TryTake(GameWorldRole role, out World world)
        {
            world = null;
            if (!Entries.TryGetValue(role, out Entry entry) || entry.Leased)
                return false;
            if (entry.Error != null)
                throw new InvalidOperationException($"{role} World preload failed: {entry.Error}");
            // A quick Start click joins the same preload, never builds a second World.
            if (entry.World == null)
                Create(entry);
            entry.Leased = true;
            SetParked(entry.World, false);
            world = entry.World;
            Debug.Log($"[WorldPreload] Reused: {role}, ready={entry.Ready}");
            return true;
        }

        internal static bool TryReturn(World world)
        {
            foreach (Entry entry in Entries.Values)
            {
                if (entry.World != world || !entry.Leased)
                    continue;
                BattleSceneResetUtility.Reset(world);
                // Projectiles, summons and drops created during play are not
                // necessarily tagged as initial dungeon entities. Every registry
                // instance has this flag, including its disabled/alive state.
                using (EntityQuery instances = world.EntityManager.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<DestroyEntityFlag>() },
                    Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IgnoreComponentEnabledState,
                }))
                {
                    using NativeArray<Entity> entities = instances.ToEntityArray(Allocator.Temp);
                    foreach (Entity entity in entities)
                        if (world.EntityManager.Exists(entity))
                            world.EntityManager.DestroyEntity(entity);
                }
                UnloadSceneInstances(entry);
                using (EntityQuery state = world.EntityManager.CreateEntityQuery(new EntityQueryDesc
                {
                    Any = new[] { ComponentType.ReadOnly<StashComponent>(), ComponentType.ReadOnly<DungeonRunComponent>(),
                        ComponentType.ReadOnly<DungeonRuntimeMapComponent>() },
                    Options = EntityQueryOptions.IncludeDisabledEntities,
                }))
                    world.EntityManager.DestroyEntity(state);
                using (EntityQuery query = world.EntityManager.CreateEntityQuery(typeof(FrameManagerComponent)))
                    if (!query.IsEmptyIgnoreFilter)
                        FrameManagerUtility.Bind(world.EntityManager, null);
                GameSingletonUtility.Set(world.EntityManager, new GameWorldContextComponent
                { Role = entry.Role, SceneMode = GameSceneMode.None });
                SetParked(world, true);
                entry.Leased = false;
                entry.Ready = IsRegistryLoaded(entry);
                Debug.Log($"[WorldPreload] Parked: {entry.Role}");
                return true;
            }
            return false;
        }

        private static void UnloadSceneInstances(Entry entry)
        {
            EntityManager manager = entry.World.EntityManager;
            using EntityQuery query = manager.CreateEntityQuery(typeof(SceneReference));
            using NativeArray<Entity> scenes = query.ToEntityArray(Allocator.Temp);
            foreach (Entity scene in scenes)
            {
                if (!manager.Exists(scene) || manager.GetComponentData<SceneReference>(scene).SceneGUID == entry.RegistryGuid)
                    continue;
                SceneSystem.UnloadScene(entry.World.Unmanaged, scene, SceneSystem.UnloadParameters.DestroyMetaEntities);
            }
        }

        internal static void DiscardForeignScenes()
        {
            foreach (Entry entry in Entries.Values)
                if (entry.World != null && entry.World.IsCreated && (!entry.Leased || entry.Role == GameWorldRole.Server))
                    UnloadSceneInstances(entry);
        }

        private static void SetParked(World world, bool parked)
        {
            SimulationSystemGroup simulation = world.GetExistingSystemManaged<SimulationSystemGroup>();
            if (simulation != null) simulation.Enabled = !parked;
            SetPresentation(world, !parked && (world.Flags & WorldFlags.GameServer) != WorldFlags.GameServer);
        }

        internal static void SetPresentation(World world, bool enabled)
        {
            PresentationSystemGroup presentation = world.GetExistingSystemManaged<PresentationSystemGroup>();
            if (presentation != null) presentation.Enabled = enabled;
            Type activationType = typeof(Baker<>).Assembly.GetType("Unity.Entities.CompanionGameObjectUpdateSystem", true);
            SystemHandle activation = world.GetExistingSystem(activationType);
            if (activation != SystemHandle.Null)
                world.Unmanaged.ResolveSystemStateRef(activation).Enabled = enabled;
            SystemHandle transforms = world.GetExistingSystem<CompanionGameObjectUpdateTransformSystem>();
            if (transforms != SystemHandle.Null)
                world.Unmanaged.ResolveSystemStateRef(transforms).Enabled = enabled;
        }

        internal static void Forget(World world)
        {
            GameWorldRole role = GameWorldRole.None;
            foreach (Entry entry in Entries.Values)
                if (entry.World == world) role = entry.Role;
            if (role != GameWorldRole.None) Entries.Remove(role);
        }

        internal static void Dispose()
        {
            foreach (Entry entry in Entries.Values)
                if (!entry.Leased && entry.World != null && entry.World.IsCreated)
                    entry.World.Dispose();
            Entries.Clear();
        }
    }
}
