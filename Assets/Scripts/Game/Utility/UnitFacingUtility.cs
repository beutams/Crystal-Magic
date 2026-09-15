using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

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

        facing = DefaultFacing;
        return false;
    }

    public static void EnsureFacing(EntityManager entityManager, Entity entity, float2 fallbackDirection)
    {
        if (entity == Entity.Null || !entityManager.Exists(entity))
            return;

        if (math.lengthsq(fallbackDirection) <= 0.0001f)
            return;

        float2 direction = math.normalize(fallbackDirection);
        if (!entityManager.HasComponent<UnitFacingComponent>(entity))
            return;

        UnitFacingComponent facing = entityManager.GetComponentData<UnitFacingComponent>(entity);
        facing.Direction = direction;
        entityManager.SetComponentData(entity, facing);
    }

    public static void SetFacing(EntityManager entityManager, Entity entity, float2 direction)
    {
        EnsureFacing(entityManager, entity, direction);
    }

    public static bool FaceTowardsPosition(EntityManager entityManager, Entity entity, float2 targetPosition)
    {
        if (entity == Entity.Null ||
            !entityManager.Exists(entity) ||
            !entityManager.HasComponent<LocalTransform>(entity))
        {
            return false;
        }

        float2 selfPosition = entityManager.GetComponentData<LocalTransform>(entity).Position.xy;
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
