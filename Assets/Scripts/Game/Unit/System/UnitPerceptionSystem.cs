using System.Collections.Generic;
using CrystalMagic.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateBefore(typeof(BehaviorTreeSystem))]
partial class UnitPerceptionSystem : SystemBase
{
    private readonly List<UnitQueryHit> _hits = new();

    protected override void OnUpdate()
    {
        if (GameGateComponent.Instance != null && GameGateComponent.Instance.IsSimulationLocked)
            return;

        UnitQuerySystem querySystem = UnitQuerySystem.Default;
        if (querySystem == null)
            return;

        foreach (var (perception, faction, transform, entity) in
                 SystemAPI.Query<RefRW<UnitPerceptionComponent>, RefRO<UnitFactionComponent>, RefRO<LocalTransform>>()
                     .WithAll<UnitAITag>()
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
            querySystem.QueryCircle(center, radius, _hits);

            float bestDistanceSq = float.MaxValue;
            for (int i = 0; i < _hits.Count; i++)
            {
                UnitQueryHit hit = _hits[i];
                if (hit.Entity == entity)
                    continue;
                if (!EntityManager.Exists(hit.Entity) || !EntityManager.HasComponent<UnitFactionComponent>(hit.Entity))
                    continue;
                if (!IsEnemy(faction.ValueRO.Value, EntityManager.GetComponentData<UnitFactionComponent>(hit.Entity).Value))
                    continue;
                if (EntityManager.HasComponent<UnitVitalityComponent>(hit.Entity) &&
                    EntityManager.GetComponentData<UnitVitalityComponent>(hit.Entity).CurrentHealth <= 0f)
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

    private static bool IsEnemy(UnitFactionType self, UnitFactionType other)
    {
        if (self == UnitFactionType.Enemy)
            return other != UnitFactionType.Enemy;

        return other == UnitFactionType.Enemy;
    }
}
