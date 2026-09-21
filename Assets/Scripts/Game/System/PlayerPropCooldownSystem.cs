using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
public partial struct PlayerPropCooldownSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPropCooldownComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new PlayerPropCooldownJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
        }.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
public partial struct PlayerPropCooldownJob : IJobEntity
{
    public float DeltaTime;

    private void Execute(ref PlayerPropCooldownComponent cooldown)
    {
        if (cooldown.SharedCooldownRemaining <= 0f)
            return;

        float nextCooldown = math.max(
            0f,
            cooldown.SharedCooldownRemaining - DeltaTime);
        if (nextCooldown == cooldown.SharedCooldownRemaining)
            return;

        cooldown.SharedCooldownRemaining = nextCooldown;
        if (nextCooldown <= 0f)
            cooldown.NetworkDirty = 1;
    }
}
