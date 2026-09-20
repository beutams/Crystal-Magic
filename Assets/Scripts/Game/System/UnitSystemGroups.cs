using CrystalMagic.Core;
using Server;
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
    protected override void OnUpdate()
    {
        if (SystemAPI.TryGetSingleton(out WorldStateComponent worldState) && worldState.IsSimulationLocked)
            return;

        // Battle World 在地图初始化时已经加入 PlayerLoop；这里只阻断开战前的权威战斗逻辑，
        // 不改变 ECS 正常的逐帧更新频率。
        if (FrameManagerUtility.TryGet(EntityManager, out FrameManager frame) && !frame.running)
            return;

        base.OnUpdate();
    }
}

[WorldSystemFilter(
    WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation,
    WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitDecisionSystemGroup))]
[UpdateBefore(typeof(UnitPostProcessSystemGroup))]
public partial class UnitExecutionSystemGroup : ComponentSystemGroup
{
    protected override void OnUpdate()
    {
        if (FrameManagerUtility.TryGet(EntityManager, out FrameManager frame) && !frame.running)
            return;

        base.OnUpdate();
    }
}

[WorldSystemFilter(
    WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation,
    WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(UnitExecutionSystemGroup))]
public partial class UnitPostProcessSystemGroup : ComponentSystemGroup
{
    protected override void OnUpdate()
    {
        if (FrameManagerUtility.TryGet(EntityManager, out FrameManager frame) && !frame.running)
            return;

        base.OnUpdate();
    }
}

[WorldSystemFilter(
    WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation,
    WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
[UpdateAfter(typeof(FrameReceiveSystem))]
public partial class ClientInputSystemGroup : ComponentSystemGroup
{
}

[WorldSystemFilter(
    WorldSystemFilterFlags.ClientSimulation,
    WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ClientInputSystemGroup))]
[UpdateBefore(typeof(ClientNetworkPresentationSystemGroup))]
public partial class ClientPlayerPredictionSystemGroup : ComponentSystemGroup
{
}

[WorldSystemFilter(
    WorldSystemFilterFlags.ClientSimulation,
    WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(FrameReceiveSystem))]
[UpdateAfter(typeof(ClientPlayerPredictionSystemGroup))]
[UpdateBefore(typeof(ClientPresentationSystemGroup))]
public partial class ClientNetworkPresentationSystemGroup : ComponentSystemGroup
{
}

[WorldSystemFilter(
    WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Presentation,
    WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Presentation)]
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
public partial class ClientPresentationSystemGroup : ComponentSystemGroup
{
}
