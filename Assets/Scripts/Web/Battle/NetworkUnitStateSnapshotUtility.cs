using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;

public static class NetworkUnitStateSnapshotUtility
{
    public static NetworkMoveStateData CreateMoveState(
        Guid unitId,
        in UnitMoveComponent move,
        in LocalTransform transform)
    {
        return new NetworkMoveStateData
        {
            unitId = unitId,
            baseMoveSpeed = move.BaseMoveSpeed,
            baseMoveSpeedOffset = move.BaseMoveSpeedOffset,
            baseMaxAcceleration = move.BaseMaxAcceleration,
            stateMoveMultiplier = move.StateMoveMultiplier,
            velocityX = move.Velocity.x,
            velocityY = move.Velocity.y,
            positionX = transform.Position.x,
            positionY = transform.Position.y,
            positionZ = transform.Position.z,
        };
    }

    public static NetworkFacingStateData CreateFacingState(
        Guid unitId,
        in UnitFacingComponent facing)
    {
        return new NetworkFacingStateData
        {
            unitId = unitId,
            directionX = facing.Direction.x,
            directionY = facing.Direction.y,
        };
    }

    public static Queue<NetworkStateData> CapturePlayerState(
        EntityManager entityManager,
        Entity entity,
        Guid unitId)
    {
        Queue<NetworkStateData> states = new();
        if (unitId == Guid.Empty || entity == Entity.Null || !entityManager.Exists(entity))
            return states;

        if (entityManager.HasComponent<UnitMoveComponent>(entity) &&
            entityManager.HasComponent<LocalTransform>(entity))
        {
            UnitMoveComponent move = entityManager.GetComponentData<UnitMoveComponent>(entity);
            LocalTransform transform = entityManager.GetComponentData<LocalTransform>(entity);
            if (move.HasPredictedPosition != 0)
                transform.Position = move.PredictedPosition;
            states.Enqueue(CreateMoveState(unitId, move, transform));
        }

        if (entityManager.HasComponent<UnitFacingComponent>(entity))
        {
            UnitFacingComponent facing = entityManager.GetComponentData<UnitFacingComponent>(entity);
            states.Enqueue(CreateFacingState(unitId, facing));
        }

        return states;
    }
}
