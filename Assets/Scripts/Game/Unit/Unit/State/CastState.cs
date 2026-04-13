using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// 施法状态
/// </summary>
public class CastState : AUnitState
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
