using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct UnitQueryHit
{
    public Entity Entity;
    public float3 Position;
}

partial class UnitQuerySystem : SystemBase
{
    public static UnitQuerySystem Default =>
        World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<UnitQuerySystem>();

    public EntityManager QueryEntityManager => EntityManager;

    protected override void OnUpdate()
    {
    }

    public void QueryCircle(float3 center, float radius, List<UnitQueryHit> results)
    {
        results.Clear();

        float radiusSq = radius * radius;
        foreach (var (transform, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>>()
                     .WithAll<UnitFactionComponent>()
                     .WithEntityAccess())
        {
            float3 position = transform.ValueRO.Position;
            float2 diff = position.xy - center.xy;
            if (math.lengthsq(diff) > radiusSq)
                continue;

            results.Add(new UnitQueryHit
            {
                Entity = entity,
                Position = position,
            });
        }
    }
}
