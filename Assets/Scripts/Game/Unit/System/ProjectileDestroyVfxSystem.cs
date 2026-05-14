using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SkillProjectileSystem))]
[UpdateBefore(typeof(DestroyEntitySystem))]
public partial class ProjectileDestroyVfxSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((EnabledRefRO<DestroyEntityFlag> destroyFlag,
                  RefRW<ProjectileDestroyVfxComponent> destroyVfx,
                  Entity entity) in
                 SystemAPI.Query<EnabledRefRO<DestroyEntityFlag>, RefRW<ProjectileDestroyVfxComponent>>()
                     .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                     .WithEntityAccess())
        {
            if (destroyFlag.ValueRO)
                continue;

            destroyVfx.ValueRW.RemainingLifetime -= deltaTime;
            if (destroyVfx.ValueRW.RemainingLifetime > 0f)
                continue;

            if (!EntityManager.HasComponent<DestroyEntityFlag>(entity))
                EntityManager.AddComponent<DestroyEntityFlag>(entity);

            EntityManager.SetComponentEnabled<DestroyEntityFlag>(entity, true);
        }
    }
}
