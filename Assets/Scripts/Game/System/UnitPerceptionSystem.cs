using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateBefore(typeof(BehaviorTreeSystem))]
[BurstCompile]
partial struct UnitPerceptionSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<UnitQuerySingleton>();
        state.RequireForUpdate<UnitPerceptionComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        UnitQuerySingleton query = SystemAPI.GetSingleton<UnitQuerySingleton>();
        BufferLookup<UnitQueryEntry> grids = SystemAPI.GetBufferLookup<UnitQueryEntry>(true);
        if (!grids.TryGetBuffer(
                query.UnitGridEntity,
                out DynamicBuffer<UnitQueryEntry> unitEntries))
        {
            return;
        }

        state.Dependency = new UnitPerceptionJob
        {
            UnitEntries = unitEntries.AsNativeArray(),
            InverseCellSize = query.InverseCellSize,
            Factions = SystemAPI.GetComponentLookup<UnitFactionComponent>(true),
            Deaths = SystemAPI.GetComponentLookup<UnitDeathComponent>(true),
            DestroyFlags = SystemAPI.GetComponentLookup<DestroyEntityFlag>(true),
        }.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithNone(typeof(UnitDeathComponent))]
public partial struct UnitPerceptionJob : IJobEntity
{
    [ReadOnly]
    public NativeArray<UnitQueryEntry> UnitEntries;

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
        if (!UnitQueryGrid.TryGetCellRange(
                UnitEntries,
                cellKey,
                out int startIndex,
                out int endIndex))
        {
            return;
        }

        for (int index = startIndex; index < endIndex; index++)
        {
            UnitQueryEntry entry = UnitEntries[index];
            if (entry.Entity == observer)
                continue;

            float2 planarDifference = entry.Position.xy - center.xy;
            if (math.lengthsq(planarDifference) > radiusSq ||
                !Factions.TryGetComponent(entry.Entity, out UnitFactionComponent faction) ||
                IsUnavailable(entry.Entity))
            {
                continue;
            }

            nearbyEntities.Add(new UnitPerceptionUnitElement
            {
                Value = entry.Entity,
                DistanceSq = math.distancesq(entry.Position, center),
                Faction = faction.Value,
            });
        }
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
