using Unity.Entities;
using Unity.Mathematics;

public struct UnitFacingComponent : IComponentData
{
    public float2 Direction;
    public float2 AnimationDirection;
    public byte HasAnimationDirection;
}

public static class UnitFacingUtility
{
    private static readonly float2 DefaultFacing = new(1f, 0f);

    public static bool TryGetFacing(EntityManager entityManager, Entity entity, out float2 facing)
    {
        if (entity != Entity.Null &&
            entityManager.Exists(entity) &&
            entityManager.HasComponent<UnitFacingComponent>(entity))
        {
            facing = math.normalizesafe(entityManager.GetComponentData<UnitFacingComponent>(entity).Direction, DefaultFacing);
            return true;
        }

        if (entity != Entity.Null &&
            entityManager.Exists(entity) &&
            entityManager.HasComponent<UnitMoveComponent>(entity))
        {
            UnitMoveComponent move = entityManager.GetComponentData<UnitMoveComponent>(entity);
            if (math.lengthsq(move.Velocity) > 0.0001f)
            {
                facing = math.normalize(move.Velocity);
                return true;
            }
        }

        facing = DefaultFacing;
        return false;
    }

    public static void EnsureFacing(EntityManager entityManager, Entity entity, float2 fallbackDirection)
    {
        if (entity == Entity.Null || !entityManager.Exists(entity))
            return;

        float2 direction = math.normalizesafe(fallbackDirection, DefaultFacing);
        if (entityManager.HasComponent<UnitFacingComponent>(entity))
        {
            UnitFacingComponent facing = entityManager.GetComponentData<UnitFacingComponent>(entity);
            facing.Direction = direction;
            entityManager.SetComponentData(entity, facing);
        }
        else
        {
            entityManager.AddComponentData(entity, new UnitFacingComponent { Direction = direction });
        }
    }

    public static void SetFacing(EntityManager entityManager, Entity entity, float2 direction)
    {
        EnsureFacing(entityManager, entity, direction);
    }

    public static void SetAnimationDirection(EntityManager entityManager, Entity entity, float2 direction)
    {
        if (entity == Entity.Null || !entityManager.Exists(entity))
            return;

        float2 normalized = math.normalizesafe(direction, float2.zero);
        if (math.lengthsq(normalized) <= 0.0001f)
        {
            ClearAnimationDirection(entityManager, entity);
            return;
        }

        if (entityManager.HasComponent<UnitFacingComponent>(entity))
        {
            UnitFacingComponent facing = entityManager.GetComponentData<UnitFacingComponent>(entity);
            facing.AnimationDirection = normalized;
            facing.HasAnimationDirection = 1;
            entityManager.SetComponentData(entity, facing);
        }
        else
        {
            entityManager.AddComponentData(entity, new UnitFacingComponent
            {
                Direction = DefaultFacing,
                AnimationDirection = normalized,
                HasAnimationDirection = 1,
            });
        }
    }

    public static void ClearAnimationDirection(EntityManager entityManager, Entity entity)
    {
        if (entity == Entity.Null || !entityManager.Exists(entity) || !entityManager.HasComponent<UnitFacingComponent>(entity))
            return;

        UnitFacingComponent facing = entityManager.GetComponentData<UnitFacingComponent>(entity);
        facing.AnimationDirection = float2.zero;
        facing.HasAnimationDirection = 0;
        entityManager.SetComponentData(entity, facing);
    }

    public static bool TryGetAnimationDirection(EntityManager entityManager, Entity entity, out float2 direction)
    {
        direction = float2.zero;
        if (entity == Entity.Null ||
            !entityManager.Exists(entity) ||
            !entityManager.HasComponent<UnitFacingComponent>(entity))
        {
            return false;
        }

        UnitFacingComponent facing = entityManager.GetComponentData<UnitFacingComponent>(entity);
        if (facing.HasAnimationDirection == 0)
            return false;

        direction = math.normalizesafe(facing.AnimationDirection, float2.zero);
        return math.lengthsq(direction) > 0.0001f;
    }

    public static bool FaceTowardsPosition(EntityManager entityManager, Entity entity, float2 targetPosition)
    {
        if (entity == Entity.Null ||
            !entityManager.Exists(entity) ||
            !entityManager.HasComponent<Unity.Transforms.LocalTransform>(entity))
        {
            return false;
        }

        float2 selfPosition = entityManager.GetComponentData<Unity.Transforms.LocalTransform>(entity).Position.xy;
        float2 desiredDirection = targetPosition - selfPosition;
        if (math.lengthsq(desiredDirection) <= 0.0001f)
            return false;

        EnsureFacing(entityManager, entity, desiredDirection);
        return true;
    }

    public static quaternion CreateRotation(float2 direction)
    {
        float2 normalized = math.normalizesafe(direction, DefaultFacing);
        float angleRadians = math.atan2(normalized.y, normalized.x);
        return quaternion.RotateZ(angleRadians);
    }
}
