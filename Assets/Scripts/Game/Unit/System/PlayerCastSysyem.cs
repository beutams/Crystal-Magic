using Unity.Burst;
using Unity.Entities;

[BurstCompile]
partial struct PlayerCastSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerTag>();
        state.RequireForUpdate<UnitIntentComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (_, intent) in SystemAPI.Query<RefRO<PlayerTag>, RefRO<UnitIntentComponent>>())
        {
            if (intent.ValueRO.WantToCast)
            {

            }
        }
    }
}
