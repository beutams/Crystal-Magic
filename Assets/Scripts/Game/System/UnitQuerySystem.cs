using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(UnitInitializationSystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(UnitPerceptionSystem))]
[UpdateBefore(typeof(SkillProjectileSystem))]
partial struct UnitQueryBuildSystem : ISystem
{
    private Entity _singletonEntity;
    private Entity _unitGridEntity;
    private Entity _interactableGridEntity;
    private EntityQuery _unitQuery;
    private EntityQuery _interactableQuery;
    private EntityQuery _changedInteractableQuery;
    private int _interactableCount;

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
                ComponentType.ReadOnly<UnitDeathComponent>(),
            },
        });

        _interactableQuery = state.GetEntityQuery(
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<UnitInteractableComponent>());
        _changedInteractableQuery = state.GetEntityQuery(
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<UnitInteractableComponent>());
        _changedInteractableQuery.AddChangedVersionFilter(ComponentType.ReadOnly<LocalTransform>());

        _unitGridEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddBuffer<UnitQueryEntry>(_unitGridEntity);
        _interactableGridEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddBuffer<UnitQueryEntry>(_interactableGridEntity);

        _singletonEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(_singletonEntity, new UnitQuerySingleton
        {
            UnitGridEntity = _unitGridEntity,
            InteractableGridEntity = _interactableGridEntity,
            InverseCellSize = 1f / UnitQueryGrid.DefaultCellSize,
        });
        _interactableCount = -1;
    }

    public void OnUpdate(ref SystemState state)
    {
        // Dynamic-buffer capacity can change while preparing a new frame. Finish the
        // previous readers/writers before exposing a fresh array to the build jobs.
        state.Dependency.Complete();

        UnitQuerySingleton singleton =
            state.EntityManager.GetComponentData<UnitQuerySingleton>(_singletonEntity);
        DynamicBuffer<UnitQueryEntry> unitBuffer =
            state.EntityManager.GetBuffer<UnitQueryEntry>(_unitGridEntity);
        unitBuffer.ResizeUninitialized(_unitQuery.CalculateEntityCount());

        JobHandle unitBuildHandle = new UnitQueryBuildJob
        {
            Entries = unitBuffer.AsNativeArray(),
            InverseCellSize = singleton.InverseCellSize,
        }.ScheduleParallel(_unitQuery, default);
        JobHandle unitSortHandle = new UnitQuerySortJob
        {
            Entries = unitBuffer.AsNativeArray(),
        }.Schedule(unitBuildHandle);

        int interactableCount = _interactableQuery.CalculateEntityCount();
        bool rebuildInteractables =
            interactableCount != _interactableCount || !_changedInteractableQuery.IsEmpty;
        JobHandle interactableSortHandle = default;
        if (rebuildInteractables)
        {
            DynamicBuffer<UnitQueryEntry> interactableBuffer =
                state.EntityManager.GetBuffer<UnitQueryEntry>(_interactableGridEntity);
            interactableBuffer.ResizeUninitialized(interactableCount);
            JobHandle interactableBuildHandle = new UnitQueryBuildJob
            {
                Entries = interactableBuffer.AsNativeArray(),
                InverseCellSize = singleton.InverseCellSize,
            }.ScheduleParallel(_interactableQuery, default);
            interactableSortHandle = new UnitQuerySortJob
            {
                Entries = interactableBuffer.AsNativeArray(),
            }.Schedule(interactableBuildHandle);
            _interactableCount = interactableCount;
        }

        state.Dependency = rebuildInteractables
            ? JobHandle.CombineDependencies(unitSortHandle, interactableSortHandle)
            : unitSortHandle;

        // Managed shape effects can query later in the same frame. Completing here keeps
        // their view stable while all collection and sorting work itself remains Burst.
        state.Dependency.Complete();
    }

    public void OnDestroy(ref SystemState state)
    {
        state.Dependency.Complete();
        if (state.EntityManager.Exists(_singletonEntity))
            state.EntityManager.DestroyEntity(_singletonEntity);
        if (state.EntityManager.Exists(_unitGridEntity))
            state.EntityManager.DestroyEntity(_unitGridEntity);
        if (state.EntityManager.Exists(_interactableGridEntity))
            state.EntityManager.DestroyEntity(_interactableGridEntity);
    }
}

[BurstCompile]
public partial struct UnitQueryBuildJob : IJobEntity
{
    [NativeDisableParallelForRestriction]
    public NativeArray<UnitQueryEntry> Entries;

    public float InverseCellSize;

    private void Execute([EntityIndexInQuery] int index, Entity entity, in LocalTransform transform)
    {
        int2 cell = (int2)math.floor(transform.Position.xy * InverseCellSize);
        Entries[index] = new UnitQueryEntry
        {
            CellKey = UnitQueryGrid.GetCellKey(cell),
            Entity = entity,
            Position = transform.Position,
        };
    }
}

[BurstCompile]
public struct UnitQuerySortJob : IJob
{
    public NativeArray<UnitQueryEntry> Entries;

    public void Execute()
    {
        Entries.Sort(new UnitQueryEntryComparer());
    }
}

public struct UnitQueryEntryComparer : IComparer<UnitQueryEntry>
{
    public int Compare(UnitQueryEntry left, UnitQueryEntry right)
    {
        if (left.CellKey != right.CellKey)
            return left.CellKey < right.CellKey ? -1 : 1;
        if (left.Entity.Index != right.Entity.Index)
            return left.Entity.Index < right.Entity.Index ? -1 : 1;
        if (left.Entity.Version == right.Entity.Version)
            return 0;
        return left.Entity.Version < right.Entity.Version ? -1 : 1;
    }
}
