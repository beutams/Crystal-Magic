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
                if (!GameWorldPreload.TryTake(GameWorldRole.Server, out world))
                    world = GameWorldManager.CreateConfiguredWorld(GameWorldRole.Server, $"BattleWorld_{battleId}");
                GameSingletonUtility.Set(world.EntityManager, new GameWorldContextComponent
                { Role = GameWorldRole.Server, SceneMode = GameSceneMode.Dungeon });

                FrameManagerUtility.Bind(world.EntityManager, frame);
                DisableServerPresentation(world);
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

        internal static void DisableServerPresentation(World serverWorld)
        {
            if ((serverWorld.Flags & WorldFlags.GameServer) != WorldFlags.GameServer)
                return;

            // This internal Unity system runs outside PresentationSystemGroup,
            // even without the Presentation filter. It activates SpriteRenderer
            // companions alongside the client's characters in a hosted battle.
            GameWorldPreload.SetPresentation(serverWorld, false);
            // Keep live-baking initialization and CompanionReference disposal so
            // hidden companion objects still have their normal ownership/cleanup.
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

            World previous = world;
            bool wasAppended = appendedToPlayerLoop;
            world = null;
            appendedToPlayerLoop = false;
            RegistrySceneEntity = Entity.Null;
            try
            {
                if (wasAppended) ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(previous);
            }
            finally
            {
                if (!GameWorldPreload.TryReturn(previous))
                    previous.Dispose();
            }
        }
    }
}
