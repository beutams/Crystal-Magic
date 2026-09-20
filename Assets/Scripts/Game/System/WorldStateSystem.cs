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
        GameGateMask gateMask = GameGateMask.None;
        if (GameWorldContextUtility.TryGet(EntityManager, out GameWorldContextComponent context) &&
            (context.Role == GameWorldRole.Standalone || context.Role == GameWorldRole.Client))
        {
            GameGateComponent gameGate = GameGateComponent.Instance;
            if (gameGate.IsSimulationLocked)
                gateMask |= GameGateMask.Simulation;
            if (gameGate.IsPlayerInputLocked)
                gateMask |= GameGateMask.PlayerInput;
            if (gameGate.IsUIInputLocked)
                gateMask |= GameGateMask.UIInput;
        }

        if (worldState.CurrentFrame == currentFrame && worldState.GateMask == gateMask)
            return;

        worldState.CurrentFrame = currentFrame;
        worldState.GateMask = gateMask;
        EntityManager.SetComponentData(_worldEntity, worldState);
    }
}
