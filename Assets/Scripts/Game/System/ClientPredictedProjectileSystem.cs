using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(ClientNetworkPresentationSystemGroup))]
[UpdateAfter(typeof(ClientSkillVisualExecutionSystem))]
[UpdateBefore(typeof(ClientPresentationEventSystem))]
public partial struct ClientPredictedProjectileSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ClientPredictedProjectileComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new ClientPredictedProjectileJob
        {
            DeltaTime = math.max(0f, SystemAPI.Time.DeltaTime),
        }.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
public partial struct ClientPredictedProjectileJob : IJobEntity
{
    public float DeltaTime;

    private void Execute(
        ref ClientPredictedProjectileComponent projectile,
        ref LocalTransform transform,
        EnabledRefRW<DestroyEntityFlag> destroyFlag)
    {
        if (destroyFlag.ValueRO)
            return;

        float moveDistance = projectile.Speed * DeltaTime;
        transform.Position += projectile.Direction * moveDistance;
        float2 planar = math.normalizesafe(projectile.Direction.xy, new float2(1f, 0f));
        transform.Rotation = quaternion.RotateZ(math.atan2(planar.y, planar.x));
        projectile.TraveledDistance += math.abs(moveDistance);
        if (projectile.MaxRange > 0f && projectile.TraveledDistance >= projectile.MaxRange)
            destroyFlag.ValueRW = true;
    }
}
