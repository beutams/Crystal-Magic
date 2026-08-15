using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateAfter(typeof(BehaviorTreeSystem))]
[UpdateBefore(typeof(StateScriptSystem))]
partial struct UnitControlSystem : ISystem
{
    private const float KnockbackSnapAcceleration = 100000f;

    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        EntityManager entityManager = state.EntityManager;

        foreach (var (_, entity) in SystemAPI.Query<RefRO<UnitControlRuntimeComponent>>()
                     .WithNone<UnitDeathComponent>()
                     .WithEntityAccess())
        {
            UnitControlUtility.TickAndRefresh(entityManager, entity, deltaTime);
            ApplyControlledMovement(entityManager, entity);
        }
    }

    private static void ApplyControlledMovement(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.HasComponent<UnitMoveComponent>(entity))
            return;

        UnitControlRuntimeComponent control = entityManager.GetComponentData<UnitControlRuntimeComponent>(entity);
        UnitMoveComponent move = entityManager.GetComponentData<UnitMoveComponent>(entity);
        switch (control.ActiveType)
        {
            case UnitControlType.Knockback:
                move.SetTargetMovement(
                    control.ActiveMotionVelocity,
                    math.length(control.ActiveMotionVelocity),
                    math.max(move.RealMaxAcceleration, KnockbackSnapAcceleration));
                if (math.lengthsq(control.ActiveMotionVelocity) > 0.0001f)
                    UnitFacingUtility.SetFacing(entityManager, entity, -control.ActiveMotionVelocity);
                break;

            case UnitControlType.Fear:
                float2 fearDirection = GetFearDirection(entityManager, entity, control.ActiveSourceEntity);
                move.SetTargetMovementByFactor(fearDirection);
                if (math.lengthsq(fearDirection) > 0.0001f)
                    UnitFacingUtility.SetFacing(entityManager, entity, fearDirection);
                break;

            default:
                return;
        }

        entityManager.SetComponentData(entity, move);
    }

    private static float2 GetFearDirection(EntityManager entityManager, Entity entity, Entity sourceEntity)
    {
        if (!entityManager.HasComponent<LocalTransform>(entity) ||
            sourceEntity == Entity.Null ||
            !entityManager.Exists(sourceEntity) ||
            !entityManager.HasComponent<LocalTransform>(sourceEntity))
        {
            return float2.zero;
        }

        float2 selfPosition = entityManager.GetComponentData<LocalTransform>(entity).Position.xy;
        float2 sourcePosition = entityManager.GetComponentData<LocalTransform>(sourceEntity).Position.xy;
        return math.normalizesafe(selfPosition - sourcePosition);
    }
}
