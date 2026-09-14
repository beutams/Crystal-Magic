using Unity.Entities;

[RunInGameWorld(GameWorldKind.Town | GameWorldKind.Dungeon)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(UnitDecisionSystemGroup))]
public partial class UnitInitializationSystemGroup : ComponentSystemGroup
{
}

[RunInGameWorld(GameWorldKind.Town | GameWorldKind.Dungeon)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitInitializationSystemGroup))]
[UpdateBefore(typeof(UnitExecutionSystemGroup))]
public partial class UnitDecisionSystemGroup : ComponentSystemGroup
{
}

[RunInGameWorld(GameWorldKind.Town | GameWorldKind.Dungeon)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitDecisionSystemGroup))]
[UpdateBefore(typeof(UnitPostProcessSystemGroup))]
public partial class UnitExecutionSystemGroup : ComponentSystemGroup
{
}

[RunInGameWorld(GameWorldKind.Town | GameWorldKind.Dungeon)]
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(UnitExecutionSystemGroup))]
public partial class UnitPostProcessSystemGroup : ComponentSystemGroup
{
}
