using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;

[Flags]
public enum GameWorldKind
{
    None = 0,
    Default = 1 << 0,
    Town = 1 << 1,
    Dungeon = 1 << 2,
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class RunInGameWorldAttribute : Attribute
{
    public RunInGameWorldAttribute(GameWorldKind worlds)
    {
        Worlds = worlds;
    }

    public GameWorldKind Worlds { get; }
}

namespace CrystalMagic.Core
{
    /// <summary>
    /// Prevents gameplay systems from being created in Unity's automatic default world.
    /// </summary>
    public sealed class GameWorldBootstrap : ICustomBootstrap
    {
        public bool Initialize(string defaultWorldName)
        {
            GameWorldManager.EnsureDefaultWorld();
            return true;
        }
    }

    /// <summary>
    /// Owns the currently loaded gameplay world and switches it before scene loading begins.
    /// </summary>
    public static class GameWorldManager
    {
        private const string DefaultWorldName = "DefaultWorld";
        private const string TownWorldName = "TownWorld";
        private const string DungeonWorldName = "DungeonWorld";

        private static World _defaultWorld;
        private static World _activeGameWorld;

        public static GameWorldKind ActiveKind { get; private set; } = GameWorldKind.Default;

        public static void EnsureDefaultWorld()
        {
            if (_defaultWorld != null && _defaultWorld.IsCreated)
            {
                if (World.DefaultGameObjectInjectionWorld == null)
                    World.DefaultGameObjectInjectionWorld = _defaultWorld;
                return;
            }

            _defaultWorld = new World(DefaultWorldName, WorldFlags.Game);
            World.DefaultGameObjectInjectionWorld = _defaultWorld;
            ActiveKind = GameWorldKind.Default;
        }

        public static void PrepareForSceneLoad(string sceneName)
        {
            EnsureDefaultWorld();

            GameWorldKind targetKind = ResolveWorldKind(sceneName);
            DisposeWorld(ref _activeGameWorld);

            if (targetKind == GameWorldKind.Default)
            {
                World.DefaultGameObjectInjectionWorld = _defaultWorld;
                ActiveKind = GameWorldKind.Default;
                return;
            }

            _activeGameWorld = CreateGameWorld(targetKind);
            World.DefaultGameObjectInjectionWorld = _activeGameWorld;
            ActiveKind = targetKind;
        }

        public static void Shutdown()
        {
            if (World.DefaultGameObjectInjectionWorld == _activeGameWorld)
                World.DefaultGameObjectInjectionWorld = _defaultWorld;

            DisposeWorld(ref _activeGameWorld);

            if (World.DefaultGameObjectInjectionWorld == _defaultWorld)
                World.DefaultGameObjectInjectionWorld = null;

            DisposeWorld(ref _defaultWorld);
            ActiveKind = GameWorldKind.Default;
        }

        private static GameWorldKind ResolveWorldKind(string sceneName)
        {
            if (sceneName == TownState.SceneName)
                return GameWorldKind.Town;

            // Training shares the combat runtime and therefore reuses DungeonWorld.
            if (sceneName == DungeonState.SceneName || sceneName == TrainingState.SceneName)
                return GameWorldKind.Dungeon;

            return GameWorldKind.Default;
        }

        private static World CreateGameWorld(GameWorldKind worldKind)
        {
            string worldName = worldKind == GameWorldKind.Town ? TownWorldName : DungeonWorldName;
            World world = new(worldName, WorldFlags.Game);
            World.DefaultGameObjectInjectionWorld = world;
            List<Type> systemTypes = DefaultWorldInitialization
                .GetAllSystems(WorldSystemFilterFlags.Default)
                .Where(type => type.Assembly != typeof(GameWorldManager).Assembly || RunsInWorld(type, worldKind))
                .ToList();

            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systemTypes);
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
            return world;
        }

        private static void DisposeWorld(ref World world)
        {
            if (world == null)
                return;

            if (world.IsCreated)
            {
                ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(world);
                world.Dispose();
            }

            world = null;
        }

        private static bool RunsInWorld(Type systemType, GameWorldKind worldKind)
        {
            RunInGameWorldAttribute attribute = Attribute.GetCustomAttribute(
                systemType,
                typeof(RunInGameWorldAttribute)) as RunInGameWorldAttribute;
            return attribute != null && (attribute.Worlds & worldKind) != 0;
        }
    }
}
