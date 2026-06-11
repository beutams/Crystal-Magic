using Unity.Burst;
using Unity.Entities;

[BurstCompile]
[UpdateInGroup(typeof(UnitPostProcessSystemGroup), OrderLast = true)]
[UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]
partial struct DestroyEntitySystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        foreach ((EnabledRefRO<DestroyEntityFlag> destroyFlag, Entity entity) in
                 SystemAPI.Query<EnabledRefRO<DestroyEntityFlag>>()
                     .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                     .WithEntityAccess())
        {
            if (!destroyFlag.ValueRO)
                continue;

            ecb.DestroyEntity(entity);
        }
    }
}
