using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;

[Flags]
public enum GameWorldKind
{
    None = 0,
    Town = 1 << 0,
    Dungeon = 1 << 1,
}

public enum GameSceneMode
{
    None = 0,
    Town = 1,
    Dungeon = 2,
    Training = 3,
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
    /// 菜单阶段不创建 ECS World。进入一局游戏后才由 GameWorldManager 显式创建唯一的 GameWorld。
    /// </summary>
    public sealed class GameWorldBootstrap : ICustomBootstrap
    {
        public bool Initialize(string defaultWorldName)
        {
            return true;
        }
    }

    /// <summary>
    /// 管理一次游戏会话唯一的 ECS World。
    /// Town、Dungeon、Training 只是同一 World 中不同的场景运行模式。
    /// </summary>
    public static class GameWorldManager
    {
        private const string WorldName = "GameWorld";

        private static World _gameWorld;
        private static List<Type> _gameSystemTypes;

        public static bool HasGameWorld => _gameWorld != null && _gameWorld.IsCreated;
        public static World GameWorld => HasGameWorld ? _gameWorld : null;
        public static GameSceneMode SceneMode { get; private set; }

        public static World CreateGameWorld()
        {
            if (HasGameWorld)
                return _gameWorld;

            _gameWorld = new World(WorldName, WorldFlags.Game);
            World.DefaultGameObjectInjectionWorld = _gameWorld;

            _gameSystemTypes = DefaultWorldInitialization
                .GetAllSystems(WorldSystemFilterFlags.Default)
                .Where(type => type.Assembly != typeof(GameWorldManager).Assembly || HasGameWorldAttribute(type))
                .ToList();
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(_gameWorld, _gameSystemTypes);
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(_gameWorld);
            UpdateSystemEnablement();
            return _gameWorld;
        }

        public static bool TryGetEntityManager(out EntityManager entityManager)
        {
            if (!HasGameWorld)
            {
                entityManager = default;
                return false;
            }

            entityManager = _gameWorld.EntityManager;
            return true;
        }

        public static void PrepareForSceneLoad(string sceneName)
        {
            if (!HasGameWorld)
                return;

            GameSceneMode targetMode = ResolveSceneMode(sceneName);
            if (targetMode == GameSceneMode.None)
            {
                ShutdownGameWorld();
                return;
            }

            SetSceneMode(targetMode);
        }

        public static void SetSceneMode(GameSceneMode sceneMode)
        {
            if (!HasGameWorld)
                return;

            SceneMode = sceneMode;
            UpdateSystemEnablement();
        }

        public static void ShutdownGameWorld()
        {
            if (!HasGameWorld)
            {
                SceneMode = GameSceneMode.None;
                return;
            }

            if (World.DefaultGameObjectInjectionWorld == _gameWorld)
                World.DefaultGameObjectInjectionWorld = null;

            ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(_gameWorld);
            _gameWorld.Dispose();
            _gameWorld = null;
            _gameSystemTypes = null;
            SceneMode = GameSceneMode.None;
        }

        public static void Shutdown()
        {
            ShutdownGameWorld();
        }

        private static GameSceneMode ResolveSceneMode(string sceneName)
        {
            if (sceneName == TownState.SceneName)
                return GameSceneMode.Town;

            if (sceneName == DungeonState.SceneName)
                return GameSceneMode.Dungeon;

            if (sceneName == TrainingState.SceneName)
                return GameSceneMode.Training;

            return GameSceneMode.None;
        }

        private static bool HasGameWorldAttribute(Type systemType)
        {
            return Attribute.IsDefined(systemType, typeof(RunInGameWorldAttribute));
        }

        private static void UpdateSystemEnablement()
        {
            if (!HasGameWorld || _gameSystemTypes == null)
                return;

            GameWorldKind activeKind = SceneMode == GameSceneMode.Town
                ? GameWorldKind.Town
                : SceneMode == GameSceneMode.Dungeon || SceneMode == GameSceneMode.Training
                    ? GameWorldKind.Dungeon
                    : GameWorldKind.None;

            for (int i = 0; i < _gameSystemTypes.Count; i++)
            {
                Type type = _gameSystemTypes[i];
                RunInGameWorldAttribute attribute = Attribute.GetCustomAttribute(type, typeof(RunInGameWorldAttribute)) as RunInGameWorldAttribute;
                if (attribute == null)
                    continue;

                ComponentSystemBase system = _gameWorld.GetExistingSystemManaged(type);
                if (system != null)
                    system.Enabled = (attribute.Worlds & activeKind) != 0;
            }
        }
    }
}
