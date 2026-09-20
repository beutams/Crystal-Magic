using CrystalMagic.Core;
using Server;
using Unity.Entities;

[UpdateInGroup(typeof(UnitInitializationSystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(UnitSourceDispatcherSystem))]
[UpdateBefore(typeof(BehaviorTreeInitSystem))]
[UpdateBefore(typeof(StateScriptInitSystem))]
public partial class WorldStateSystem : SystemBase
{
    private Entity _worldEntity;

    protected override void OnCreate()
    {
        if (!WorldStateUtility.TryGetEntity(EntityManager, out _worldEntity))
            _worldEntity = EntityManager.CreateEntity(typeof(WorldStateComponent));

        if (!EntityManager.HasComponent<WorldVariableComponent>(_worldEntity))
            EntityManager.AddComponentData(_worldEntity, new WorldVariableComponent());
        if (!EntityManager.HasBuffer<WorldVariableElement>(_worldEntity))
            EntityManager.AddBuffer<WorldVariableElement>(_worldEntity);
        if (!EntityManager.HasComponent<InteractionCandidateComponent>(_worldEntity))
        {
            EntityManager.AddComponentData(_worldEntity, new InteractionCandidateComponent
            {
                Target = Entity.Null,
            });
        }
    }

    protected override void OnUpdate()
    {
        WorldStateComponent worldState = EntityManager.GetComponentData<WorldStateComponent>(_worldEntity);
        uint currentFrame = FrameManagerUtility.TryGet(EntityManager, out FrameManager frameManager)
            ? frameManager.currentFrame
            : 0u;
        if (worldState.CurrentFrame == currentFrame)
            return;

        worldState.CurrentFrame = currentFrame;
        EntityManager.SetComponentData(_worldEntity, worldState);
    }
}
