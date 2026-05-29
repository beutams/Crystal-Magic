using Unity.Mathematics;

[FactoryKey("ControlledState")]
public class ControlledState : AUnitState
{
    public override void OnEnter()
    {
        ClearControlledIntent();
        ClearAnimationFacingDirection();
    }

    public override void OnUpdate(float deltaTime)
    {
        ClearControlledIntent();
        ClearAnimationFacingDirection();
    }

    public override void OnExit()
    {
        ClearAnimationFacingDirection();
    }

    private void ClearControlledIntent()
    {
        if (EntityManager.HasComponent<UnitIntentComponent>(Entity))
        {
            UnitIntentComponent intent = EntityManager.GetComponentData<UnitIntentComponent>(Entity);
            intent.MoveDirection = float2.zero;
            intent.WantToCast = false;
            EntityManager.SetComponentData(Entity, intent);
        }

        if (EntityManager.HasComponent<UnitMoveComponent>(Entity))
        {
            UnitMoveComponent move = EntityManager.GetComponentData<UnitMoveComponent>(Entity);
            move.AccelInput = float2.zero;
            EntityManager.SetComponentData(Entity, move);
        }
    }
}
