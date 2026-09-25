using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(UnitInitializationSystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(UnitPerceptionSystem))]
[UpdateBefore(typeof(SkillProjectileSystem))]
partial struct UnitQueryBuildSystem : ISystem
{
    private const int LeafCapacity = 16;
    private const int MaxDepth = 10;

    private Entity _singletonEntity;
    private Entity _treeEntity;
    private EntityQuery _unitQuery;

    public void OnCreate(ref SystemState state)
    {
        _unitQuery = state.GetEntityQuery(new EntityQueryDesc
        {
            All = new[]
            {
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<UnitFactionComponent>(),
            },
            None = new[]
            {
                ComponentType.ReadOnly<UnitInitializationPendingTag>(),
                ComponentType.ReadOnly<BattleSpectatorComponent>(),
            },
        });

        _treeEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddBuffer<UnitQueryEntry>(_treeEntity);
        state.EntityManager.AddBuffer<UnitQueryScratchEntry>(_treeEntity);
        state.EntityManager.AddBuffer<UnitQueryNode>(_treeEntity);

        _singletonEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(_singletonEntity, new UnitQuerySingleton
        {
            TreeEntity = _treeEntity,
        });
    }

    public void OnUpdate(ref SystemState state)
    {
        // Buffer lengths and capacities change during a rebuild. Complete last frame's
        // readers before exposing new aliases to the build jobs.
        state.Dependency.Complete();

        int entryCount = _unitQuery.CalculateEntityCount();
        DynamicBuffer<UnitQueryEntry> entries =
            state.EntityManager.GetBuffer<UnitQueryEntry>(_treeEntity);
        DynamicBuffer<UnitQueryScratchEntry> scratch =
            state.EntityManager.GetBuffer<UnitQueryScratchEntry>(_treeEntity);
        DynamicBuffer<UnitQueryNode> nodes =
            state.EntityManager.GetBuffer<UnitQueryNode>(_treeEntity);

        entries.ResizeUninitialized(entryCount);
        scratch.ResizeUninitialized(entryCount);
        nodes.Clear();
        nodes.EnsureCapacity(math.max(1, entryCount * 4 + 1));

        JobHandle gatherHandle = new UnitQueryGatherJob
        {
            Entries = entries.AsNativeArray(),
            StateScripts = state.GetComponentLookup<UnitStateScriptComponent>(true),
            BehaviorTrees = state.GetComponentLookup<UnitBehaviorTreeComponent>(true),
            Deaths = state.GetComponentLookup<UnitDeathComponent>(true),
            DestroyFlags = state.GetComponentLookup<DestroyEntityFlag>(true),
        }.ScheduleParallel(_unitQuery, default);

        state.Dependency = new UnitQueryTreeBuildJob
        {
            Entries = entries,
            Scratch = scratch,
            Nodes = nodes,
            LeafCapacity = LeafCapacity,
            MaxDepth = MaxDepth,
        }.Schedule(gatherHandle);

        // Managed skill effects query the tree later in this frame. Keep one immutable
        // tree snapshot visible to both managed callers and Burst jobs.
        state.Dependency.Complete();
    }

    public void OnDestroy(ref SystemState state)
    {
        state.Dependency.Complete();
        if (state.EntityManager.Exists(_singletonEntity))
            state.EntityManager.DestroyEntity(_singletonEntity);
        if (state.EntityManager.Exists(_treeEntity))
            state.EntityManager.DestroyEntity(_treeEntity);
    }
}

[BurstCompile]
public partial struct UnitQueryGatherJob : IJobEntity
{
    [NativeDisableParallelForRestriction]
    public NativeArray<UnitQueryEntry> Entries;

    [ReadOnly]
    public ComponentLookup<UnitStateScriptComponent> StateScripts;

    [ReadOnly]
    public ComponentLookup<UnitBehaviorTreeComponent> BehaviorTrees;

    [ReadOnly]
    public ComponentLookup<UnitDeathComponent> Deaths;

    [ReadOnly]
    public ComponentLookup<DestroyEntityFlag> DestroyFlags;

