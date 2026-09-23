using System.Collections.Generic;
using Server;
using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitPostProcessSystemGroup), OrderLast = true)]
[UpdateBefore(typeof(ServerNetworkStateCollectSystem))]
public partial class ServerNetworkEntitySpawnCollectSystem : SystemBase
{
    private EntityQuery _queueQuery;

    protected override void OnCreate()
    {
        _queueQuery = GetEntityQuery(ComponentType.ReadOnly<NetworkEntitySpawnQueueComponent>());
    }

    protected override void OnUpdate()
    {
        if (!FrameManagerUtility.TryGet(EntityManager, out ServerFrameManager frame) ||
            !frame.running ||
            _queueQuery.IsEmptyIgnoreFilter)
        {
            return;
        }

        NetworkEntitySpawnQueueComponent spawnQueue = EntityManager.GetComponentObject<NetworkEntitySpawnQueueComponent>(
            _queueQuery.GetSingletonEntity());
        if (spawnQueue.entityInfos.Count == 0)
        {
            return;
        }

        uint currentFrame = frame.currentFrame;
        if (!frame.sendOrder.TryGetValue(currentFrame, out Queue<NetworkStateData> states))
        {
            states = new Queue<NetworkStateData>();
            frame.sendOrder.Add(currentFrame, states);
        }

        for (int index = 0; index < spawnQueue.entityInfos.Count; index++)
        {
            NetworkEntitySpawnInfo entityInfo = spawnQueue.entityInfos[index];
            if (entityInfo == null || entityInfo.unitId == System.Guid.Empty || string.IsNullOrWhiteSpace(entityInfo.prefabName))
            {
                continue;
            }

            states.Enqueue(new NetworkEntitySpawnStateData
            {
                unitId = entityInfo.unitId,
                entityInfo = entityInfo,
            });
        }

        spawnQueue.entityInfos.Clear();
    }
}
