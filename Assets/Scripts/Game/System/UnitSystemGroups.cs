using Unity.Entities;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(UnitDecisionSystemGroup))]
public partial class UnitInitializationSystemGroup : ComponentSystemGroup
{
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitInitializationSystemGroup))]
[UpdateBefore(typeof(UnitExecutionSystemGroup))]
public partial class UnitDecisionSystemGroup : ComponentSystemGroup
{
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitDecisionSystemGroup))]
[UpdateBefore(typeof(UnitPostProcessSystemGroup))]
public partial class UnitExecutionSystemGroup : ComponentSystemGroup
{
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitExecutionSystemGroup))]
public partial class UnitPostProcessSystemGroup : ComponentSystemGroup
{
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FrameReceiveSystem))]
public partial class ClientInputSystemGroup : ComponentSystemGroup
{
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ClientInputSystemGroup))]
[UpdateBefore(typeof(ClientNetworkPresentationSystemGroup))]
public partial class ClientPlayerPredictionSystemGroup : ComponentSystemGroup
{
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BattleSimulationSystemGroup))]
[UpdateBefore(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(GamePresentationSystemGroup))]
public partial class ClientNetworkPresentationSystemGroup : ComponentSystemGroup
{
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class GamePresentationSystemGroup : ComponentSystemGroup
{
}
