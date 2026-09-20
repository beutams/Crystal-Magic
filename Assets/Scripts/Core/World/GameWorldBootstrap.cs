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
    public struct GameWorldContextComponent : IComponentData
    {
        public GameWorldRole Role;
        public GameSceneMode SceneMode;
    }

    public static class GameWorldContextUtility
    {
        public static void Bind(EntityManager entityManager, GameWorldRole role, GameSceneMode sceneMode)
        {
            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameWorldContextComponent>());
            GameWorldContextComponent context = new()
            {
                Role = role,
                SceneMode = sceneMode,
            };

            if (query.IsEmptyIgnoreFilter)
            {
                Entity entity = entityManager.CreateEntity();
                entityManager.AddComponentData(entity, context);
                query.Dispose();
                return;
            }

            Entity contextEntity = query.GetSingletonEntity();
            query.Dispose();
            entityManager.SetComponentData(contextEntity, context);
        }

        public static bool TryGet(EntityManager entityManager, out GameWorldContextComponent context)
        {
            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameWorldContextComponent>());
            if (query.IsEmptyIgnoreFilter)
            {
                query.Dispose();
                context = default;
                return false;
            }

            Entity contextEntity = query.GetSingletonEntity();
            query.Dispose();
            context = entityManager.GetComponentData<GameWorldContextComponent>(contextEntity);
            return true;
        }

        public static GameSceneMode GetSceneMode(EntityManager entityManager)
        {
            return TryGet(entityManager, out GameWorldContextComponent context)
                ? context.SceneMode
                : GameSceneMode.None;
        }
    }

    /// <summary>
    /// 菜单阶段仅创建一个空的 Bootstrap World 来满足 Entities 的默认 World 契约；
    /// 它不包含系统，也不会加入 PlayerLoop。进入一局游戏后才由 GameWorldManager 显式创建 GameWorld。
    /// </summary>
    public sealed class GameWorldBootstrap : ICustomBootstrap
    {
        public bool Initialize(string defaultWorldName)
        {
            GameWorldManager.CreateBootstrapWorld(defaultWorldName);
            return true;
        }
    }

    /// <summary>
    /// 管理客户端或单机当前游戏会话唯一的 ECS World。
    /// 单机 Town、Dungeon、Training 共享同一个 World；进入联机战斗时会销毁 Standalone World 并创建 Client World，
    /// 返回城镇时再反向重建。Battle Server 的 World 由各自 BattleRoom 独立持有，不经过这里。
    /// </summary>
    public static class GameWorldManager
    {
        private const string WorldName = "GameWorld";

        private static World _bootstrapWorld;
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

        public static World CreateGameWorld(GameWorldRole role, bool appendToPlayerLoop = true)
        {
            if (role == GameWorldRole.None)
                throw new ArgumentOutOfRangeException(nameof(role));

            if (HasGameWorld)
            {
                if (Role != role)
                    throw new InvalidOperationException($"GameWorld is already running as {Role}, cannot change it to {role}.");

                if (appendToPlayerLoop)
                    AppendGameWorldToPlayerLoop();

                return _gameWorld;
            }

            DisposeBootstrapWorld();

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
            SceneMode = GameSceneMode.None;
            GameWorldContextUtility.Bind(_gameWorld.EntityManager, role, SceneMode);
            if (appendToPlayerLoop)
                AppendGameWorldToPlayerLoop();

            return _gameWorld;
        }

        internal static void CreateBootstrapWorld(string defaultWorldName)
        {
            if (_bootstrapWorld != null && _bootstrapWorld.IsCreated)
            {
                World.DefaultGameObjectInjectionWorld = _bootstrapWorld;
                return;
            }

            _bootstrapWorld = new World(defaultWorldName, WorldFlags.Game);
            World.DefaultGameObjectInjectionWorld = _bootstrapWorld;
        }

        /// <summary>
        /// 用于黑屏转场阶段：World 尚未加入 Unity PlayerLoop 时，手动推进一次初始化和场景导入。
        /// </summary>
        public static void UpdateGameWorld()
        {
            if (!HasGameWorld || _appendedToPlayerLoop)
                return;

            _gameWorld.Update();
        }

        public static bool AppendGameWorldToPlayerLoop()
        {
            if (!HasGameWorld)
                return false;

            if (_appendedToPlayerLoop)
                return true;

            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(_gameWorld);
            _appendedToPlayerLoop = true;
            return true;
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
            GameWorldContextUtility.Bind(_gameWorld.EntityManager, Role, SceneMode);
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
            DisposeBootstrapWorld();
        }

        private static void DisposeBootstrapWorld()
        {
            if (_bootstrapWorld == null)
                return;

            if (World.DefaultGameObjectInjectionWorld == _bootstrapWorld)
                World.DefaultGameObjectInjectionWorld = null;

            if (_bootstrapWorld.IsCreated)
                _bootstrapWorld.Dispose();

            _bootstrapWorld = null;
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
