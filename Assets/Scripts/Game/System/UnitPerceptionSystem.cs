using System.Collections.Generic;
using CrystalMagic.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateBefore(typeof(BehaviorTreeSystem))]
partial class UnitPerceptionSystem : SystemBase
{
    private EntityQuery _queryRuntimeQuery;

    protected override void OnCreate()
    {
        _queryRuntimeQuery = GetEntityQuery(
            ComponentType.ReadOnly<UnitQuerySingleton>(),
            ComponentType.ReadOnly<UnitQueryRuntimeComponent>());
        RequireForUpdate(_queryRuntimeQuery);
        RequireForUpdate<UnitPerceptionComponent>();
    }

    protected override void OnUpdate()
    {
        GameGateComponent gameGate = GameGateComponent.Instance;
        if (gameGate != null && gameGate.IsSimulationLocked)
            return;

        Entity queryEntity = _queryRuntimeQuery.GetSingletonEntity();
        UnitQueryRuntimeComponent runtime = EntityManager.GetComponentObject<UnitQueryRuntimeComponent>(queryEntity);
        if (runtime?.UnitGrid == null || !runtime.UnitGrid.IsCreated)
            return;

        Dependency = new UnitPerceptionJob
        {
            UnitEntries = runtime.UnitGrid.AsReadOnly(),
            InverseCellSize = runtime.UnitGrid.InverseCellSize,
            Factions = GetComponentLookup<UnitFactionComponent>(true),
            Deaths = GetComponentLookup<UnitDeathComponent>(true),
            DestroyFlags = GetComponentLookup<DestroyEntityFlag>(true),
        }.ScheduleParallel(Dependency);

        // BehaviorTreeSystem consumes the buffers immediately after this system.
        Dependency.Complete();
    }
}

[BurstCompile]
[WithNone(typeof(UnitDeathComponent))]
public partial struct UnitPerceptionJob : IJobEntity
{
    [ReadOnly]
    public NativeParallelMultiHashMap<long, UnitQueryHit>.ReadOnly UnitEntries;

    [ReadOnly]
    public ComponentLookup<UnitFactionComponent> Factions;

    [ReadOnly]
    public ComponentLookup<UnitDeathComponent> Deaths;

    [ReadOnly]
    public ComponentLookup<DestroyEntityFlag> DestroyFlags;

    public float InverseCellSize;

    private void Execute(
        Entity entity,
        in UnitPerceptionComponent perception,
        in LocalTransform transform,
        ref DynamicBuffer<UnitPerceptionUnitElement> nearbyEntities)
    {
        nearbyEntities.Clear();

        float radius = math.max(0f, perception.SearchRadius);
        if (radius <= 0f)
            return;

        float3 center = transform.Position;
        float radiusSq = radius * radius;
        int2 minCell = (int2)math.floor((center.xy - radius) * InverseCellSize);
        int2 maxCell = (int2)math.floor((center.xy + radius) * InverseCellSize);

        for (int y = minCell.y; y <= maxCell.y; y++)
        {
            for (int x = minCell.x; x <= maxCell.x; x++)
                AddCellUnits(entity, center, radiusSq, new int2(x, y), ref nearbyEntities);
        }

        SortByEntity(ref nearbyEntities);
    }

    private void AddCellUnits(
        Entity observer,
        float3 center,
        float radiusSq,
        int2 cell,
        ref DynamicBuffer<UnitPerceptionUnitElement> nearbyEntities)
    {
        long cellKey = ((long)cell.x << 32) | (uint)cell.y;
        if (!UnitEntries.TryGetFirstValue(
                cellKey,
                out UnitQueryHit hit,
                out NativeParallelMultiHashMapIterator<long> iterator))
        {
            return;
        }

        do
        {
            if (hit.Entity == observer)
                continue;

            float2 planarDifference = hit.Position.xy - center.xy;
            if (math.lengthsq(planarDifference) > radiusSq ||
                !Factions.TryGetComponent(hit.Entity, out UnitFactionComponent faction) ||
                IsUnavailable(hit.Entity))
            {
                continue;
            }

            nearbyEntities.Add(new UnitPerceptionUnitElement
            {
                Value = hit.Entity,
                DistanceSq = math.distancesq(hit.Position, center),
                Faction = faction.Value,
            });
        } while (UnitEntries.TryGetNextValue(out hit, ref iterator));
    }

    private bool IsUnavailable(Entity entity)
    {
        return Deaths.HasComponent(entity) && Deaths.IsComponentEnabled(entity) ||
               DestroyFlags.HasComponent(entity) && DestroyFlags.IsComponentEnabled(entity);
    }

    private static void SortByEntity(ref DynamicBuffer<UnitPerceptionUnitElement> units)
    {
        if (units.Length > 1)
            units.AsNativeArray().Sort(new UnitPerceptionEntityComparer());
    }

    private struct UnitPerceptionEntityComparer : IComparer<UnitPerceptionUnitElement>
    {
        public int Compare(UnitPerceptionUnitElement left, UnitPerceptionUnitElement right)
        {
            if (left.Value.Index != right.Value.Index)
                return left.Value.Index < right.Value.Index ? -1 : 1;
            if (left.Value.Version == right.Value.Version)
                return 0;
            return left.Value.Version < right.Value.Version ? -1 : 1;
        }
    }
}
