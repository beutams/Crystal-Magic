using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(UnitMoveSystem))]
[UpdateBefore(typeof(UnitSkillExecuteSystem))]
partial class UnitJumpArcSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (jumpRef, transformRef, entity) in
                 SystemAPI.Query<RefRW<UnitJumpArcComponent>, RefRW<LocalTransform>>().WithEntityAccess())
        {
            UnitJumpArcComponent jump = jumpRef.ValueRW;
            LocalTransform transform = transformRef.ValueRW;

            if (jump.IsActive == 0)
                continue;

            if (EntityManager.HasComponent<UnitMoveComponent>(entity))
            {
                UnitMoveComponent move = EntityManager.GetComponentData<UnitMoveComponent>(entity);
                move.ClearCommand();
                move.Velocity = float2.zero;
                EntityManager.SetComponentData(entity, move);
            }

            if (EntityManager.HasComponent<PhysicsVelocity>(entity))
            {
                PhysicsVelocity physicsVelocity = EntityManager.GetComponentData<PhysicsVelocity>(entity);
                physicsVelocity.Linear = float3.zero;
                physicsVelocity.Angular = float3.zero;
                EntityManager.SetComponentData(entity, physicsVelocity);
            }

            float duration = math.max(0f, jump.Duration);
            if (duration <= 0f)
            {
                transform.Position = jump.EndPosition;
                transform.Rotation = quaternion.identity;
                jump.Elapsed = 0f;
                jump.IsActive = 0;
                jump.IsCompleted = 1;
            }
            else if (jump.IsCompleted == 0)
            {
                jump.Elapsed = math.min(duration, jump.Elapsed + math.max(0f, deltaTime));
                float t = math.saturate(jump.Elapsed / duration);
                float3 position = math.lerp(jump.StartPosition, jump.EndPosition, t);
                position.z += 4f * math.max(0f, jump.ArcHeight) * t * (1f - t);
                transform.Position = position;
                transform.Rotation = quaternion.identity;

                if (jump.Elapsed >= duration)
                {
                    transform.Position = jump.EndPosition;
                    jump.IsActive = 0;
                    jump.IsCompleted = 1;
                }
            }
            else
            {
                transform.Position = jump.EndPosition;
                transform.Rotation = quaternion.identity;
                jump.IsActive = 0;
            }

            jumpRef.ValueRW = jump;
            transformRef.ValueRW = transform;
        }
    }
}
