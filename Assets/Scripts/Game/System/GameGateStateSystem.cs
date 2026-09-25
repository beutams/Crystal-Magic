using CrystalMagic.Core;
using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(UnitInitializationSystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(UnitSourceDispatcherSystem))]
[UpdateBefore(typeof(BehaviorTreeInitSystem))]
[UpdateBefore(typeof(StateScriptInitSystem))]
public partial class GameGateStateSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<GameGateStateComponent>();
    }

    protected override void OnUpdate()
    {
        GameGateMask mask = GameGateMask.None;
        if (GameGateComponent.Instance.IsSimulationLocked)
            mask |= GameGateMask.Simulation;
        if (GameGateComponent.Instance.IsPlayerInputLocked)
            mask |= GameGateMask.PlayerInput;
        if (GameGateComponent.Instance.IsUIInputLocked)
            mask |= GameGateMask.UIInput;

        RefRW<GameGateStateComponent> state = SystemAPI.GetSingletonRW<GameGateStateComponent>();
        if (state.ValueRO.Mask != mask)
            state.ValueRW.Mask = mask;
    }
}
