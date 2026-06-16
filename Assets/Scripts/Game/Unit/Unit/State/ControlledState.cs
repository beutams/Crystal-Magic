using Unity.Entities;
using Unity.Mathematics;

[FactoryKey("ControlledState")]
public class ControlledState : AUnitState
{
    public override void OnEnter()
    {
        ApplyControlledMovement();
        ClearAnimationFacingDirection();
    }

    public override void OnUpdate(float deltaTime)
    {
        ApplyControlledMovement();
        ClearAnimationFacingDirection();
    }

    public override void OnExit()
    {
        if (EntityManager.HasComponent<UnitMoveComponent>(Entity))
        {
            UnitMoveComponent move = EntityManager.GetComponentData<UnitMoveComponent>(Entity);
            move.ClearCommand();
            EntityManager.SetComponentData(Entity, move);
        }

        ClearAnimationFacingDirection();
    }

    private void ApplyControlledMovement()
    {
        if (!EntityManager.HasComponent<UnitMoveComponent>(Entity))
            return;

        UnitMoveComponent move = EntityManager.GetComponentData<UnitMoveComponent>(Entity);
        move.ClearCommand();

        if (EntityManager.HasComponent<UnitControlRuntimeComponent>(Entity))
        {
            UnitControlRuntimeComponent control = EntityManager.GetComponentData<UnitControlRuntimeComponent>(Entity);
            switch (control.ActiveType)
            {
                case UnitControlType.Knockback:
                    move.SetDirectVelocityCommand(control.ActiveMotionVelocity);
                    break;

                case UnitControlType.Fear:
                    move.SetAccelerateCommand(GetFearDirection(control.ActiveSourceEntity));
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
