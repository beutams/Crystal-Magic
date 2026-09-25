using System.Collections.Generic;
using CrystalMagic.Core;
using Unity.Collections;
using Unity.Entities;

namespace Server
{
    // 转层保留 World、系统和配置注册表，只清理关卡实例与运行中的请求。
    public static class BattleSceneResetUtility
    {
        public static void Reset(World world)
        {
            EntityManager manager = world.EntityManager;
            manager.CompleteAllTrackedJobs();
            world.GetExistingSystemManaged<StateScriptManagedCommandSystem>()?.ResetScene();
            world.GetExistingSystemManaged<PersistentEffectSystem>()?.ResetScene();
            world.GetExistingSystemManaged<EffectExecutionSystem>()?.ResetScene();
            world.GetExistingSystemManaged<ServerNetworkStateCollectSystem>()?.ResetScene();
            world.GetExistingSystemManaged<ClientPlayerStateRecordSystem>()?.ResetScene();

            using (EntityQuery query = manager.CreateEntityQuery(new EntityQueryDesc
            {
                Any = new[] { ComponentType.ReadOnly<DungeonRuntimeOwnedEntity>(), ComponentType.ReadOnly<VfxLifetimeComponent>() },
                Options = EntityQueryOptions.IncludeDisabledEntities,
            }))
            {
                using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
                foreach (Entity entity in entities)
                    if (manager.Exists(entity))
                        manager.DestroyEntity(entity);
            }

            using (EntityQuery query = manager.CreateEntityQuery(ComponentType.ReadOnly<EffectDataBridgeComponent>()))
            {
                if (!query.IsEmptyIgnoreFilter)
                {
                    EffectDataBridgeComponent bridge = manager.GetComponentObject<EffectDataBridgeComponent>(query.GetSingletonEntity());
                    foreach (int id in new List<int>(bridge.Values.Keys))
                        if (!bridge.RegistryIds.Contains(id))
                            bridge.Values.Remove(id);
                    bridge.ManagedContexts.Clear();
                    bridge.ConditionLists.Clear();
                }
            }

            using (EntityQuery query = manager.CreateEntityQuery(ComponentType.ReadOnly<FrameReceiveBufferComponent>()))
                if (!query.IsEmptyIgnoreFilter)
                    manager.GetComponentObject<FrameReceiveBufferComponent>(query.GetSingletonEntity()).frames.Clear();
            using (EntityQuery query = manager.CreateEntityQuery(ComponentType.ReadOnly<NetworkPresentationEventQueueComponent>()))
            {
                if (!query.IsEmptyIgnoreFilter)
                {
                    NetworkPresentationEventQueueComponent queue = manager.GetComponentObject<NetworkPresentationEventQueueComponent>(query.GetSingletonEntity());
                    queue.Events.Clear();
                    queue.NextSequence = 0;
                }
            }
            using (EntityQuery query = manager.CreateEntityQuery(ComponentType.ReadOnly<ClientPresentationClockComponent>()))
                if (!query.IsEmptyIgnoreFilter)
                    manager.SetComponentData(query.GetSingletonEntity(), default(ClientPresentationClockComponent));
            using (EntityQuery query = manager.CreateEntityQuery(ComponentType.ReadOnly<ClientSkillVisualRuntimeComponent>()))
                if (!query.IsEmptyIgnoreFilter)
                    manager.SetComponentData(query.GetSingletonEntity(), default(ClientSkillVisualRuntimeComponent));

            NetworkEntitySpawnUtility.ClearSpawnQueue(manager);
            ClearBuffer<WorldVariableElement>(manager);
            ClearBuffer<InteractionTransactionElement>(manager);
            ClearBuffer<ClientPresentationEventElement>(manager);
            ClearBuffer<ClientSkillVisualRequestElement>(manager);
            ClearBuffer<ClientPredictedSkillVisualEventElement>(manager);
            ClearBuffer<UnitQueryEntry>(manager);
            ClearBuffer<UnitQueryScratchEntry>(manager);
            ClearBuffer<UnitQueryNode>(manager);
        }

        private static void ClearBuffer<T>(EntityManager manager) where T : unmanaged, IBufferElementData
        {
            using EntityQuery query = manager.CreateEntityQuery(ComponentType.ReadWrite<T>());
            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            foreach (Entity entity in entities)
                manager.GetBuffer<T>(entity).Clear();
        }
    }
}
