using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(UnitInitializationSystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(UnitPerceptionSystem))]
[UpdateBefore(typeof(SkillProjectileSystem))]
partial class UnitQueryBuildSystem : SystemBase
{
    private Entity _singletonEntity;
    private EntityQuery _unitQuery;
    private EntityQuery _interactableQuery;
    private EntityQuery _changedInteractableQuery;
    private UnitQueryRuntimeComponent _runtime;
    private int _interactableCount = -1;

    protected override void OnCreate()
    {
        _unitQuery = GetEntityQuery(new EntityQueryDesc
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

        _interactableQuery = GetEntityQuery(
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<UnitInteractableComponent>());

        _changedInteractableQuery = GetEntityQuery(
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<UnitInteractableComponent>());
        _changedInteractableQuery.AddChangedVersionFilter(ComponentType.ReadOnly<LocalTransform>());

        _singletonEntity = EntityManager.CreateEntity(typeof(UnitQuerySingleton));
        _runtime = new UnitQueryRuntimeComponent();
        EntityManager.AddComponentObject(_singletonEntity, _runtime);
    }

    protected override void OnUpdate()
    {
        Dependency.Complete();

        int unitCount = _unitQuery.CalculateEntityCount();
        _runtime.UnitGrid.PrepareForBuild(unitCount);

        JobHandle unitBuildHandle = new UnitQueryBuildJob
        {
            Entries = _runtime.UnitGrid.AsParallelWriter(),
            InverseCellSize = _runtime.UnitGrid.InverseCellSize,
        }.ScheduleParallel(_unitQuery, Dependency);

        int interactableCount = _interactableQuery.CalculateEntityCount();
        bool rebuildInteractables = interactableCount != _interactableCount || !_changedInteractableQuery.IsEmpty;
        JobHandle interactableBuildHandle = Dependency;
        if (rebuildInteractables)
        {
            _runtime.InteractableGrid.PrepareForBuild(interactableCount);
            interactableBuildHandle = new UnitQueryBuildJob
            {
                Entries = _runtime.InteractableGrid.AsParallelWriter(),
                InverseCellSize = _runtime.InteractableGrid.InverseCellSize,
            }.ScheduleParallel(_interactableQuery, Dependency);
            _interactableCount = interactableCount;
        }

        Dependency = JobHandle.CombineDependencies(unitBuildHandle, interactableBuildHandle);
        Dependency.Complete();
    }

    protected override void OnDestroy()
    {
        Dependency.Complete();
        _runtime?.Dispose();
        _runtime = null;
        if (EntityManager.Exists(_singletonEntity))
            EntityManager.DestroyEntity(_singletonEntity);
    }
}

[BurstCompile]
public partial struct UnitQueryBuildJob : IJobEntity
{
    public NativeParallelMultiHashMap<long, UnitQueryHit>.ParallelWriter Entries;
    public float InverseCellSize;

    private void Execute(Entity entity, in LocalTransform transform)
    {
        int2 cell = (int2)math.floor(transform.Position.xy * InverseCellSize);
        long cellKey = ((long)cell.x << 32) | (uint)cell.y;
        Entries.Add(cellKey, new UnitQueryHit
        {
            Entity = entity,
            Position = transform.Position,
        });
    }
}
