using System.Collections.Generic;
using System.Linq;
using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
public partial class FrameReceiveSystem : SystemBase
{
    private EntityQuery _bufferQuery;

    protected override void OnCreate()
    {
        _bufferQuery = GetEntityQuery(ComponentType.ReadWrite<FrameReceiveBufferComponent>());
        if (_bufferQuery.IsEmptyIgnoreFilter)
            EntityManager.CreateEntity(typeof(FrameReceiveBufferComponent));
    }

    protected override void OnUpdate()
    {
        Entity bufferEntity = _bufferQuery.GetSingletonEntity();
        FrameReceiveBufferComponent buffer = EntityManager.GetComponentObject<FrameReceiveBufferComponent>(bufferEntity);
        while (buffer.frames.Count > 0)
        {
            KeyValuePair<uint, Queue<NetworkState>> frame = buffer.frames.First();
            buffer.frames.Remove(frame.Key);

            NetworkStateApplyContext context = new(EntityManager, frame.Key, buffer.frameInterval);
            while (frame.Value.Count > 0)
                frame.Value.Dequeue().data?.Apply(context);
        }
    }
}
