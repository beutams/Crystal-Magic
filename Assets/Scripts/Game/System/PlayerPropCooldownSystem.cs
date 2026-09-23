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
        float deltaTime = SystemAPI.Time.DeltaTime;
        foreach (RefRW<PlayerPropCooldownComponent> cooldownRef in
                 SystemAPI.Query<RefRW<PlayerPropCooldownComponent>>())
        {
            ref PlayerPropCooldownComponent cooldown = ref cooldownRef.ValueRW;
            if (cooldown.SharedCooldownRemaining <= 0f)
                continue;

            float nextCooldown = math.max(
                0f,
                cooldown.SharedCooldownRemaining - deltaTime);
            if (nextCooldown == cooldown.SharedCooldownRemaining)
                continue;

            cooldown.SharedCooldownRemaining = nextCooldown;
            if (nextCooldown <= 0f)
                cooldown.NetworkDirty = 1;
        }
    }
}
