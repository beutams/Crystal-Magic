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

[Flags]
public enum GameWorldExecutionTarget : byte
{
    None = 0,
    Standalone = 1 << 0,
    Client = 1 << 1,
    Server = 1 << 2,
    All = Standalone | Client | Server,
}

public static class GameWorldExecutionTargetUtility
{
    public static GameWorldExecutionTarget FromRole(GameWorldRole role)
    {
        return role switch
        {
            GameWorldRole.Standalone => GameWorldExecutionTarget.Standalone,
            GameWorldRole.Client => GameWorldExecutionTarget.Client,
            GameWorldRole.Server => GameWorldExecutionTarget.Server,
            _ => GameWorldExecutionTarget.None,
        };
    }

    public static bool Contains(GameWorldExecutionTarget targets, GameWorldRole role)
    {
        GameWorldExecutionTarget target = FromRole(role);
        return target != GameWorldExecutionTarget.None && (targets & target) != 0;
    }
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
        public static bool TryGet(EntityManager entityManager, out GameWorldContextComponent context)
        {
            using EntityQuery query =
                entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameWorldContextComponent>());
            return query.TryGetSingleton(out context);
        }

        public static GameWorldContextComponent Get(EntityManager entityManager)
        {
            return GameSingletonUtility.Get<GameWorldContextComponent>(entityManager);
        }

        public static GameSceneMode GetSceneMode(EntityManager entityManager)
        {
            return Get(entityManager).SceneMode;
        }
    }

    /// <summary>
    /// 菜单阶段仅创建一个空的 Bootstrap World 来满足 Entities 的默认 World 契约；
    /// 它不包含系统，也不会加入 PlayerLoop。预加载 World 独立准备，进入游戏后才接管默认 World。
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
    /// 单机和联机分别复用预加载 World，切换角色时停放旧 World，保留系统和通用注册表。
    /// Battle Server 的预加载 World 由 BattleRoom 取得并独立驱动。
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

            if (!GameWorldPreload.TryTake(role, out _gameWorld))
                _gameWorld = CreateConfiguredWorld(role, WorldName);
            Role = role;
            SceneMode = GameSceneMode.None;
            World.DefaultGameObjectInjectionWorld = _gameWorld;
            if (appendToPlayerLoop)
                AppendGameWorldToPlayerLoop();
            return _gameWorld;
        }

        internal static World CreateConfiguredWorld(GameWorldRole role, string name)
        {
            WorldFlags worldFlags = role switch
            {
                GameWorldRole.Server => WorldFlags.GameServer,
                GameWorldRole.Client => WorldFlags.GameClient,
                _ => WorldFlags.Game,
            };
            WorldSystemFilterFlags systemFilter = role switch
            {
                // GetAllSystems expands Default to LocalSimulation | Presentation.
                // Adding it to a network role also installs standalone input, loot,
                // and projectile simulation alongside the network systems.
                GameWorldRole.Server => WorldSystemFilterFlags.ServerSimulation,
                GameWorldRole.Client => WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Presentation,
                _ => WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.Presentation,
            };

            World world = new World(name, worldFlags);
            try
            {
                GameSingletonUtility.Create(world.EntityManager, role, GameSceneMode.None);
                DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(
                    world, DefaultWorldInitialization.GetAllSystems(systemFilter));
                return world;
            }
            catch { world.Dispose(); throw; }
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

        public static void RemoveGameWorldFromPlayerLoop()
        {
            if (!HasGameWorld || !_appendedToPlayerLoop)
                return;
            ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(_gameWorld);
            _appendedToPlayerLoop = false;
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
                ReleaseGameWorld();
                return;
            }

            SetSceneMode(targetMode);
        }

        public static void SetSceneMode(GameSceneMode sceneMode)
        {
            if (!HasGameWorld)
                return;

            SceneMode = sceneMode;
            GameSingletonUtility.Set(_gameWorld.EntityManager, new GameWorldContextComponent
            {
                Role = Role,
                SceneMode = SceneMode,
            });
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

            GameWorldPreload.Forget(_gameWorld);
            _gameWorld.Dispose();
            _gameWorld = null;
            _appendedToPlayerLoop = false;
            SceneMode = GameSceneMode.None;
            Role = GameWorldRole.None;
        }

        public static void Shutdown()
        {
            ShutdownGameWorld();
            GameWorldPreload.Dispose();
            DisposeBootstrapWorld();
        }

        public static void ReleaseGameWorld()
        {
            // Headless servers have no SceneComponent. Local/client presentation
            // must release its entities while the owning World is still alive.
            if (SceneComponent.TryGetInstance(out SceneComponent sceneComponent))
                sceneComponent.MapPresentation.Clear();
            if (!HasGameWorld) return;
            RemoveGameWorldFromPlayerLoop();
            World previous = _gameWorld;
            if (World.DefaultGameObjectInjectionWorld == previous)
                World.DefaultGameObjectInjectionWorld = null;
            _gameWorld = null;
            Role = GameWorldRole.None;
            SceneMode = GameSceneMode.None;
            if (!GameWorldPreload.TryReturn(previous))
                previous.Dispose();
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
