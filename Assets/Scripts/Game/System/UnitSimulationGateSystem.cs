using Server;
using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitInitializationSystemGroup))]
[UpdateBefore(typeof(UnitDecisionSystemGroup))]
public partial class UnitSimulationGateSystem : SystemBase
{
    private UnitDecisionSystemGroup _decisionGroup;
    private UnitExecutionSystemGroup _executionGroup;
    private UnitPostProcessSystemGroup _postProcessGroup;

    protected override void OnCreate()
    {
        _decisionGroup = World.GetOrCreateSystemManaged<UnitDecisionSystemGroup>();
        _executionGroup = World.GetOrCreateSystemManaged<UnitExecutionSystemGroup>();
        _postProcessGroup = World.GetOrCreateSystemManaged<UnitPostProcessSystemGroup>();
    }

    protected override void OnUpdate()
    {
        bool canSimulate = true;
        if (SystemAPI.TryGetSingleton(out GameGateStateComponent gameGate) && gameGate.IsSimulationLocked)
            canSimulate = false;
        if (FrameManagerUtility.TryGet(EntityManager, out FrameManager frame) && !frame.running)
            canSimulate = false;

        _decisionGroup.Enabled = canSimulate;
        _executionGroup.Enabled = canSimulate;
        _postProcessGroup.Enabled = canSimulate;
    }
}
