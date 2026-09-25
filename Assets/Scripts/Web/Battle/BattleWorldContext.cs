using System;
using CrystalMagic.Core;
using Unity.Entities;
using Unity.Scenes;

namespace Server
{
    public sealed class BattleWorldContext : IDisposable
    {
        private World world;
        private bool appendedToPlayerLoop;

        public World World => IsCreated ? world : null;
        public EntityManager EntityManager => world.EntityManager;
        public Entity RegistrySceneEntity { get; private set; }
        public bool IsCreated => world != null && world.IsCreated;

        public BattleWorldContext(ulong battleId, ServerFrameManager frame, Hash128 registrySceneGuid)
        {
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));

            if (!registrySceneGuid.IsValid)
                throw new ArgumentException("Registry scene guid is invalid.", nameof(registrySceneGuid));

            try
            {
                world = new World($"BattleWorld_{battleId}", WorldFlags.GameServer);
                GameSingletonUtility.Create(
                    world.EntityManager,
                    GameWorldRole.Server,
                    GameSceneMode.Dungeon);
                DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(
                    world,
                    DefaultWorldInitialization.GetAllSystems(
                        WorldSystemFilterFlags.Default | WorldSystemFilterFlags.ServerSimulation));

                FrameManagerUtility.Bind(world.EntityManager, frame);
                RegistrySceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, registrySceneGuid);
                ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
                appendedToPlayerLoop = true;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public bool TryGetEntityManager(out EntityManager entityManager)
        {
            if (!IsCreated)
            {
                entityManager = default;
                return false;
            }

            entityManager = world.EntityManager;
            return true;
        }

        public void Dispose()
        {
            if (!IsCreated)
            {
                world = null;
                appendedToPlayerLoop = false;
                RegistrySceneEntity = Entity.Null;
                return;
            }

            if (appendedToPlayerLoop)
                ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(world);

            world.Dispose();
            world = null;
            appendedToPlayerLoop = false;
            RegistrySceneEntity = Entity.Null;
        }
    }
}
