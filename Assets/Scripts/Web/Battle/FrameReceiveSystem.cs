using System.Collections.Generic;
using System.Linq;
using CrystalMagic.Core;
using Server;
using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
public partial class FrameReceiveSystem : SystemBase
{
    private EntityQuery _bufferQuery;
    private FrameManager _frameManager;
    private int _frameInterval;

    protected override void OnCreate()
    {
        _bufferQuery = GetEntityQuery(ComponentType.ReadWrite<FrameReceiveBufferComponent>());
        if (_bufferQuery.IsEmptyIgnoreFilter)
            EntityManager.CreateEntity(typeof(FrameReceiveBufferComponent));

    }

    protected override void OnDestroy()
    {
        if (_frameManager != null)
            _frameManager.onHandleReceive -= OnReceiveFrame;
    }

    protected override void OnUpdate()
    {
        if (!FrameManagerUtility.TryGet(EntityManager, out FrameManager frameManager))
            return;

        if (_frameManager != frameManager)
        {
            if (_frameManager != null)
                _frameManager.onHandleReceive -= OnReceiveFrame;

            _frameManager = frameManager;
            _frameInterval = frameManager.frameInterval;
            _frameManager.onHandleReceive += OnReceiveFrame;
        }

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

    private void OnReceiveFrame(uint frame, Queue<NetworkState> states)
    {
        if (states == null || states.Count == 0)
        {
            return;
        }

        Entity bufferEntity = _bufferQuery.GetSingletonEntity();
        FrameReceiveBufferComponent buffer = EntityManager.GetComponentObject<FrameReceiveBufferComponent>(bufferEntity);
        buffer.frameInterval = _frameInterval;
        if (!buffer.frames.TryGetValue(frame, out Queue<NetworkState> targetStates))
        {
            buffer.frames.Add(frame, states);
            return;
        }

        while (states.Count > 0)
        {
            targetStates.Enqueue(states.Dequeue());
        }
    }
}
