using Unity.Entities;
using Unity.Mathematics;

[FactoryKey("ControlledState")]
public class ControlledState : AUnitState
{
    private const float KnockbackSnapAcceleration = 100000f;

    public override void OnEnter()
    {
    }

    public override void OnUpdate(float deltaTime)
    {
        ApplyControlledMovement();
    }

    public override void OnExit()
    {
    }

    private void ApplyControlledMovement()
    {
        if (!EntityManager.HasComponent<UnitMoveComponent>(Entity))
            return;

        UnitMoveComponent move = EntityManager.GetComponentData<UnitMoveComponent>(Entity);

        if (EntityManager.HasComponent<UnitControlRuntimeComponent>(Entity))
        {
            UnitControlRuntimeComponent control = EntityManager.GetComponentData<UnitControlRuntimeComponent>(Entity);
            switch (control.ActiveType)
            {
                case UnitControlType.Knockback:
                    move.SetTargetMovement(
                        control.ActiveMotionVelocity,
                        math.length(control.ActiveMotionVelocity),
                        math.max(move.RealMaxAcceleration, KnockbackSnapAcceleration));
                    if (math.lengthsq(control.ActiveMotionVelocity) > 0.0001f)
                        UnitFacingUtility.SetFacing(EntityManager, Entity, -control.ActiveMotionVelocity);
                    break;

                case UnitControlType.Fear:
                    float2 fearDirection = GetFearDirection(control.ActiveSourceEntity);
                    move.SetTargetMovementByFactor(fearDirection);
                    if (math.lengthsq(fearDirection) > 0.0001f)
                        UnitFacingUtility.SetFacing(EntityManager, Entity, fearDirection);
                    break;
            }
        }

        EntityManager.SetComponentData(Entity, move);
    }

    private float2 GetFearDirection(Entity sourceEntity)
    {
        if (!EntityManager.HasComponent<Unity.Transforms.LocalTransform>(Entity))
            return float2.zero;

        if (sourceEntity == Entity.Null ||
            !EntityManager.Exists(sourceEntity) ||
            !EntityManager.HasComponent<Unity.Transforms.LocalTransform>(sourceEntity))
        {
            return float2.zero;
        }

        float2 selfPosition = EntityManager.GetComponentData<Unity.Transforms.LocalTransform>(Entity).Position.xy;
        float2 sourcePosition = EntityManager.GetComponentData<Unity.Transforms.LocalTransform>(sourceEntity).Position.xy;
        return math.normalizesafe(selfPosition - sourcePosition);
    }
}
