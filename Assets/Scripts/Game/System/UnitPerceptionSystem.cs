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
            Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
            Factions = SystemAPI.GetComponentLookup<UnitFactionComponent>(true),
            Spectators = SystemAPI.GetComponentLookup<BattleSpectatorComponent>(true),
            DisabledUnits = SystemAPI.GetComponentLookup<Disabled>(true),
            ElapsedTime = SystemAPI.Time.ElapsedTime,
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

    [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
    [ReadOnly] public ComponentLookup<UnitFactionComponent> Factions;
    [ReadOnly] public ComponentLookup<BattleSpectatorComponent> Spectators;
    [ReadOnly] public ComponentLookup<Disabled> DisabledUnits;
    public double ElapsedTime;

    private void Execute(
        Entity entity,
        ref UnitPerceptionComponent perception,
        in LocalTransform transform,
        ref DynamicBuffer<UnitPerceptionUnitElement> nearbyEntities)
    {
        nearbyEntities.Clear();

        float radius = math.max(0f, perception.SearchRadius);
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
        if (radius > 0f)
            Tree.Query(in shape, UnitFactionMask.Combatants, ref visitor);

        Entity target = perception.DamageTarget;
        if (perception.HasDamageTarget(ElapsedTime) && target != entity &&
            Transforms.TryGetComponent(target, out LocalTransform targetTransform) &&
            Factions.TryGetComponent(target, out UnitFactionComponent targetFaction) &&
            targetFaction.Value == UnitFactionType.Player &&
            !Spectators.HasComponent(target) && !DisabledUnits.HasComponent(target) &&
            (!Deaths.HasComponent(target) || !Deaths.IsComponentEnabled(target)) &&
            (!DestroyFlags.HasComponent(target) || !DestroyFlags.IsComponentEnabled(target)))
        {
            UnitPerceptionUnitElement remembered = new()
            {
                Value = target,
                DistanceSq = math.distancesq(targetTransform.Position, center),
                Faction = targetFaction.Value,
                IsDamageTarget = 1,
            };
            bool found = false;
            for (int i = 0; i < nearbyEntities.Length; i++)
            {
                if (nearbyEntities[i].Value != target) continue;
                nearbyEntities[i] = remembered;
                found = true;
                break;
            }
            if (!found) nearbyEntities.Add(remembered);
        }
        else
        {
            perception.DamageTarget = Entity.Null;
            perception.DamageTargetUntil = 0;
        }

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
