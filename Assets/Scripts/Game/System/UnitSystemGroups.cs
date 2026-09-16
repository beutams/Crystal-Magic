using Unity.Entities;

[WorldSystemFilter(
    WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation,
    WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(UnitDecisionSystemGroup))]
public partial class UnitInitializationSystemGroup : ComponentSystemGroup
{
}

[WorldSystemFilter(
    WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation,
    WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitInitializationSystemGroup))]
[UpdateBefore(typeof(UnitExecutionSystemGroup))]
public partial class UnitDecisionSystemGroup : ComponentSystemGroup
{
}

[WorldSystemFilter(
    WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation,
    WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitDecisionSystemGroup))]
[UpdateBefore(typeof(UnitPostProcessSystemGroup))]
public partial class UnitExecutionSystemGroup : ComponentSystemGroup
{
}

[WorldSystemFilter(
    WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation,
    WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(UnitExecutionSystemGroup))]
public partial class UnitPostProcessSystemGroup : ComponentSystemGroup
{
}

[WorldSystemFilter(
    WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation,
    WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
public partial class ClientInputSystemGroup : ComponentSystemGroup
{
}

[WorldSystemFilter(
    WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Presentation,
    WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Presentation)]
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
public partial class ClientPresentationSystemGroup : ComponentSystemGroup
{
}
