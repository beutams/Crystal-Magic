using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation |
                   WorldSystemFilterFlags.ClientSimulation |
                   WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitInitializationSystemGroup))]
[UpdateAfter(typeof(UnitQueryBuildSystem))]
public partial struct UnitAoiBuildSystem : ISystem
{
    private Entity _singletonEntity;
    private EntityQuery _missingStateQuery;
    private EntityQuery _missingActiveTagQuery;
    private EntityQuery _activeStateQuery;
    private EntityQuery _spectatorObserverQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _missingStateQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<LocalTransform, UnitFactionComponent>()
            .WithNone<UnitAoiStateComponent>()
            .Build(ref state);
        _missingActiveTagQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<LocalTransform, UnitFactionComponent>()
            .WithNone<UnitAoiActiveTag>()
            .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
            .Build(ref state);
        _activeStateQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<UnitAoiStateComponent, UnitAoiActiveTag>()
            .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
            .Build(ref state);
        _spectatorObserverQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<LocalTransform, UnitFactionComponent, BattleSpectatorComponent>()
            .Build(ref state);

        _singletonEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddBuffer<UnitAoiObserverEntry>(_singletonEntity);
        state.EntityManager.AddComponentData(_singletonEntity, new UnitAoiSingleton
        {
            Version = 0u,
            MaxQueryRadius = UnitAoiRanges.MaxQueryRadius,
        });

        state.RequireForUpdate<UnitQuerySingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!_missingStateQuery.IsEmptyIgnoreFilter || !_missingActiveTagQuery.IsEmptyIgnoreFilter)
        {
            state.Dependency.Complete();
            if (!_missingStateQuery.IsEmptyIgnoreFilter)
                state.EntityManager.AddComponent<UnitAoiStateComponent>(_missingStateQuery);
            if (!_missingActiveTagQuery.IsEmptyIgnoreFilter)
                state.EntityManager.AddComponent<UnitAoiActiveTag>(_missingActiveTagQuery);
        }

        RefRW<UnitAoiSingleton> singletonRef =
            SystemAPI.GetComponentRW<UnitAoiSingleton>(_singletonEntity);
        UnitAoiSingleton singleton = singletonRef.ValueRO;
        singleton.Version++;
        if (singleton.Version == 0u)
            singleton.Version = 1u;
        singleton.MaxQueryRadius = math.max(0f, singleton.MaxQueryRadius);
        singletonRef.ValueRW = singleton;

        UnitQuerySingleton querySingleton = SystemAPI.GetSingleton<UnitQuerySingleton>();
        DynamicBuffer<UnitQueryNode> treeNodes =
            state.EntityManager.GetBuffer<UnitQueryNode>(querySingleton.TreeEntity, true);
        DynamicBuffer<UnitQueryEntry> treeEntries =
            state.EntityManager.GetBuffer<UnitQueryEntry>(querySingleton.TreeEntity, true);

        DynamicBuffer<UnitAoiObserverEntry> spectatorObservers =
            state.EntityManager.GetBuffer<UnitAoiObserverEntry>(_singletonEntity);
        spectatorObservers.ResizeUninitialized(_spectatorObserverQuery.CalculateEntityCount());
        JobHandle gatherSpectatorsHandle = new UnitAoiSpectatorObserverGatherJob
        {
            Observers = spectatorObservers.AsNativeArray(),
        }.ScheduleParallel(_spectatorObserverQuery, state.Dependency);

        JobHandle buildHandle = new UnitAoiBuildJob
        {
            Version = singleton.Version,
            QueryRadius = singleton.MaxQueryRadius,
            Entries = treeEntries.AsNativeArray(),
            AdditionalObservers = spectatorObservers.AsNativeArray(),
            Tree = new UnitQueryTree(treeNodes.AsNativeArray(), treeEntries.AsNativeArray()),
            AoiStates = SystemAPI.GetComponentLookup<UnitAoiStateComponent>(),
        }.Schedule(gatherSpectatorsHandle);

        state.Dependency = new UnitAoiActiveStateJob
        {
            Version = singleton.Version,
            EnterDistanceSq = UnitAoiRanges.AnimationEnterRadius * UnitAoiRanges.AnimationEnterRadius,
            ExitDistanceSq = UnitAoiRanges.AnimationExitRadius * UnitAoiRanges.AnimationExitRadius,
        }.ScheduleParallel(_activeStateQuery, buildHandle);

        // The tree is exposed through DynamicBuffer NativeArray aliases. The next frame's
        // UnitQueryBuildSystem resizes those buffers through EntityManager, which cannot
        // discover this manually scheduled reader. Finish the small per-player AOI query
        // here so the tree can be rebuilt safely on the following frame.
        state.Dependency.Complete();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        state.Dependency.Complete();
        if (state.EntityManager.Exists(_singletonEntity))
            state.EntityManager.DestroyEntity(_singletonEntity);
    }
}

