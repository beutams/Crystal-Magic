using System;
using System.Collections.Generic;
using System.Linq;
using CrystalMagic.Core;
using Server;
using Unity.Collections;
using Unity.Entities;
using Unity.Profiling;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
public partial class FrameReceiveSystem : SystemBase
{
    private EntityQuery _bufferQuery;
    private EntityQuery _localPlayerQuery;
    private EntityQuery _networkEntitiesQuery;
    private EntityQuery _inputPlayersQuery;
    private readonly Dictionary<Guid, Entity> _entities = new();
    private readonly List<NetworkStateData> _localPlayerStates = new();
    private NetworkStateApplyContext _applyContext;
    private static readonly ProfilerMarker<int> ApplyFrameMarker = new("Network.Receive.ApplyFrame", "StateCount");
    private FrameManager _frameManager;
    private int _frameInterval;

    protected override void OnCreate()
    {
        _bufferQuery = GetEntityQuery(ComponentType.ReadWrite<FrameReceiveBufferComponent>());
        _localPlayerQuery = GetEntityQuery(ComponentType.ReadOnly<NetworkPlayerComponent>());
        _networkEntitiesQuery = GetEntityQuery(ComponentType.ReadOnly<NetworkIdentityComponent>());
        _inputPlayersQuery = GetEntityQuery(ComponentType.ReadOnly<NetworkIdentityComponent>(),
            ComponentType.ReadOnly<PlayerInputComponent>());
        if (_bufferQuery.IsEmptyIgnoreFilter)
        {
            Entity bufferEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentObject(bufferEntity, new FrameReceiveBufferComponent());
        }
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
        if (frameManager is ClientFrameManager && frameManager.running &&
            SystemAPI.TryGetSingleton(out BattleSimulationScope scope) &&
            scope.Pass == BattleSimulationPass.World)
            return;
        if (!TryGetBuffer(out FrameReceiveBufferComponent buffer))
            return;

        if (_frameManager.running)
            _frameManager.HandleReceive();
        if (buffer.frames.Count == 0)
            return;

        // Server inputs resolve players only. Refresh once per received batch so
        // client spawn registrations remain visible to subsequent frames in it.
        _applyContext ??= new NetworkStateApplyContext(EntityManager, _entities);
        _applyContext.RefreshEntityMap(frameManager is ServerFrameManager ? _inputPlayersQuery : _networkEntitiesQuery);
        ClientFrameManager clientFrame = _frameManager as ClientFrameManager;
        while (buffer.frames.Count > 0)
        {
            KeyValuePair<uint, Queue<NetworkState>> frame = buffer.frames.First();
            buffer.frames.Remove(frame.Key);

            using var profile = ApplyFrameMarker.Auto(frame.Value.Count);
            NetworkStateApplyContext context = _applyContext;
            context.BeginFrame(frame.Key, buffer.frameInterval);
            clientFrame?.RecordServerFrame(frame.Key);

            Guid localPlayerId = Guid.Empty;
            bool playerFrameHandled = false;
            if (clientFrame != null)
            {
                localPlayerId = GetLocalPlayerId();
                CollectPlayerStates(frame.Value, localPlayerId);
                playerFrameHandled = clientFrame.TryHandlePlayerFrame(frame.Key, _localPlayerStates, context);
            }

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

    private void CollectPlayerStates(
        Queue<NetworkState> states,
        Guid localPlayerId)
    {
        _localPlayerStates.Clear();
        if (localPlayerId == Guid.Empty)
            return;

        foreach (NetworkState state in states)
        {
            if (state.data != null && state.data.unitId == localPlayerId &&
                state.data is not NetworkCharacterStateData and not NetworkBattlePlayerStatusStateData)
                _localPlayerStates.Add(state.data);
        }
    }

    private void OnReceiveFrame(uint frame, Queue<NetworkState> states)
    {
        if (states == null || states.Count == 0)
        {
            return;
        }

        if (!TryGetBuffer(out FrameReceiveBufferComponent buffer))
            return;

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

    private bool TryGetBuffer(out FrameReceiveBufferComponent buffer)
    {
        buffer = null;
        if (_bufferQuery.IsEmptyIgnoreFilter || _bufferQuery.CalculateEntityCount() != 1)
            return false;

        Entity bufferEntity = _bufferQuery.GetSingletonEntity();
        buffer = EntityManager.GetComponentObject<FrameReceiveBufferComponent>(bufferEntity);
        if (buffer == null)
            return false;

        buffer.frames ??= new SortedDictionary<uint, Queue<NetworkState>>();
        return true;
    }
}