    private void Execute(
        [EntityIndexInQuery] int index,
        Entity entity,
        in LocalTransform transform,
        in UnitFactionComponent faction)
    {
        int unitDataId = StateScripts.TryGetComponent(entity, out UnitStateScriptComponent stateScript)
            ? stateScript.UnitDataId
            : BehaviorTrees.TryGetComponent(entity, out UnitBehaviorTreeComponent behaviorTree)
                ? behaviorTree.UnitDataId
                : -1;
        Entries[index] = new UnitQueryEntry
        {
            Entity = entity,
            Position = transform.Position,
            Faction = faction.Value,
            UnitDataId = unitDataId,
            IsDead = Deaths.HasComponent(entity) && Deaths.IsComponentEnabled(entity) ||
                     DestroyFlags.HasComponent(entity) && DestroyFlags.IsComponentEnabled(entity)
                ? (byte)1
                : (byte)0,
        };
    }
}

[BurstCompile]
public struct UnitQueryTreeBuildJob : IJob
{
    public DynamicBuffer<UnitQueryEntry> Entries;
    public DynamicBuffer<UnitQueryScratchEntry> Scratch;
    public DynamicBuffer<UnitQueryNode> Nodes;
    public int LeafCapacity;
    public int MaxDepth;

    public void Execute()
    {
        Nodes.Clear();
        if (Entries.Length == 0)
            return;

        float2 minimum = Entries[0].Position.xy;
        float2 maximum = minimum;
        for (int index = 1; index < Entries.Length; index++)
        {
            float2 position = Entries[index].Position.xy;
            minimum = math.min(minimum, position);
            maximum = math.max(maximum, position);
        }

        float2 center = (minimum + maximum) * 0.5f;
        float halfExtent = math.max(0.5f, math.cmax(maximum - minimum) * 0.5f + 0.001f);
        Nodes.Add(new UnitQueryNode
        {
            Min = center - halfExtent,
            Max = center + halfExtent,
            StartIndex = 0,
            Count = Entries.Length,
            FirstChildIndex = -1,
            Depth = 0,
        });

        // Breadth-first processing avoids recursion and keeps the entire build Burst-compatible.
        for (int nodeIndex = 0; nodeIndex < Nodes.Length; nodeIndex++)
        {
            UnitQueryNode node = Nodes[nodeIndex];
            if (node.Count <= LeafCapacity || node.Depth >= MaxDepth)
                continue;

            float2 nodeCenter = (node.Min + node.Max) * 0.5f;
            int4 counts = int4.zero;
            for (int entryIndex = node.StartIndex;
                 entryIndex < node.StartIndex + node.Count;
                 entryIndex++)
            {
                counts[GetQuadrant(Entries[entryIndex].Position.xy, nodeCenter)]++;
            }

            int4 starts = new(
                node.StartIndex,
                node.StartIndex + counts.x,
                node.StartIndex + counts.x + counts.y,
                node.StartIndex + counts.x + counts.y + counts.z);
            int4 cursors = starts;
            for (int entryIndex = node.StartIndex;
                 entryIndex < node.StartIndex + node.Count;
                 entryIndex++)
            {
                UnitQueryEntry entry = Entries[entryIndex];
                int quadrant = GetQuadrant(entry.Position.xy, nodeCenter);
                Scratch[cursors[quadrant]++] = new UnitQueryScratchEntry { Value = entry };
            }

            for (int entryIndex = node.StartIndex;
                 entryIndex < node.StartIndex + node.Count;
                 entryIndex++)
            {
                Entries[entryIndex] = Scratch[entryIndex].Value;
            }

            node.FirstChildIndex = Nodes.Length;
            Nodes[nodeIndex] = node;
            for (int quadrant = 0; quadrant < 4; quadrant++)
            {
                GetChildBounds(in node, nodeCenter, quadrant, out float2 childMin, out float2 childMax);
                Nodes.Add(new UnitQueryNode
                {
                    Min = childMin,
                    Max = childMax,
                    StartIndex = starts[quadrant],
                    Count = counts[quadrant],
                    FirstChildIndex = -1,
                    Depth = (byte)(node.Depth + 1),
                });
            }
        }
    }

    private static int GetQuadrant(float2 position, float2 center)
    {
        return (position.x >= center.x ? 1 : 0) |
               (position.y >= center.y ? 2 : 0);
    }

    private static void GetChildBounds(
        in UnitQueryNode parent,
        float2 center,
        int quadrant,
        out float2 minimum,
        out float2 maximum)
    {
        bool right = (quadrant & 1) != 0;
        bool top = (quadrant & 2) != 0;
        minimum = new float2(right ? center.x : parent.Min.x, top ? center.y : parent.Min.y);
        maximum = new float2(right ? parent.Max.x : center.x, top ? parent.Max.y : center.y);
    }
}
