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
        ClearAnimationFacingDirection();
    }

    public override void OnUpdate(float deltaTime) 
    {
        var intent = EntityManager.GetComponentData<UnitIntentComponent>(Entity);
        var move = EntityManager.GetComponentData<UnitMoveComponent>(Entity);
        move.AccelInput = intent.MoveDirection;
        EntityManager.SetComponentData(Entity, move);
        ClearAnimationFacingDirection();
    }
    public override void OnExit()
    {
        ClearAnimationFacingDirection();
    }
}
