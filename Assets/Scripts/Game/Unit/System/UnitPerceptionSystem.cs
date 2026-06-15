using System.Collections.Generic;
using CrystalMagic.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateBefore(typeof(BehaviorTreeSystem))]
partial class UnitPerceptionSystem : SystemBase
{
    private readonly List<UnitQueryHit> _hits = new();

    protected override void OnUpdate()
    {
        if (GameGateComponent.Instance.IsSimulationLocked || !SystemAPI.HasSingleton<UnitQuerySingleton>())
            return;

        DynamicBuffer<UnitQueryEntry> queryEntries = SystemAPI.GetSingletonBuffer<UnitQueryEntry>(true);

        foreach (var (perception, faction, transform, entity) in
                 SystemAPI.Query<RefRW<UnitPerceptionComponent>, RefRO<UnitFactionComponent>, RefRO<LocalTransform>>()
                     .WithEntityAccess())
        {
            UnitPerceptionComponent perceptionValue = perception.ValueRW;
            perceptionValue.HasTarget = false;
            perceptionValue.TargetEntity = Entity.Null;
            perceptionValue.TargetPosition = float2.zero;
            perceptionValue.TargetDistance = 0f;

            float radius = math.max(0f, perceptionValue.SearchRadius);
            if (radius <= 0f)
            {
                perception.ValueRW = perceptionValue;
                continue;
            }

            float3 center = transform.ValueRO.Position;
            UnitQueryUtility.QueryCircle(queryEntries, center, radius, _hits);

            float bestDistanceSq = float.MaxValue;
            for (int i = 0; i < _hits.Count; i++)
            {
                UnitQueryHit hit = _hits[i];
                if (hit.Entity == entity)
                    continue;
                if (!EntityManager.Exists(hit.Entity) || !EntityManager.HasComponent<UnitFactionComponent>(hit.Entity))
                    continue;
                if (!UnitFactionUtility.IsEnemy(faction.ValueRO.Value,EntityManager.GetComponentData<UnitFactionComponent>(hit.Entity).Value))
                    continue;
                if (EntityManager.HasComponent<DestroyEntityFlag>(hit.Entity) && EntityManager.IsComponentEnabled<DestroyEntityFlag>(hit.Entity))
                    continue;

                float2 diff = hit.Position.xy - center.xy;
                float distanceSq = math.lengthsq(diff);
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                perceptionValue.HasTarget = true;
                perceptionValue.TargetEntity = hit.Entity;
                perceptionValue.TargetPosition = hit.Position.xy;
                perceptionValue.TargetDistance = math.sqrt(distanceSq);
            }

            perception.ValueRW = perceptionValue;
        }
    }
}
