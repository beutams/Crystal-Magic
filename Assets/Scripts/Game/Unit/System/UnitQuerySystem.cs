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

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(UnitPerceptionSystem))]
[UpdateBefore(typeof(CrystalMagic.Game.Skill.Effects.SkillProjectileSystem))]
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

public static class UnitQueryUtility
{
    public static void QueryCircle(DynamicBuffer<UnitQueryEntry> entries, float3 center, float radius, List<UnitQueryHit> results)
    {
        results.Clear();

        float radiusSq = radius * radius;
        for (int i = 0; i < entries.Length; i++)
        {
            UnitQueryEntry entry = entries[i];
            float2 diff = entry.Position.xy - center.xy;
            if (math.lengthsq(diff) > radiusSq)
                continue;

            results.Add(new UnitQueryHit
            {
                Entity = entry.Entity,
                Position = entry.Position,
            });
        }
    }

    public static bool TryQueryCircle(EntityManager entityManager, float3 center, float radius, List<UnitQueryHit> results)
    {
        EntityQuery singletonQuery = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<UnitQuerySingleton>(),
            ComponentType.ReadOnly<UnitQueryEntry>());
        if (singletonQuery.IsEmptyIgnoreFilter)
        {
            results.Clear();
            return false;
        }

        Entity singletonEntity = singletonQuery.GetSingletonEntity();
        DynamicBuffer<UnitQueryEntry> entries = entityManager.GetBuffer<UnitQueryEntry>(singletonEntity, true);
        QueryCircle(entries, center, radius, results);
        return true;
    }
}
