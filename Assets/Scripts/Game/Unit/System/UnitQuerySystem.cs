using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct UnitQueryHit
{
    public Entity Entity;
    public float3 Position;
}

public struct UnitQuerySingleton : IComponentData
{
}

public struct UnitQueryEntry : IBufferElementData
{
    public Entity Entity;
    public float3 Position;
}

[UpdateInGroup(typeof(UnitInitializationSystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(UnitPerceptionSystem))]
[UpdateBefore(typeof(SkillProjectileSystem))]
partial class UnitQueryBuildSystem : SystemBase
{
    private Entity _singletonEntity;

    protected override void OnCreate()
    {
        _singletonEntity = EntityManager.CreateEntity(typeof(UnitQuerySingleton));
        EntityManager.AddBuffer<UnitQueryEntry>(_singletonEntity);
    }

    protected override void OnUpdate()
    {
        DynamicBuffer<UnitQueryEntry> entries = EntityManager.GetBuffer<UnitQueryEntry>(_singletonEntity);
        entries.Clear();

        foreach ((RefRO<LocalTransform> transform, Entity entity) in
                 SystemAPI.Query<RefRO<LocalTransform>>()
                     .WithAll<UnitFactionComponent>()
                     .WithEntityAccess())
        {
            entries.Add(new UnitQueryEntry
            {
                Entity = entity,
                Position = transform.ValueRO.Position,
            });
        }
    }
}
