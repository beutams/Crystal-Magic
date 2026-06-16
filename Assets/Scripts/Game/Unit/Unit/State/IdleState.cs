using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// 待机状态——清零加速意图，MoveSystem 会自然减速到停。
/// </summary>
[FactoryKey("IdleState")]
public class IdleState : AUnitState
{
    public override void OnEnter()
    {
        if (EntityManager.HasComponent<UnitMoveComponent>(Entity))
        {
            UnitMoveComponent move = EntityManager.GetComponentData<UnitMoveComponent>(Entity);
            move.ClearCommand();
            EntityManager.SetComponentData(Entity, move);
        }

        ClearAnimationFacingDirection();
    }

    public override void OnUpdate(float deltaTime) 
    {
        if (EntityManager.HasComponent<UnitMoveComponent>(Entity))
        {
            UnitMoveComponent move = EntityManager.GetComponentData<UnitMoveComponent>(Entity);
            move.ClearCommand();
            EntityManager.SetComponentData(Entity, move);
        }

        ClearAnimationFacingDirection();
    }
    public override void OnExit()
    {
        ClearAnimationFacingDirection();
    }
}
