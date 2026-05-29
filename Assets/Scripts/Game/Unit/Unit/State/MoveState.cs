using Unity.Mathematics;

/// <summary>
/// 移动状态——从 UnitIntentComponent 读取移动方向，写入 UnitMoveComponent.AccelInput。
/// MoveSystem 负责从加速度积分到速度。
/// </summary>
[FactoryKey("MoveState")]
public class MoveState : AUnitState
{
    public override void OnEnter()
    {
        var intent = EntityManager.GetComponentData<UnitIntentComponent>(Entity);
        var move = EntityManager.GetComponentData<UnitMoveComponent>(Entity);
        move.AccelInput = intent.MoveDirection;
        EntityManager.SetComponentData(Entity, move);
        ApplyAnimationFacing(intent.MoveDirection);
    }

    public override void OnUpdate(float deltaTime)
    {
        var intent = EntityManager.GetComponentData<UnitIntentComponent>(Entity);
        var move   = EntityManager.GetComponentData<UnitMoveComponent>(Entity);
        move.AccelInput = intent.MoveDirection;
        EntityManager.SetComponentData(Entity, move);
        ApplyAnimationFacing(intent.MoveDirection);
    }

    public override void OnExit()
    {
        var move = EntityManager.GetComponentData<UnitMoveComponent>(Entity);
        move.AccelInput = float2.zero;
        EntityManager.SetComponentData(Entity, move);
        ClearAnimationFacingDirection();
    }

    private void ApplyAnimationFacing(float2 direction)
    {
        if (math.lengthsq(direction) <= 0.0001f)
        {
            ClearAnimationFacingDirection();
            return;
        }

        SetAnimationFacingDirection(direction);
    }
}
