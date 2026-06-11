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

public static class UnitQueryUtility
{
    public static bool TryGetEntries(EntityManager entityManager, out DynamicBuffer<UnitQueryEntry> entries)
    {
        EntityQuery singletonQuery = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<UnitQuerySingleton>(),
            ComponentType.ReadOnly<UnitQueryEntry>());
        if (singletonQuery.IsEmptyIgnoreFilter)
        {
            entries = default;
            return false;
        }

        Entity singletonEntity = singletonQuery.GetSingletonEntity();
        entries = entityManager.GetBuffer<UnitQueryEntry>(singletonEntity, true);
        return true;
    }

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

    public static void QueryForwardRect(
        DynamicBuffer<UnitQueryEntry> entries,
        float3 origin,
        float2 forward,
        float length,
        float width,
        List<UnitQueryHit> results)
    {
        results.Clear();

        if (length <= 0f || width <= 0f || math.lengthsq(forward) <= 0.0001f)
            return;

        float2 normalizedForward = math.normalize(forward);
        float2 right = new float2(-normalizedForward.y, normalizedForward.x);
        float halfWidth = width * 0.5f;

        for (int i = 0; i < entries.Length; i++)
        {
            UnitQueryEntry entry = entries[i];
            float2 diff = entry.Position.xy - origin.xy;
            float forwardDistance = math.dot(diff, normalizedForward);
            if (forwardDistance < 0f || forwardDistance > length)
                continue;

            float lateralDistance = math.abs(math.dot(diff, right));
            if (lateralDistance > halfWidth)
                continue;

            results.Add(new UnitQueryHit
            {
                Entity = entry.Entity,
                Position = entry.Position,
            });
        }
    }

    public static void QueryCone(
        DynamicBuffer<UnitQueryEntry> entries,
        float3 origin,
        float2 forward,
        float radius,
        float angleDegrees,
        List<UnitQueryHit> results)
    {
        results.Clear();

        if (radius <= 0f || angleDegrees <= 0f || math.lengthsq(forward) <= 0.0001f)
            return;

        float2 normalizedForward = math.normalize(forward);
        float radiusSq = radius * radius;
        float minDot = math.cos(math.radians(math.min(360f, math.max(0f, angleDegrees)) * 0.5f));

        for (int i = 0; i < entries.Length; i++)
        {
            UnitQueryEntry entry = entries[i];
            float2 diff = entry.Position.xy - origin.xy;
            float distanceSq = math.lengthsq(diff);
            if (distanceSq > radiusSq)
                continue;

            if (distanceSq <= 0.0001f)
            {
                results.Add(new UnitQueryHit
                {
                    Entity = entry.Entity,
                    Position = entry.Position,
                });
                continue;
            }

            float2 normalizedDiff = diff * math.rsqrt(distanceSq);
            if (math.dot(normalizedForward, normalizedDiff) < minDot)
                continue;

            results.Add(new UnitQueryHit
            {
                Entity = entry.Entity,
                Position = entry.Position,
            });
        }
    }

}
