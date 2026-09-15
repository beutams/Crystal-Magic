using Unity.Entities;
using Unity.Mathematics;

[RunInGameWorld(GameWorldKind.Dungeon)]
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

            cooldownRef.ValueRW.SharedCooldownRemaining = math.max(
                0f,
                cooldownRef.ValueRO.SharedCooldownRemaining - deltaTime);
        }
    }
}
