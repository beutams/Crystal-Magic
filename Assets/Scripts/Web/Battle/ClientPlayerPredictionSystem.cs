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
        if (_frameManager == null || states == null ||
            !TryFindMoveState(states, out NetworkMoveStateData authoritativeMove))
        {
            return false;
        }

        Entity player = GetLocalPlayerEntity();
        if (player == Entity.Null)
            return false;

        bool hasSnapshot = _frameManager.playerStates.TryGetValue(
            authoritativeFrame,
            out ClientPlayerPredictionSnapshot snapshot);
        bool predictionMatches = !_frameManager.HasPendingPredictionReplay &&
                                 hasSnapshot &&
                                 snapshot.TryGetMoveState(out NetworkMoveStateData predictedMove) &&
                                 MoveStatesMatch(predictedMove, authoritativeMove);

        if (predictionMatches)
        {
            // 移动逻辑已经相同，不把历史权威位置重新写回当前预测态。
            ApplyStates(context, states, skipMove: true);
        }
        else
        {
            bool restored = hasSnapshot && snapshot.RestoreStateScript(EntityManager, player);
            ApplyStates(context, states, skipMove: false);
            if (restored && authoritativeFrame < _frameManager.currentFrame)
                _frameManager.RequestPredictionReplay(authoritativeFrame);
        }

        _frameManager.RemovePredictionHistoryThrough(authoritativeFrame);
        return true;
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
        bool skipMove)
    {
        for (int index = 0; index < states.Count; index++)
        {
            NetworkStateData state = states[index];
            if (state == null || (skipMove && state is NetworkMoveStateData))
                continue;
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
