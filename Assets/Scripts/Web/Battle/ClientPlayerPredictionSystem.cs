using System;
using System.Collections.Generic;
using Server;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(ClientPlayerPredictionSystemGroup))]
[UpdateBefore(typeof(ClientPlayerStateRecordSystem))]
public partial class ClientPlayerPredictionSystem : SystemBase
{
    private const float PositionToleranceSq = 0.0001f;
    private const float VelocityToleranceSq = 0.0001f;
    private const float ScalarTolerance = 0.0001f;

    private EntityQuery _localPlayerQuery;
    private ClientFrameManager _frameManager;
    private uint _lastControlFrame;
    private uint _controlSceneVersion;
    private bool _hasControlFrame;

    protected override void OnCreate()
    {
        _localPlayerQuery = GetEntityQuery(
            ComponentType.ReadOnly<NetworkPlayerComponent>(),
            ComponentType.ReadOnly<NetworkIdentityComponent>());
    }

    protected override void OnUpdate()
    {
        if (!FrameManagerUtility.TryGet(EntityManager, out ClientFrameManager frame))
            return;

        if (_frameManager != frame)
        {
            UnbindFrameManager();
            _frameManager = frame;
            _hasControlFrame = false;
            _frameManager.onHandlePlayerFrame = HandlePlayerFrame;
        }

        EnsurePresentationState();
    }

    protected override void OnDestroy()
    {
        UnbindFrameManager();
        base.OnDestroy();
    }

    private bool HandlePlayerFrame(
        uint authoritativeFrame,
        IReadOnlyList<NetworkStateData> states,
        NetworkStateApplyContext context)
    {
        if (_frameManager == null || states == null)
        {
            return false;
        }
        bool hasMove = TryFindMoveState(states, out NetworkMoveStateData authoritativeMove);
        NetworkControlStateData authoritativeControl = null;
        for (int i = 0; i < states.Count; i++)
            if (states[i] is NetworkControlStateData control)
                authoritativeControl = control;
        if (!hasMove && authoritativeControl == null)
            return false;

        if (_controlSceneVersion != _frameManager.sceneVersion ||
            (!_frameManager.HasReconciledPlayerFrame && _frameManager.currentFrame <= _lastControlFrame))
            _hasControlFrame = false;
        _controlSceneVersion = _frameManager.sceneVersion;
        bool skipControl = authoritativeControl == null ||
                           (_hasControlFrame && authoritativeFrame <= _lastControlFrame);

        Entity player = GetLocalPlayerEntity();
        if (player == Entity.Null)
            return false;

        if (_frameManager.HasReconciledPlayerFrame &&
            authoritativeFrame <= _frameManager.LastReconciledPlayerFrame)
        {
            // A delayed/repeated move must not rewind prediction after its history was retired.
            ApplyStates(context, states, skipMove: true, skipControl: skipControl);
            if (!skipControl)
                UpdateControlHistory(authoritativeControl, context, authoritativeFrame);
            RecordControlFrame(authoritativeFrame, skipControl);
            return true;
        }

        bool hasSnapshot = _frameManager.playerStates.TryGetValue(
            authoritativeFrame,
            out ClientPlayerPredictionSnapshot snapshot);
        bool predictionMatches = !_frameManager.HasPendingPredictionReplay &&
                                 hasSnapshot &&
                                 (!hasMove || (snapshot.TryGetMoveState(out NetworkMoveStateData predictedMove) &&
                                  MoveStatesMatch(predictedMove, authoritativeMove))) &&
                                 (skipControl || (snapshot.HasControl && UnitControlUtility.StatesMatch(
                                     snapshot.Control, authoritativeControl.CreateRuntime(context))));

        if (predictionMatches)
        {
            // 移动逻辑已经相同，不把历史权威位置重新写回当前预测态。
            ApplyStates(context, states, skipMove: true, skipControl: true);
            if (!skipControl)
                authoritativeControl.ApplyPresentation(context, player);
        }
        else
        {
            bool restored = hasSnapshot && snapshot.RestoreStateScript(EntityManager, player);
            if (restored)
            {
                snapshot.RestoreControl(EntityManager, player);
                if (!hasMove && snapshot.TryGetMoveState(out NetworkMoveStateData baseMove))
                    baseMove.Apply(context);
            }
            ApplyStates(context, states, skipMove: false, skipControl: skipControl, controlAtFrame: restored);
            if (!skipControl)
                UpdateControlHistory(authoritativeControl, context, authoritativeFrame);
            if (restored && authoritativeFrame < _frameManager.currentFrame)
                _frameManager.RequestPredictionReplay(authoritativeFrame);
            else if (!_frameManager.HasPendingPredictionReplay &&
                     EntityManager.HasComponent<ClientPlayerMovePresentationComponent>(player))
            {
                ClientPlayerMovePresentationComponent presentation =
                    EntityManager.GetComponentData<ClientPlayerMovePresentationComponent>(player);
                presentation.CompleteReconciliation(
                    EntityManager.GetComponentData<UnitMoveComponent>(player).PredictedPosition);
                EntityManager.SetComponentData(player, presentation);
            }
        }

        RecordControlFrame(authoritativeFrame, skipControl);
        if (hasMove)
        {
            _frameManager.RemovePredictionHistoryThrough(authoritativeFrame);
            _frameManager.RecordReconciledPlayerFrame(authoritativeFrame);
        }
        return true;
    }

