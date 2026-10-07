using CrystalMagic.Core;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(ClientNetworkPresentationSystemGroup))]
[UpdateAfter(typeof(ClientSkillVisualExecutionSystem))]
[UpdateBefore(typeof(ClientPresentationEventSystem))]
public partial struct ClientPredictedProjectileSystem : ISystem
{
    private UnitSourceDispatcher _sources;
    private EntityQuery _terrainQuery;
    public void OnCreate(ref SystemState state)
    {
        _sources.InitializeReadOnly(ref state);
        _terrainQuery = state.GetEntityQuery(ComponentType.ReadOnly<DungeonNavigationMapComponent>(),
            ComponentType.ReadOnly<DungeonNavigationCollisionWord>());
        state.RequireForUpdate<ClientPredictedProjectileComponent>();
        state.RequireForUpdate<UnitQuerySingleton>();
    }

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

        _sources.Update(ref state);
        ComponentLookup<ClientPredictedProjectileComponent> predictions = SystemAPI.GetComponentLookup<ClientPredictedProjectileComponent>(true);
        JobHandle movementHandle = new SkillProjectileMovementJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            Predictions = predictions,
        }.ScheduleParallel(state.Dependency);
        Entity terrainEntity = _terrainQuery.IsEmptyIgnoreFilter ? Entity.Null : _terrainQuery.GetSingletonEntity();
        state.Dependency = new SkillProjectileCollisionJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            Tree = new UnitQueryTree(treeNodes.AsNativeArray(), treeEntries.AsNativeArray()),
            Variables = SystemAPI.GetComponentLookup<UnitVariableComponent>(true),
            Sources = _sources,
            Predictions = predictions,
            TerrainEntity = terrainEntity,
            TerrainMap = terrainEntity == Entity.Null ? default : SystemAPI.GetComponent<DungeonNavigationMapComponent>(terrainEntity),
            TerrainWords = SystemAPI.GetBufferLookup<DungeonNavigationCollisionWord>(true),
        }.ScheduleParallel(movementHandle);
    }
}
