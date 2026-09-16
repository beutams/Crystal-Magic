using System;
using Unity.Entities;

public enum GameSceneMode
{
    None = 0,
    Town = 1,
    Dungeon = 2,
    Training = 3,
}

public enum GameWorldRole
{
    None = 0,
    Standalone = 1,
    Server = 2,
    Client = 3,
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
        private static bool _appendedToPlayerLoop;

        public static bool HasGameWorld => _gameWorld != null && _gameWorld.IsCreated;
        public static World GameWorld => HasGameWorld ? _gameWorld : null;
        public static GameSceneMode SceneMode { get; private set; }
        public static GameWorldRole Role { get; private set; }

        public static World CreateGameWorld()
        {
            return CreateGameWorld(GameWorldRole.Standalone);
        }

        public static World CreateGameWorld(GameWorldRole role)
        {
            if (role == GameWorldRole.None)
                throw new ArgumentOutOfRangeException(nameof(role));

            if (HasGameWorld)
            {
                if (Role != role)
                    throw new InvalidOperationException($"GameWorld is already running as {Role}, cannot change it to {role}.");

                return _gameWorld;
            }

            WorldFlags worldFlags = role switch
            {
                GameWorldRole.Server => WorldFlags.GameServer,
                GameWorldRole.Client => WorldFlags.GameClient,
                _ => WorldFlags.Game,
            };
            WorldSystemFilterFlags systemFilter = role switch
            {
                GameWorldRole.Server => WorldSystemFilterFlags.Default | WorldSystemFilterFlags.ServerSimulation,
                GameWorldRole.Client => WorldSystemFilterFlags.Default | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Presentation,
                _ => WorldSystemFilterFlags.Default | WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.Presentation,
            };

            _gameWorld = new World(WorldName, worldFlags);
            Role = role;
            World.DefaultGameObjectInjectionWorld = _gameWorld;

            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(
                _gameWorld,
                DefaultWorldInitialization.GetAllSystems(systemFilter));
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(_gameWorld);
            _appendedToPlayerLoop = true;
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
        }

        public static void ShutdownGameWorld()
        {
            if (!HasGameWorld)
            {
                SceneMode = GameSceneMode.None;
                Role = GameWorldRole.None;
                return;
            }

            if (World.DefaultGameObjectInjectionWorld == _gameWorld)
                World.DefaultGameObjectInjectionWorld = null;

            if (_appendedToPlayerLoop)
                ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(_gameWorld);

            _gameWorld.Dispose();
            _gameWorld = null;
            _appendedToPlayerLoop = false;
            SceneMode = GameSceneMode.None;
            Role = GameWorldRole.None;
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

    }
}
