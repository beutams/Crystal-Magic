using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
public partial struct PlayerPropCooldownSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        foreach (RefRW<PlayerPropCooldownComponent> cooldownRef in
                 SystemAPI.Query<RefRW<PlayerPropCooldownComponent>>())
        {
            if (cooldownRef.ValueRO.SharedCooldownRemaining <= 0f)
                continue;

            float nextCooldown = math.max(
                0f,
                cooldownRef.ValueRO.SharedCooldownRemaining - deltaTime);
            if (nextCooldown == cooldownRef.ValueRO.SharedCooldownRemaining)
                continue;

            cooldownRef.ValueRW.SharedCooldownRemaining = nextCooldown;
            if (nextCooldown <= 0f)
                cooldownRef.ValueRW.NetworkDirty = 1;
        }
    }
}
