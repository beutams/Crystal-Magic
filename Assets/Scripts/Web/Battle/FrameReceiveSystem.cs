using System;
using System.Collections.Generic;
using System.Linq;
using CrystalMagic.Core;
using Server;
using Unity.Collections;
using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
public partial class FrameReceiveSystem : SystemBase
{
    private EntityQuery _bufferQuery;
    private EntityQuery _localPlayerQuery;
    private FrameManager _frameManager;
    private int _frameInterval;

    protected override void OnCreate()
    {
        _bufferQuery = GetEntityQuery(ComponentType.ReadWrite<FrameReceiveBufferComponent>());
        _localPlayerQuery = GetEntityQuery(ComponentType.ReadOnly<NetworkPlayerComponent>());
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
            _frameManager.onHandleReceive += OnReceiveFrame;
        }

        _frameInterval = frameManager.frameInterval;
        Entity bufferEntity = _bufferQuery.GetSingletonEntity();
        FrameReceiveBufferComponent buffer = EntityManager.GetComponentObject<FrameReceiveBufferComponent>(bufferEntity);
        if (_frameManager.running)
            _frameManager.HandleReceive();
        while (buffer.frames.Count > 0)
        {
            KeyValuePair<uint, Queue<NetworkState>> frame = buffer.frames.First();
            buffer.frames.Remove(frame.Key);

            NetworkStateApplyContext context = new(EntityManager, frame.Key, buffer.frameInterval);
            ClientFrameManager clientFrame = _frameManager as ClientFrameManager;
            clientFrame?.RecordServerFrame(frame.Key);

            Guid localPlayerId = GetLocalPlayerId();
            List<NetworkStateData> localPlayerStates = CollectPlayerStates(frame.Value, localPlayerId);
            bool playerFrameHandled =
                clientFrame?.TryHandlePlayerFrame(frame.Key, localPlayerStates, context) == true;

            while (frame.Value.Count > 0)
            {
                NetworkStateData state = frame.Value.Dequeue().data;
                if (state == null || (playerFrameHandled && state.unitId == localPlayerId &&
                                     state is not NetworkCharacterStateData and not NetworkBattlePlayerStatusStateData))
                    continue;

                state.Apply(context);
            }
        }
    }

    private Guid GetLocalPlayerId()
    {
        if (_localPlayerQuery.IsEmptyIgnoreFilter)
            return Guid.Empty;

        using NativeArray<Entity> entities = _localPlayerQuery.ToEntityArray(Allocator.Temp);
        NetworkPlayerComponent player =
            EntityManager.GetComponentData<NetworkPlayerComponent>(entities[0]);
        return player.id;
    }

    private static List<NetworkStateData> CollectPlayerStates(
        Queue<NetworkState> states,
        Guid localPlayerId)
    {
        List<NetworkStateData> result = new();
        if (localPlayerId == Guid.Empty)
            return result;

        foreach (NetworkState state in states)
        {
            if (state.data != null && state.data.unitId == localPlayerId &&
                state.data is not NetworkCharacterStateData and not NetworkBattlePlayerStatusStateData)
                result.Add(state.data);
        }

        return result;
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
