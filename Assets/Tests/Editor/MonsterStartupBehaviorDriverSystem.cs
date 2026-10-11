using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Transforms;

// Runs the production behavior jobs without requiring a scene's ConfigComponent.
[DisableAutoCreation]
public partial class MonsterStartupBehaviorDriverSystem : SystemBase
{
    public BlobAssetReference<BehaviorTreeRuntimeRegistryBlob> Registry;
    private UnitSourceDispatcher readSources;
    private UnitSourceDispatcher writeSources;

    protected override void OnCreate()
    {
        readSources.InitializeReadOnly(this);
        writeSources.Initialize(this);
    }

    protected override void OnUpdate()
    {
        readSources.Update(this);
        writeSources.Update(this);
        new BehaviorTreeEvaluationJob
        {
            Registry = Registry,
            Sources = readSources,
            DeltaTime = SystemAPI.Time.DeltaTime,
            MaxImmediateIterations = 256,
            Transforms = GetComponentLookup<LocalTransform>(true),
            PostTransforms = GetComponentLookup<PostTransformMatrix>(true),
            Facings = GetComponentLookup<UnitFacingComponent>(true),
            Navigations = GetComponentLookup<UnitNavigationComponent>(true),
            Moves = GetComponentLookup<UnitMoveComponent>(true),
            Variables = GetComponentLookup<UnitVariableComponent>(true),
        }.Run();
        new BehaviorTreeSourceCommandJob { Sources = writeSources }.Run();
        new BehaviorTreeMoveCommandJob().Run();
    }
}
