using Unity.Mathematics;

/// <summary>
/// 移动状态——从 UnitIntentComponent 读取移动方向，写入移动命令。
/// MoveSystem 负责把命令推进为实际速度。
/// </summary>
[FactoryKey("MoveState")]
public class MoveState : AUnitState
{
    public override void OnEnter()
    {
    }

    public override void OnUpdate(float deltaTime)
    {
        ApplyMoveIntent();
    }

    public override void OnExit()
    {
    }

    private void ApplyMoveIntent()
    {
        if (!EntityManager.HasComponent<UnitIntentComponent>(Entity) ||
            !EntityManager.HasComponent<UnitMoveComponent>(Entity))
        {
            return;
        }

        UnitIntentComponent intent = EntityManager.GetComponentData<UnitIntentComponent>(Entity);
        UnitMoveComponent move = EntityManager.GetComponentData<UnitMoveComponent>(Entity);
        move.SetTargetMovementByFactor(intent.MoveDirection);
        EntityManager.SetComponentData(Entity, move);

        if (math.lengthsq(intent.MoveDirection) > 0.0001f)
            UnitFacingUtility.SetFacing(EntityManager, Entity, intent.MoveDirection);
    }
}
