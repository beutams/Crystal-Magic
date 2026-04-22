using Unity.Mathematics;

/// <summary>
/// 移动状态——从 UnitIntentComponent 读取移动方向，写入 UnitMoveComponent.AccelInput。
/// MoveSystem 负责从加速度积分到速度。
/// </summary>
[FactoryKey("MoveState")]
public class MoveState : AUnitState
{
    public override void OnEnter() { }

    public override void OnUpdate(float deltaTime)
    {
        var intent = EntityManager.GetComponentData<UnitIntentComponent>(Entity);
        var move   = EntityManager.GetComponentData<UnitMoveComponent>(Entity);
        move.AccelInput = intent.MoveDirection;
        EntityManager.SetComponentData(Entity, move);
    }

    public override void OnExit()
    {
        var move = EntityManager.GetComponentData<UnitMoveComponent>(Entity);
        move.AccelInput = float2.zero;
        EntityManager.SetComponentData(Entity, move);
    }
}
