using CrystalMagic.Core;
using Server;
using Unity.Entities;

[UpdateInGroup(typeof(UnitInitializationSystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(UnitSourceDispatcherSystem))]
[UpdateBefore(typeof(BehaviorTreeInitSystem))]
[UpdateBefore(typeof(StateScriptInitSystem))]
public partial class WorldStateSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<WorldStateComponent>();
        RequireForUpdate<GameWorldContextComponent>();
    }

    protected override void OnUpdate()
    {
        RefRW<WorldStateComponent> worldState = SystemAPI.GetSingletonRW<WorldStateComponent>();
        uint currentFrame = FrameManagerUtility.TryGet(EntityManager, out FrameManager frameManager)
            ? frameManager.currentFrame
            : 0u;
        GameGateMask gateMask = GameGateMask.None;
        GameWorldContextComponent context = SystemAPI.GetSingleton<GameWorldContextComponent>();
        if (context.Role == GameWorldRole.Standalone || context.Role == GameWorldRole.Client)
        {
            GameGateComponent gameGate = GameGateComponent.Instance;
            if (gameGate.IsSimulationLocked)
                gateMask |= GameGateMask.Simulation;
            if (gameGate.IsPlayerInputLocked)
                gateMask |= GameGateMask.PlayerInput;
            if (gameGate.IsUIInputLocked)
                gateMask |= GameGateMask.UIInput;
        }

        if (worldState.ValueRO.CurrentFrame == currentFrame && worldState.ValueRO.GateMask == gateMask)
            return;

        worldState.ValueRW.CurrentFrame = currentFrame;
        worldState.ValueRW.GateMask = gateMask;
    }
}
