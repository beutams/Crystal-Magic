using CrystalMagic.Game.Skill;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

[FactoryKey("DeathState")]
public sealed class DeathState : AUnitState
{
    public override void OnEnter()
    {
        StopUnitActions();
    }

    public override void OnUpdate(float deltaTime)
    {
        StopUnitActions();
    }

    public override void OnExit()
    {
    }

    private void StopUnitActions()
    {
        if (EntityManager.HasComponent<UnitIntentComponent>(Entity))
        {
            UnitIntentComponent intent = EntityManager.GetComponentData<UnitIntentComponent>(Entity);
            intent.ClearFrameIntent();
            EntityManager.SetComponentData(Entity, intent);
        }

        if (EntityManager.HasComponent<UnitMoveComponent>(Entity))
        {
            UnitMoveComponent move = EntityManager.GetComponentData<UnitMoveComponent>(Entity);
            move.ClearTargetMovement();
            move.Velocity = float2.zero;
            EntityManager.SetComponentData(Entity, move);
        }

        if (EntityManager.HasComponent<PhysicsVelocity>(Entity))
        {
            PhysicsVelocity velocity = EntityManager.GetComponentData<PhysicsVelocity>(Entity);
            velocity.Linear = float3.zero;
            velocity.Angular = float3.zero;
            EntityManager.SetComponentData(Entity, velocity);
        }

        if (EntityManager.HasComponent<PhysicsCollider>(Entity))
        {
            PhysicsCollider collider = EntityManager.GetComponentData<PhysicsCollider>(Entity);
            collider.Value = default;
            EntityManager.SetComponentData(Entity, collider);
        }

        if (EntityManager.HasComponent<UnitJumpArcComponent>(Entity))
        {
            UnitJumpArcComponent jump = EntityManager.GetComponentData<UnitJumpArcComponent>(Entity);
            jump.IsActive = 0;
            jump.IsCompleted = 1;
            EntityManager.SetComponentData(Entity, jump);
        }

        if (EntityManager.HasComponent<UnitCastComponent>(Entity))
        {
            UnitCastComponent cast = EntityManager.GetComponentData<UnitCastComponent>(Entity);
            SkillExecutionUtility.ResetCastState(EntityManager, Entity, ref cast);
            SkillExecutionUtility.ClearFollowupEffects(EntityManager, Entity);
            EntityManager.SetComponentData(Entity, cast);
        }

        if (EntityManager.HasComponent<UnitSkillComponent>(Entity))
        {
            UnitSkillComponent skills = EntityManager.GetComponentData<UnitSkillComponent>(Entity);
            skills.ClearPending();
            EntityManager.SetComponentData(Entity, skills);
        }

        if (EntityManager.HasComponent<PlayerSkillComponent>(Entity))
        {
            PlayerSkillComponent skills = EntityManager.GetComponentData<PlayerSkillComponent>(Entity);
            skills.Clear();
            EntityManager.SetComponentData(Entity, skills);
        }
    }
}
