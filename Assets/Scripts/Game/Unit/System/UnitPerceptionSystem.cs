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
        GameGateComponent gameGate = GameGateComponent.Instance;
        if ((gameGate != null && gameGate.IsSimulationLocked) ||
            !UnitQueryUtility.TryGetTree(EntityManager, UnitQueryTreeKind.Unit, out UnitQueryTree unitTree))
        {
            return;
        }

        foreach (var (perception, transform, nearbyEntities, entity) in
                 SystemAPI.Query<RefRO<UnitPerceptionComponent>, RefRO<LocalTransform>, DynamicBuffer<UnitPerceptionEntityElement>>()
                     .WithNone<UnitDeathComponent>()
                     .WithEntityAccess())
        {
            nearbyEntities.Clear();

            float radius = math.max(0f, perception.ValueRO.SearchRadius);
            if (radius <= 0f)
                continue;

            float3 center = transform.ValueRO.Position;
            unitTree.QueryCircle(center, radius, _hits);

            for (int i = 0; i < _hits.Count; i++)
            {
                UnitQueryHit hit = _hits[i];
                if (hit.Entity == entity)
                    continue;
                if (!EntityManager.Exists(hit.Entity) || !EntityManager.HasComponent<UnitFactionComponent>(hit.Entity))
                    continue;
                if (EntityManager.HasComponent<DestroyEntityFlag>(hit.Entity) && EntityManager.IsComponentEnabled<DestroyEntityFlag>(hit.Entity))
                    continue;
                if (EntityManager.HasComponent<UnitDeathComponent>(hit.Entity) && EntityManager.IsComponentEnabled<UnitDeathComponent>(hit.Entity))
                    continue;
                nearbyEntities.Add(new UnitPerceptionEntityElement { Value = hit.Entity });
            }
        }
    }
}
