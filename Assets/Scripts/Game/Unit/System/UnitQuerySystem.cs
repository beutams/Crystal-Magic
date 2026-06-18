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

public sealed class UnitQueryRuntimeComponent : IComponentData
{
    public UnitQueryTree UnitTree = new();
    public UnitQueryTree WorldDropTree = new();
}

[UpdateInGroup(typeof(UnitInitializationSystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(UnitPerceptionSystem))]
[UpdateBefore(typeof(SkillProjectileSystem))]
partial class UnitQueryBuildSystem : SystemBase
{
    private Entity _singletonEntity;
    private readonly List<UnitQueryHit> _unitEntries = new();
    private readonly List<UnitQueryHit> _worldDropEntries = new();

    protected override void OnCreate()
    {
        _singletonEntity = EntityManager.CreateEntity(typeof(UnitQuerySingleton));
        EntityManager.AddComponentObject(_singletonEntity, new UnitQueryRuntimeComponent());
    }

    protected override void OnUpdate()
    {
        _unitEntries.Clear();
        _worldDropEntries.Clear();

        foreach ((RefRO<LocalTransform> transform, Entity entity) in
                 SystemAPI.Query<RefRO<LocalTransform>>()
                     .WithAll<UnitFactionComponent>()
                     .WithEntityAccess())
        {
            _unitEntries.Add(new UnitQueryHit
            {
                Entity = entity,
                Position = transform.ValueRO.Position,
            });
        }

        foreach ((RefRO<LocalTransform> transform, Entity entity) in
                 SystemAPI.Query<RefRO<LocalTransform>>()
                     .WithAll<WorldDropComponent>()
                     .WithEntityAccess())
        {
            _worldDropEntries.Add(new UnitQueryHit
            {
                Entity = entity,
                Position = transform.ValueRO.Position,
            });
        }

        UnitQueryRuntimeComponent runtime = EntityManager.GetComponentObject<UnitQueryRuntimeComponent>(_singletonEntity);
        runtime.UnitTree.Rebuild(_unitEntries);
        runtime.WorldDropTree.Rebuild(_worldDropEntries);
    }
}
