using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// 待机状态——清零加速意图，MoveSystem 会自然减速到停。
/// </summary>
public class IdleState : AUnitState
{
    public override void OnEnter()
    {
    }

    public override void OnUpdate(float deltaTime) 
    {
        var intent = EntityManager.GetComponentData<UnitIntentComponent>(Entity);
        var move = EntityManager.GetComponentData<UnitMoveComponent>(Entity);
        move.AccelInput = intent.MoveDirection;
        EntityManager.SetComponentData(Entity, move);
    }
    public override void OnExit() { }
}