    private void RecordControlFrame(uint frame, bool skipControl)
    {
        if (skipControl)
            return;
        _hasControlFrame = true;
        _lastControlFrame = frame;
    }

    private void UpdateControlHistory(NetworkControlStateData control, NetworkStateApplyContext context, uint frame)
    {
        // 控制可能晚于同帧的位置确认到达。补齐仍保留的历史，避免随后仅纠正位置时丢掉免疫。
        foreach (var pair in _frameManager.playerStates)
        {
            if (pair.Key < frame)
                continue;
            UnitControlRuntimeComponent runtime = control.CreateRuntime(context);
            UnitControlUtility.TickAndRefresh(ref runtime, (pair.Key - frame) * context.FrameInterval / 1000f);
            runtime.NetworkDirty = 0;
            pair.Value.HasControl = true;
            pair.Value.Control = runtime;
        }
    }

    private void EnsurePresentationState()
    {
        Entity player = GetLocalPlayerEntity();
        if (player == Entity.Null ||
            !EntityManager.HasComponent<LocalTransform>(player) ||
            EntityManager.HasComponent<ClientPlayerMovePresentationComponent>(player))
        {
            return;
        }

        float3 position = EntityManager.GetComponentData<LocalTransform>(player).Position;
        EntityManager.AddComponentData(player, new ClientPlayerMovePresentationComponent
        {
            CurrentPosition = position,
            PreviousPredictionPosition = position,
            LatestPredictionPosition = position,
            Initialized = 1,
        });
    }

    private Entity GetLocalPlayerEntity()
    {
        if (_localPlayerQuery.IsEmptyIgnoreFilter)
            return Entity.Null;

        using NativeArray<Entity> entities = _localPlayerQuery.ToEntityArray(Allocator.Temp);
        return entities.Length > 0 ? entities[0] : Entity.Null;
    }

    private void UnbindFrameManager()
    {
        if (_frameManager != null && _frameManager.onHandlePlayerFrame == HandlePlayerFrame)
            _frameManager.onHandlePlayerFrame = null;
        _frameManager = null;
    }

    private static void ApplyStates(
        NetworkStateApplyContext context,
        IReadOnlyList<NetworkStateData> states,
        bool skipMove,
        bool skipControl = false,
        bool controlAtFrame = false)
    {
        for (int index = 0; index < states.Count; index++)
        {
            NetworkStateData state = states[index];
            if (state == null || (skipMove && state is NetworkMoveStateData) ||
                (skipControl && state is NetworkControlStateData))
                continue;
            if (controlAtFrame && state is NetworkControlStateData control)
                control.ApplyAtFrame(context);
            else
                state.Apply(context);
        }
    }

    private static bool TryFindMoveState(
        IReadOnlyList<NetworkStateData> states,
        out NetworkMoveStateData moveState)
    {
        for (int index = 0; index < states.Count; index++)
        {
            if (states[index] is NetworkMoveStateData move)
            {
                moveState = move;
                return true;
            }
        }

        moveState = null;
        return false;
    }

    private static bool MoveStatesMatch(
        NetworkMoveStateData predicted,
        NetworkMoveStateData authoritative)
    {
        float3 predictedPosition = new(predicted.positionX, predicted.positionY, predicted.positionZ);
        float3 authoritativePosition = new(
            authoritative.positionX,
            authoritative.positionY,
            authoritative.positionZ);
        float2 predictedVelocity = new(predicted.velocityX, predicted.velocityY);
        float2 authoritativeVelocity = new(authoritative.velocityX, authoritative.velocityY);
        return math.distancesq(predictedPosition, authoritativePosition) <= PositionToleranceSq &&
               math.distancesq(predictedVelocity, authoritativeVelocity) <= VelocityToleranceSq &&
               math.abs(predicted.baseMoveSpeed - authoritative.baseMoveSpeed) <= ScalarTolerance &&
               math.abs(predicted.baseMoveSpeedOffset - authoritative.baseMoveSpeedOffset) <= ScalarTolerance &&
               math.abs(predicted.baseMaxAcceleration - authoritative.baseMaxAcceleration) <= ScalarTolerance &&
               math.abs(predicted.stateMoveMultiplier - authoritative.stateMoveMultiplier) <= ScalarTolerance;
    }
}
