using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
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
        BufferLookup<UnitQueryNode> nodes = SystemAPI.GetBufferLookup<UnitQueryNode>(true);
        BufferLookup<UnitQueryEntry> entries = SystemAPI.GetBufferLookup<UnitQueryEntry>(true);
        if (!nodes.TryGetBuffer(query.TreeEntity, out DynamicBuffer<UnitQueryNode> treeNodes) ||
            !entries.TryGetBuffer(query.TreeEntity, out DynamicBuffer<UnitQueryEntry> treeEntries))
        {
            return;
        }

        state.Dependency = new UnitPerceptionJob
        {
            Tree = new UnitQueryTree(treeNodes.AsNativeArray(), treeEntries.AsNativeArray()),
            Deaths = SystemAPI.GetComponentLookup<UnitDeathComponent>(true),
            DestroyFlags = SystemAPI.GetComponentLookup<DestroyEntityFlag>(true),
        }.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithNone(typeof(UnitDeathComponent), typeof(BattleSpectatorComponent))]
public partial struct UnitPerceptionJob : IJobEntity
{
    [ReadOnly]
    public UnitQueryTree Tree;

    [ReadOnly]
    public ComponentLookup<UnitDeathComponent> Deaths;

    [ReadOnly]
    public ComponentLookup<DestroyEntityFlag> DestroyFlags;

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
        UnitQueryShape shape = UnitQueryShape.Circle(center, radius);
        PerceptionVisitor visitor = new()
        {
            Observer = entity,
            Center = center,
            NearbyEntities = nearbyEntities,
            Deaths = Deaths,
            DestroyFlags = DestroyFlags,
        };
        Tree.Query(in shape, UnitFactionMask.Combatants, ref visitor);

        SortByEntity(ref nearbyEntities);
    }

    private struct PerceptionVisitor : IUnitQueryVisitor
    {
        public Entity Observer;
        public float3 Center;
        public DynamicBuffer<UnitPerceptionUnitElement> NearbyEntities;

        [ReadOnly]
        public ComponentLookup<UnitDeathComponent> Deaths;

        [ReadOnly]
        public ComponentLookup<DestroyEntityFlag> DestroyFlags;

        public bool Visit(in UnitQueryEntry entry)
        {
            if (entry.Entity == Observer || IsUnavailable(entry.Entity))
                return true;

            NearbyEntities.Add(new UnitPerceptionUnitElement
            {
                Value = entry.Entity,
                DistanceSq = math.distancesq(entry.Position, Center),
                Faction = entry.Faction,
            });
            return true;
        }

        private bool IsUnavailable(Entity entity)
        {
            return Deaths.HasComponent(entity) && Deaths.IsComponentEnabled(entity) ||
                   DestroyFlags.HasComponent(entity) && DestroyFlags.IsComponentEnabled(entity);
        }
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
