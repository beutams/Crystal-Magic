using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(UnitInitializationSystemGroup))]
[UpdateAfter(typeof(UnitSourceDispatcherSystem))]
[UpdateBefore(typeof(StateScriptInitSystem))]
public partial struct DungeonInterestPointRuntimeSystem : ISystem
{
    private EntityQuery _factionQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DungeonInterestPointComponent>();
        _factionQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<UnitFactionComponent, LocalTransform>()
            .Build(ref state);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        NativeArray<UnitFactionComponent> factions =
            _factionQuery.ToComponentDataArray<UnitFactionComponent>(Allocator.TempJob);
        NativeArray<LocalTransform> transforms =
            _factionQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        DungeonInterestPointRuntimeJob job = new()
        {
            Factions = factions,
            FactionTransforms = transforms,
            ConsumerLookup = state.GetBufferLookup<UnitVariableConsumerElement>(true),
            UnitVariableLookup = state.GetComponentLookup<UnitVariableComponent>(true),
            TransformLookup = state.GetComponentLookup<LocalTransform>(true),
            DestroyLookup = state.GetComponentLookup<DestroyEntityFlag>(true),
        };
        state.Dependency = job.ScheduleParallel(state.Dependency);
        state.Dependency.Complete();
    }
}

[BurstCompile]
public partial struct DungeonInterestPointRuntimeJob : IJobEntity
{
    [ReadOnly, DeallocateOnJobCompletion]
    public NativeArray<UnitFactionComponent> Factions;

    [ReadOnly, DeallocateOnJobCompletion]
    public NativeArray<LocalTransform> FactionTransforms;

    [ReadOnly]
    public BufferLookup<UnitVariableConsumerElement> ConsumerLookup;

    [ReadOnly]
    public ComponentLookup<UnitVariableComponent> UnitVariableLookup;

    [ReadOnly]
    public ComponentLookup<LocalTransform> TransformLookup;

    [ReadOnly]
    public ComponentLookup<DestroyEntityFlag> DestroyLookup;

    private void Execute(
        Entity entity,
        ref DungeonInterestPointComponent point,
        in LocalTransform pointTransform)
    {
        float2 pointPosition = pointTransform.Position.xy;
        float nearestDistanceSq = float.MaxValue;
        for (int index = 0; index < Factions.Length; index++)
        {
            if (Factions[index].Value != UnitFactionType.Player)
                continue;

            float2 playerPosition = FactionTransforms[index].Position.xy;
            nearestDistanceSq = math.min(
                nearestDistanceSq,
                math.lengthsq(playerPosition - pointPosition));
        }

        point.NearestPlayerDistance = nearestDistanceSq == float.MaxValue
            ? float.MaxValue
            : math.sqrt(nearestDistanceSq);
        DungeonInterestPointSource.RefreshMemberState(
            entity,
            ref point,
            in ConsumerLookup,
            in UnitVariableLookup,
            in TransformLookup,
            in DestroyLookup);
    }
}