[BurstCompile]
[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
public partial struct UnitAoiActiveStateJob : IJobEntity
{
    public uint Version;
    public float EnterDistanceSq;
    public float ExitDistanceSq;

    private void Execute(
        in UnitAoiStateComponent state,
        EnabledRefRW<UnitAoiActiveTag> active)
    {
        float thresholdSq = active.ValueRO ? ExitDistanceSq : EnterDistanceSq;
        active.ValueRW = state.Version == Version &&
                         state.NearestPlayerDistanceSq <= thresholdSq;
    }
}

[BurstCompile]
public partial struct UnitAoiSpectatorObserverGatherJob : IJobEntity
{
    [NativeDisableParallelForRestriction]
    public NativeArray<UnitAoiObserverEntry> Observers;

    private void Execute(
        [EntityIndexInQuery] int index,
        Entity entity,
        in LocalTransform transform,
        in UnitFactionComponent faction)
    {
        Observers[index] = UnitFactionUtility.IsPlayer(faction.Value)
            ? new UnitAoiObserverEntry
            {
                Entity = entity,
                Position = transform.Position,
            }
            : default;
    }
}

[BurstCompile]
public struct UnitAoiBuildJob : IJob
{
    public uint Version;
    public float QueryRadius;

    [ReadOnly]
    public NativeArray<UnitQueryEntry> Entries;

    [ReadOnly]
    public NativeArray<UnitAoiObserverEntry> AdditionalObservers;

    [ReadOnly]
    public UnitQueryTree Tree;

    [NativeDisableParallelForRestriction]
    public ComponentLookup<UnitAoiStateComponent> AoiStates;

    public void Execute()
    {
        if (QueryRadius <= 0f || !Tree.IsCreated)
            return;

        for (int index = 0; index < Entries.Length; index++)
        {
            UnitQueryEntry observer = Entries[index];
            if (!UnitFactionUtility.IsPlayer(observer.Faction))
                continue;

            QueryObserver(observer.Entity, observer.Position);
        }

        for (int index = 0; index < AdditionalObservers.Length; index++)
        {
            UnitAoiObserverEntry observer = AdditionalObservers[index];
            if (observer.Entity == Entity.Null)
                continue;

            QueryObserver(observer.Entity, observer.Position);
        }
    }

    private void QueryObserver(Entity observerEntity, float3 observerPosition)
    {
        if (AoiStates.TryGetComponent(observerEntity, out UnitAoiStateComponent observerState))
        {
            observerState.Version = Version;
            observerState.NearestPlayerDistanceSq = 0f;
            AoiStates[observerEntity] = observerState;
        }

        UnitAoiNearestVisitor visitor = new()
        {
            Version = Version,
            Origin = observerPosition.xy,
            AoiStates = AoiStates,
        };
        UnitQueryShape shape = UnitQueryShape.Circle(observerPosition, QueryRadius);
        Tree.Query(in shape, UnitFactionMask.All, ref visitor, includeDead: true);
    }
}

[BurstCompile]
public struct UnitAoiNearestVisitor : IUnitQueryVisitor
{
    public uint Version;
    public float2 Origin;

    [NativeDisableParallelForRestriction]
    public ComponentLookup<UnitAoiStateComponent> AoiStates;

    public bool Visit(in UnitQueryEntry entry)
    {
        if (!AoiStates.TryGetComponent(entry.Entity, out UnitAoiStateComponent state))
            return true;

        float distanceSq = math.lengthsq(entry.Position.xy - Origin);
        if (state.Version != Version)
        {
            state.Version = Version;
            state.NearestPlayerDistanceSq = distanceSq;
        }
        else if (distanceSq < state.NearestPlayerDistanceSq)
        {
            state.NearestPlayerDistanceSq = distanceSq;
        }

        AoiStates[entry.Entity] = state;
        return true;
    }
}
