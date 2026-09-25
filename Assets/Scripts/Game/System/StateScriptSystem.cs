using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateAfter(typeof(BehaviorTreeSystem))]
public partial class StateScriptSystem : SystemBase
{
    private UnitSourceDispatcher _sources;

    protected override void OnCreate()
    {
        _sources.Initialize(this);
        RequireForUpdate<StateScriptRuntimeRegistryComponent>();
    }

    protected override void OnUpdate()
    {
        BlobAssetReference<StateScriptRuntimeRegistryBlob> registry =
            SystemAPI.GetSingleton<StateScriptRuntimeRegistryComponent>().Value;
        if (!registry.IsCreated)
            return;

        _sources.Update(this);
        // SetValue is part of the immediate pulse chain: later nodes in the same
        // chain must observe its write. Source targets may be Self, Other, or a
        // global entity, so evaluate serially until writes can be safely partitioned.
        Dependency = new StateScriptEvaluationJob
        {
            Registry = registry,
            Sources = _sources,
            DeltaTime = math.max(0f, SystemAPI.Time.DeltaTime),
        }.Schedule(Dependency);
        Dependency = new StateScriptDeathJob().ScheduleParallel(Dependency);
    }
}

[BurstCompile]
[WithNone(typeof(UnitDeathComponent))]
public partial struct StateScriptEvaluationJob : IJobEntity
{
    private const int MaxPulseDepth = 128;
    private const byte FlagPrimary = 1 << 0;
    private const byte FlagSecondary = 1 << 1;

    public BlobAssetReference<StateScriptRuntimeRegistryBlob> Registry;

    public UnitSourceDispatcher Sources;

    public float DeltaTime;

    private void Execute(
        Entity entity,
        ref UnitStateScriptComponent component,
        ref DynamicBuffer<StateScriptGraphStateElement> graphStates,
        ref DynamicBuffer<StateScriptNodeStateElement> nodeStates,
        ref DynamicBuffer<StateScriptSourceCommandElement> sourceCommands,
        ref DynamicBuffer<StateScriptSourceCommandArgumentElement> sourceArguments,
        ref DynamicBuffer<StateScriptManagedCommandElement> managedCommands,
        ref DynamicBuffer<StateScriptExternalResultElement> externalResults)
    {
        sourceCommands.Clear();
        sourceArguments.Clear();
        managedCommands.Clear();
        if (component.IsInitialized == 0 ||
            component.IsStoppedForDeath != 0 ||
            component.InitializationError != StateScriptInitializationError.None ||
            component.DefinitionIndex < 0 ||
            component.DefinitionIndex >= Registry.Value.Units.Length)
        {
            externalResults.Clear();
            return;
        }

        ref StateScriptUnitDefinitionBlob unit = ref Registry.Value.Units[component.DefinitionIndex];
        if (graphStates.Length != unit.Graphs.Length)
        {
            component.InitializationError = StateScriptInitializationError.InvalidDefinition;
            externalResults.Clear();
            return;
        }

        component.TickVersion++;
        if (component.TickVersion == 0)
            component.TickVersion = 1;
        UnitSourceArguments noArguments = default;
        Entity other = Entity.Null;
        if (Sources.TryGet(
                entity,
                UnitSourceId.UnitVariablesOther,
                in noArguments,
                out UnitSourceValue otherValue))
        {
            otherValue.TryGetEntity(out other);
        }
        UnitSourceContext context = new(entity, other);

        for (int graphIndex = 0; graphIndex < unit.Graphs.Length; graphIndex++)
        {
            ref StateScriptGraphDefinitionBlob graph = ref unit.Graphs[graphIndex];
            StateScriptGraphStateElement graphState = graphStates[graphIndex];
            if (graphState.NodeStateStart < 0 || graphState.NodeStateStart + graph.Nodes.Length > nodeStates.Length)
            {
                component.InitializationError = StateScriptInitializationError.InvalidDefinition;
                externalResults.Clear();
                return;
            }

            bool enabled = graph.ExecutionConditionExpressionIndex < 0 ||
                TryEvaluateCondition(ref graph, graph.ExecutionConditionExpressionIndex, in context);
            if (!enabled)
            {
                if (graphState.IsActive != 0)
                {
                    StopGraph(
                        graphIndex,
                        ref graph,
                        graphState.NodeStateStart,
                        ref nodeStates,
                        ref managedCommands);
                    graphState.IsActive = 0;
                    graphStates[graphIndex] = graphState;
                }
                continue;
            }

            FixedList4096Bytes<StateScriptPulse> pulses = default;
            if (graphState.IsActive == 0)
            {
                graphState.IsActive = 1;
                graphStates[graphIndex] = graphState;
                if (!Emit(ref graph, graph.EntryNodeIndex, StateScriptPortId.Out, ref pulses) ||
                    !DrainPulses(
                        entity,
                        graphIndex,
                        ref graph,
                        graphState.NodeStateStart,
                        in context,
                        ref component,
                        ref nodeStates,
                        ref sourceCommands,
                        ref sourceArguments,
                        ref managedCommands,
                        ref pulses))
                {
                    component.InitializationError = StateScriptInitializationError.InvalidDefinition;
                    externalResults.Clear();
                    return;
                }
            }

            for (int resultIndex = 0; resultIndex < externalResults.Length; resultIndex++)
            {
                StateScriptExternalResultElement result = externalResults[resultIndex];
                if (result.GraphIndex != graphIndex ||
                    (uint)result.NodeIndex >= (uint)graph.Nodes.Length ||
                    graph.Nodes[result.NodeIndex].Type != StateScriptNodeRuntimeType.Addition)
                {
                    continue;
                }
                int stateIndex = graphState.NodeStateStart + result.NodeIndex;
                StateScriptNodeStateElement state = nodeStates[stateIndex];
                if (state.Status == StateScriptStateStatus.Stop ||
                    state.ExecutionVersion != result.ExecutionVersion)
                {
                    continue;
                }
                state.Status = StateScriptStateStatus.Stop;
                nodeStates[stateIndex] = state;
#if UNITY_EDITOR
                managedCommands.Add(new StateScriptManagedCommandElement
                {
                    Type = StateScriptManagedCommandType.TraceAdditionResultConsumed,
                    GraphIndex = graphIndex,
                    NodeIndex = result.NodeIndex,
                    ExecutionVersion = result.ExecutionVersion,
                });
#endif
                if (!Emit(ref graph, result.NodeIndex, StateScriptPortId.OnComplete, ref pulses) ||
                    !DrainPulses(
                        entity,
                        graphIndex,
                        ref graph,
                        graphState.NodeStateStart,
                        in context,
                        ref component,
                        ref nodeStates,
                        ref sourceCommands,
                        ref sourceArguments,
                        ref managedCommands,
                        ref pulses))
                {
                    component.InitializationError = StateScriptInitializationError.InvalidDefinition;
                    externalResults.Clear();
                    return;
                }
            }

            for (int stateOrderIndex = 0; stateOrderIndex < graph.StateNodeIndices.Length; stateOrderIndex++)
            {
                int nodeIndex = graph.StateNodeIndices[stateOrderIndex];
                int stateIndex = graphState.NodeStateStart + nodeIndex;
                StateScriptNodeStateElement state = nodeStates[stateIndex];
                if (state.Status == StateScriptStateStatus.Pending && state.PendingTick < component.TickVersion)
                {
                    state.Status = StateScriptStateStatus.Running;
                    nodeStates[stateIndex] = state;
                }
            }

            for (int stateOrderIndex = 0; stateOrderIndex < graph.StateNodeIndices.Length; stateOrderIndex++)
            {
                int nodeIndex = graph.StateNodeIndices[stateOrderIndex];
                int stateIndex = graphState.NodeStateStart + nodeIndex;
                if (nodeStates[stateIndex].Status != StateScriptStateStatus.Running)
                    continue;
                if (!UpdateStateNode(
                        entity,
                        graphIndex,
                        nodeIndex,
                        ref graph,
                        graphState.NodeStateStart,
                        in context,
                        ref component,
                        ref nodeStates,
                        ref sourceCommands,
                        ref sourceArguments,
                        ref managedCommands,
                        ref pulses))
                {
                    component.InitializationError = StateScriptInitializationError.InvalidDefinition;
                    externalResults.Clear();
                    return;
                }
                if (!DrainPulses(
                        entity,
                        graphIndex,
                        ref graph,
                        graphState.NodeStateStart,
                        in context,
                        ref component,
                        ref nodeStates,
                        ref sourceCommands,
                        ref sourceArguments,
                        ref managedCommands,
                        ref pulses))
                {
                    component.InitializationError = StateScriptInitializationError.InvalidDefinition;
                    externalResults.Clear();
                    return;
                }
            }
        }
        externalResults.Clear();
    }

    private bool DrainPulses(
        Entity entity,
        int graphIndex,
        ref StateScriptGraphDefinitionBlob graph,
        int stateStart,
        in UnitSourceContext context,
        ref UnitStateScriptComponent component,
        ref DynamicBuffer<StateScriptNodeStateElement> states,
        ref DynamicBuffer<StateScriptSourceCommandElement> sourceCommands,
        ref DynamicBuffer<StateScriptSourceCommandArgumentElement> sourceArguments,
        ref DynamicBuffer<StateScriptManagedCommandElement> managedCommands,
        ref FixedList4096Bytes<StateScriptPulse> pulses)
    {
        int steps = 0;
        int maxSteps = math.max(256, graph.Nodes.Length * 64);
        while (pulses.Length > 0)
        {
            if (++steps > maxSteps)
                return false;
            int pulseIndex = pulses.Length - 1;
            StateScriptPulse pulse = pulses[pulseIndex];
            pulses.Length = pulseIndex;
            if (!ProcessPulse(
                    entity,
                    graphIndex,
                    pulse,
                    ref graph,
                    stateStart,
                    in context,
                    ref component,
                    ref states,
                    ref sourceCommands,
                    ref sourceArguments,
                    ref managedCommands,
                    ref pulses))
            {
                return false;
            }
        }
        return true;
    }

    private bool ProcessPulse(
        Entity entity,
        int graphIndex,
        StateScriptPulse pulse,
        ref StateScriptGraphDefinitionBlob graph,
        int stateStart,
        in UnitSourceContext context,
        ref UnitStateScriptComponent component,
        ref DynamicBuffer<StateScriptNodeStateElement> states,
        ref DynamicBuffer<StateScriptSourceCommandElement> sourceCommands,
        ref DynamicBuffer<StateScriptSourceCommandArgumentElement> sourceArguments,
        ref DynamicBuffer<StateScriptManagedCommandElement> managedCommands,
        ref FixedList4096Bytes<StateScriptPulse> pulses)
    {
        if ((uint)pulse.NodeIndex >= (uint)graph.Nodes.Length)
            return false;
        StateScriptNodeDefinition node = graph.Nodes[pulse.NodeIndex];
        int stateIndex = stateStart + pulse.NodeIndex;
        StateScriptNodeStateElement state = states[stateIndex];
        state.LastPulseTick = component.TickVersion;
        states[stateIndex] = state;

        switch (node.Type)
        {
            case StateScriptNodeRuntimeType.Compare:
                if (pulse.InputPortId != StateScriptPortId.In || node.ExpressionCount != 1)
                    return false;
                bool matches = TryEvaluateCondition(ref graph, node.ExpressionStart, in context);
#if UNITY_EDITOR
                AppendCurrentInputTypeTrace(
                    graphIndex,
                    pulse.NodeIndex,
                    node.ExpressionStart,
                    matches,
                    ref graph,
                    in context,
                    ref managedCommands);
#endif
                return Emit(
                    ref graph,
                    pulse.NodeIndex,
                    matches ? StateScriptPortId.True : StateScriptPortId.False,
                    ref pulses);

            case StateScriptNodeRuntimeType.SetValue:
                if (pulse.InputPortId != StateScriptPortId.In ||
                    !TryApplySourceValue(in node, in context, ref graph))
                    return true;
                return Emit(ref graph, pulse.NodeIndex, StateScriptPortId.Out, ref pulses);

            case StateScriptNodeRuntimeType.RequestSkill:
            case StateScriptNodeRuntimeType.RequestSkillWithAddition:
                if (pulse.InputPortId != StateScriptPortId.In)
                    return false;
                if (!TryBuildSkillCommand(
                        in node,
                        ref graph,
                        in context,
                        out StateScriptManagedCommandElement skillCommand
#if UNITY_EDITOR
                        , out StateScriptSkillRequestBuildError buildError
#endif
                    ))
                {
#if UNITY_EDITOR
                    managedCommands.Add(new StateScriptManagedCommandElement
                    {
                        Type = StateScriptManagedCommandType.TraceSkillRequestFailed,
                        GraphIndex = graphIndex,
                        NodeIndex = pulse.NodeIndex,
                        IntValue = (int)buildError,
                    });
#endif
                    return true;
                }
                skillCommand.Type = node.Type == StateScriptNodeRuntimeType.RequestSkill
                    ? StateScriptManagedCommandType.RequestSkill
                    : StateScriptManagedCommandType.RequestSkillWithAddition;
                skillCommand.GraphIndex = graphIndex;
                skillCommand.NodeIndex = pulse.NodeIndex;
                managedCommands.Add(skillCommand);
#if UNITY_EDITOR
                managedCommands.Add(new StateScriptManagedCommandElement
                {
                    Type = StateScriptManagedCommandType.TraceSkillRequestBuilt,
                    GraphIndex = graphIndex,
                    NodeIndex = pulse.NodeIndex,
                    IntValue = skillCommand.IntValue,
                    Position = skillCommand.Position,
                    TargetEntity = skillCommand.TargetEntity,
                });
#endif
                return Emit(ref graph, pulse.NodeIndex, StateScriptPortId.Out, ref pulses);

            case StateScriptNodeRuntimeType.PublishGameEvent:
                if (pulse.InputPortId != StateScriptPortId.In || node.ExpressionCount != 1 ||
                    !TryEvaluateValue(ref graph, node.ExpressionStart, in context, out UnitSourceValue reference))
                    return true;
                managedCommands.Add(new StateScriptManagedCommandElement
                {
                    Type = StateScriptManagedCommandType.PublishGameEvent,
                    GraphIndex = graphIndex,
                    NodeIndex = pulse.NodeIndex,
                    Value = reference,
                });
                return Emit(ref graph, pulse.NodeIndex, StateScriptPortId.Out, ref pulses);

            case StateScriptNodeRuntimeType.RequestInteraction:
                if (pulse.InputPortId != StateScriptPortId.In)
                    return false;
                Entity target = Entity.Null;
                if ((InteractionRequestSource)node.IntParameters.x == InteractionRequestSource.Fixed &&
                    (node.ExpressionCount != 1 ||
                     !TryEvaluateValue(ref graph, node.ExpressionStart, in context, out UnitSourceValue targetValue) ||
                     !targetValue.TryGetEntity(out target)))
                {
                    return true;
                }
                managedCommands.Add(new StateScriptManagedCommandElement
                {
                    Type = StateScriptManagedCommandType.RequestInteraction,
                    GraphIndex = graphIndex,
                    NodeIndex = pulse.NodeIndex,
                    TargetEntity = target,
                });
                return Emit(ref graph, pulse.NodeIndex, StateScriptPortId.Out, ref pulses);

            case StateScriptNodeRuntimeType.SpawnUnit:
                if (pulse.InputPortId != StateScriptPortId.In)
                    return false;
                managedCommands.Add(new StateScriptManagedCommandElement
                {
                    Type = StateScriptManagedCommandType.SpawnUnit,
                    GraphIndex = graphIndex,
                    NodeIndex = pulse.NodeIndex,
                    IntValue = (int)component.TickVersion,
                });
                return Emit(ref graph, pulse.NodeIndex, StateScriptPortId.Out, ref pulses);

            case StateScriptNodeRuntimeType.Timer:
            case StateScriptNodeRuntimeType.Keep:
            case StateScriptNodeRuntimeType.Monitor:
            case StateScriptNodeRuntimeType.NumberMonitor:
            case StateScriptNodeRuntimeType.Addition:
                return ProcessStatePulse(
                    entity,
                    graphIndex,
                    pulse,
                    in node,
                    ref graph,
                    stateIndex,
                    in context,
                    ref component,
                    ref states,
                    ref managedCommands,
                    ref pulses);

            default:
                return false;
        }
    }

    private bool ProcessStatePulse(
        Entity entity,
        int graphIndex,
        StateScriptPulse pulse,
        in StateScriptNodeDefinition node,
        ref StateScriptGraphDefinitionBlob graph,
        int stateIndex,
        in UnitSourceContext context,
        ref UnitStateScriptComponent component,
        ref DynamicBuffer<StateScriptNodeStateElement> states,
        ref DynamicBuffer<StateScriptManagedCommandElement> managedCommands,
        ref FixedList4096Bytes<StateScriptPulse> pulses)
    {
        StateScriptNodeStateElement state = states[stateIndex];
        if (pulse.InputPortId == StateScriptPortId.Start)
        {
            if (state.Status != StateScriptStateStatus.Stop)
                return true;
            uint nextExecutionVersion = state.ExecutionVersion + 1;
            if (nextExecutionVersion == 0)
                nextExecutionVersion = 1;
            state = default;
            state.Status = StateScriptStateStatus.Pending;
            state.ExecutionVersion = nextExecutionVersion;
            state.PendingTick = component.TickVersion;
            if (node.Type == StateScriptNodeRuntimeType.Timer)
            {
                state.Auxiliary = TryEvaluateNumber(ref graph, node.ExpressionStart, in context, out float duration)
                    ? math.max(0f, duration)
                    : 0f;
#if UNITY_EDITOR
                managedCommands.Add(new StateScriptManagedCommandElement
                {
                    Type = StateScriptManagedCommandType.TraceTimerStarted,
                    GraphIndex = graphIndex,
                    NodeIndex = pulse.NodeIndex,
                    IntValue = (int)component.TickVersion,
                    Position = new float3(state.Auxiliary, DeltaTime, 0f),
                });
#endif
            }
            else if (node.Type == StateScriptNodeRuntimeType.Monitor)
            {
                state.Flags = FlagPrimary;
                if (TryEvaluateCondition(ref graph, node.ExpressionStart, in context))
                    state.Flags |= FlagSecondary;
            }
            else if (node.Type == StateScriptNodeRuntimeType.NumberMonitor &&
                     TryEvaluateNumber(ref graph, node.ExpressionStart, in context, out float number))
            {
                state.Flags = FlagPrimary;
                state.Auxiliary = number;
            }
            states[stateIndex] = state;
            if (node.Type == StateScriptNodeRuntimeType.Addition)
            {
                managedCommands.Add(new StateScriptManagedCommandElement
                {
                    Type = StateScriptManagedCommandType.StartAddition,
                    GraphIndex = graphIndex,
                    NodeIndex = pulse.NodeIndex,
                    ExecutionVersion = state.ExecutionVersion,
                });
            }
            return Emit(ref graph, pulse.NodeIndex, StateScriptPortId.OnStart, ref pulses);
        }

        if (pulse.InputPortId == StateScriptPortId.Abort)
        {
            if (state.Status == StateScriptStateStatus.Stop)
                return true;
            state.Status = StateScriptStateStatus.Stop;
            states[stateIndex] = state;
            if (node.Type == StateScriptNodeRuntimeType.Addition)
            {
                AppendStopAddition(
                    graphIndex,
                    pulse.NodeIndex,
                    state.ExecutionVersion,
                    ref managedCommands);
            }
            return Emit(ref graph, pulse.NodeIndex, StateScriptPortId.OnAbort, ref pulses);
        }

        if (node.Type == StateScriptNodeRuntimeType.Keep && pulse.InputPortId == StateScriptPortId.Keep)
        {
            if (state.Status == StateScriptStateStatus.Stop)
                return true;
            state.LastKeepTick = component.TickVersion;
            if ((state.Flags & FlagPrimary) != 0)
            {
                states[stateIndex] = state;
                return true;
            }
            state.Flags |= FlagPrimary;
            state.TimingStartTick = component.TickVersion;
            states[stateIndex] = state;
            return Emit(ref graph, pulse.NodeIndex, StateScriptPortId.OnTimeStart, ref pulses);
        }
        return false;
    }

    private bool UpdateStateNode(
        Entity entity,
        int graphIndex,
        int nodeIndex,
        ref StateScriptGraphDefinitionBlob graph,
        int stateStart,
        in UnitSourceContext context,
        ref UnitStateScriptComponent component,
        ref DynamicBuffer<StateScriptNodeStateElement> states,
        ref DynamicBuffer<StateScriptSourceCommandElement> sourceCommands,
        ref DynamicBuffer<StateScriptSourceCommandArgumentElement> sourceArguments,
        ref DynamicBuffer<StateScriptManagedCommandElement> managedCommands,
        ref FixedList4096Bytes<StateScriptPulse> pulses)
    {
        StateScriptNodeDefinition node = graph.Nodes[nodeIndex];
        int stateIndex = stateStart + nodeIndex;
        StateScriptNodeStateElement state = states[stateIndex];
        switch (node.Type)
        {
            case StateScriptNodeRuntimeType.Timer:
                state.Time += DeltaTime;
                states[stateIndex] = state;
                if (state.Time >= state.Auxiliary)
                {
#if UNITY_EDITOR
                    managedCommands.Add(new StateScriptManagedCommandElement
                    {
                        Type = StateScriptManagedCommandType.TraceTimerCompleted,
                        GraphIndex = graphIndex,
                        NodeIndex = nodeIndex,
                        IntValue = (int)component.TickVersion,
                        Position = new float3(state.Time, state.Auxiliary, DeltaTime),
                    });
#endif
                    if (!CompleteState(ref graph, nodeIndex, stateIndex, ref states, ref pulses))
                        return false;
                }
                break;

            case StateScriptNodeRuntimeType.Keep:
                if ((state.Flags & FlagPrimary) != 0)
                {
                    uint requiredKeepTick = math.max(state.TimingStartTick, component.TickVersion - 1);
                    if (state.LastKeepTick < requiredKeepTick)
                    {
                        state.Flags = (byte)(state.Flags & ~FlagPrimary);
                        states[stateIndex] = state;
                        if (!EmitAndDrain(
                                entity, graphIndex, nodeIndex, StateScriptPortId.OnTimeStop,
                                ref graph, stateStart, in context, ref component, ref states,
                                ref sourceCommands, ref sourceArguments, ref managedCommands, ref pulses))
                            return false;
                        if (states[stateIndex].Status != StateScriptStateStatus.Stop)
                        {
                            state = states[stateIndex];
                            state.Status = StateScriptStateStatus.Stop;
                            states[stateIndex] = state;
                            if (!Emit(ref graph, nodeIndex, StateScriptPortId.OnStop, ref pulses))
                                return false;
                        }
                    }
                    else
                    {
                        state.Time += DeltaTime;
                        states[stateIndex] = state;
                        if (!EmitAndDrain(
                                entity, graphIndex, nodeIndex, StateScriptPortId.OnTimeTick,
                                ref graph, stateStart, in context, ref component, ref states,
                                ref sourceCommands, ref sourceArguments, ref managedCommands, ref pulses))
                            return false;
                        if (states[stateIndex].Status != StateScriptStateStatus.Stop && state.Time >= node.FloatParameters0.x)
                        {
                            state = states[stateIndex];
                            state.Flags = (byte)(state.Flags & ~FlagPrimary);
                            states[stateIndex] = state;
                            if (!EmitAndDrain(
                                    entity, graphIndex, nodeIndex, StateScriptPortId.OnTimeComplete,
                                    ref graph, stateStart, in context, ref component, ref states,
                                    ref sourceCommands, ref sourceArguments, ref managedCommands, ref pulses))
                                return false;
                            if (states[stateIndex].Status != StateScriptStateStatus.Stop &&
                                !CompleteState(ref graph, nodeIndex, stateIndex, ref states, ref pulses))
                                return false;
                        }
                    }
                }
                break;

            case StateScriptNodeRuntimeType.Monitor:
            {
                bool value = TryEvaluateCondition(ref graph, node.ExpressionStart, in context);
                bool hasLast = (state.Flags & FlagPrimary) != 0;
                bool last = (state.Flags & FlagSecondary) != 0;
                if (hasLast && value != last)
                {
                    if (!EmitAndDrain(
                            entity, graphIndex, nodeIndex,
                            value ? StateScriptPortId.OnChangeTrue : StateScriptPortId.OnChangeFalse,
                            ref graph, stateStart, in context, ref component, ref states,
                            ref sourceCommands, ref sourceArguments, ref managedCommands, ref pulses))
                        return false;
                    if (states[stateIndex].Status == StateScriptStateStatus.Stop)
                        break;
                }
                if (!EmitAndDrain(
                        entity, graphIndex, nodeIndex,
                        value ? StateScriptPortId.MonitorTrue : StateScriptPortId.MonitorFalse,
                        ref graph, stateStart, in context, ref component, ref states,
                        ref sourceCommands, ref sourceArguments, ref managedCommands, ref pulses))
                    return false;
                state = states[stateIndex];
                state.Flags |= FlagPrimary;
                if (value) state.Flags |= FlagSecondary;
                else state.Flags = (byte)(state.Flags & ~FlagSecondary);
                states[stateIndex] = state;
                break;
            }

            case StateScriptNodeRuntimeType.NumberMonitor:
                if (TryEvaluateNumber(ref graph, node.ExpressionStart, in context, out float numberValue))
                {
                    bool hasLast = (state.Flags & FlagPrimary) != 0;
                    if (hasLast && !Approximately(numberValue, state.Auxiliary) &&
                        !EmitAndDrain(
                            entity, graphIndex, nodeIndex, StateScriptPortId.OnValueChange,
                            ref graph, stateStart, in context, ref component, ref states,
                            ref sourceCommands, ref sourceArguments, ref managedCommands, ref pulses))
                        return false;
                    state = states[stateIndex];
                    state.Flags |= FlagPrimary;
                    state.Auxiliary = numberValue;
                    states[stateIndex] = state;
                }
                break;
        }

        if (states[stateIndex].Status == StateScriptStateStatus.Running)
        {
            if (!EmitAndDrain(
                    entity, graphIndex, nodeIndex, StateScriptPortId.OnTick,
                    ref graph, stateStart, in context, ref component, ref states,
                    ref sourceCommands, ref sourceArguments, ref managedCommands, ref pulses))
                return false;
        }
        return true;
    }

    private bool EmitAndDrain(
        Entity entity,
        int graphIndex,
        int nodeIndex,
        byte outputPortId,
        ref StateScriptGraphDefinitionBlob graph,
        int stateStart,
        in UnitSourceContext context,
        ref UnitStateScriptComponent component,
        ref DynamicBuffer<StateScriptNodeStateElement> states,
        ref DynamicBuffer<StateScriptSourceCommandElement> sourceCommands,
        ref DynamicBuffer<StateScriptSourceCommandArgumentElement> sourceArguments,
        ref DynamicBuffer<StateScriptManagedCommandElement> managedCommands,
        ref FixedList4096Bytes<StateScriptPulse> pulses)
    {
        return Emit(ref graph, nodeIndex, outputPortId, ref pulses) &&
               DrainPulses(
                   entity, graphIndex, ref graph, stateStart, in context, ref component, ref states,
                   ref sourceCommands, ref sourceArguments, ref managedCommands, ref pulses);
    }

    private static bool CompleteState(
        ref StateScriptGraphDefinitionBlob graph,
        int nodeIndex,
        int stateIndex,
        ref DynamicBuffer<StateScriptNodeStateElement> states,
        ref FixedList4096Bytes<StateScriptPulse> pulses)
    {
        StateScriptNodeStateElement state = states[stateIndex];
        if (state.Status == StateScriptStateStatus.Stop)
            return true;
        state.Status = StateScriptStateStatus.Stop;
        states[stateIndex] = state;
        return Emit(ref graph, nodeIndex, StateScriptPortId.OnComplete, ref pulses);
    }

    private static void StopGraph(
        int graphIndex,
        ref StateScriptGraphDefinitionBlob graph,
        int stateStart,
        ref DynamicBuffer<StateScriptNodeStateElement> states,
        ref DynamicBuffer<StateScriptManagedCommandElement> managedCommands)
    {
        for (int index = 0; index < graph.StateNodeIndices.Length; index++)
        {
            int nodeIndex = graph.StateNodeIndices[index];
            int stateIndex = stateStart + nodeIndex;
            StateScriptNodeStateElement state = states[stateIndex];
            if (state.Status == StateScriptStateStatus.Stop)
                continue;
            uint executionVersion = state.ExecutionVersion;
            state = default;
            state.ExecutionVersion = executionVersion;
            states[stateIndex] = state;
            if (graph.Nodes[nodeIndex].Type == StateScriptNodeRuntimeType.Addition)
                AppendStopAddition(graphIndex, nodeIndex, executionVersion, ref managedCommands);
        }
    }

    private static void AppendStopAddition(
        int graphIndex,
        int nodeIndex,
        uint executionVersion,
        ref DynamicBuffer<StateScriptManagedCommandElement> managedCommands)
    {
        managedCommands.Add(new StateScriptManagedCommandElement
        {
            Type = StateScriptManagedCommandType.StopAddition,
            GraphIndex = graphIndex,
            NodeIndex = nodeIndex,
            ExecutionVersion = executionVersion,
        });
    }

    private bool TryApplySourceValue(
        in StateScriptNodeDefinition node,
        in UnitSourceContext context,
        ref StateScriptGraphDefinitionBlob graph)
    {
        UnitSourceArguments arguments = default;
        if (node.ExpressionCount > arguments.Values.Capacity)
            return false;
        for (int index = 0; index < node.ExpressionCount; index++)
        {
            if (!TryEvaluateValue(ref graph, node.ExpressionStart + index, in context, out UnitSourceValue value))
                return false;
            arguments.Values.Add(value);
        }

        arguments.Key = node.Key;
        arguments.HasKey = node.IntParameters.x != 0 ? (byte)1 : (byte)0;
        Sources.TrySet(
            context.Resolve(node.SetSourceTarget),
            node.SetSourceId,
            in arguments);
        return true;
    }

    private bool TryBuildSkillCommand(
        in StateScriptNodeDefinition node,
        ref StateScriptGraphDefinitionBlob graph,
        in UnitSourceContext context,
        out StateScriptManagedCommandElement command
#if UNITY_EDITOR
        , out StateScriptSkillRequestBuildError error
#endif
        )
    {
        command = default;
#if UNITY_EDITOR
        error = StateScriptSkillRequestBuildError.None;
#endif
        if (node.ExpressionCount != 3)
        {
#if UNITY_EDITOR
            error = StateScriptSkillRequestBuildError.InvalidExpressionCount;
#endif
            return false;
        }
        if (!TryEvaluateNumber(ref graph, node.ExpressionStart, in context, out float rawSkillId) ||
            !math.isfinite(rawSkillId))
        {
#if UNITY_EDITOR
            error = StateScriptSkillRequestBuildError.SkillIdEvaluationFailed;
#endif
            return false;
        }
        float rounded = math.round(rawSkillId);
        if (rounded < 0f || rounded > int.MaxValue || math.abs(rawSkillId - rounded) > 0.0001f)
        {
#if UNITY_EDITOR
            error = StateScriptSkillRequestBuildError.SkillIdNotInteger;
#endif
            return false;
        }
        if (!TryEvaluateValue(ref graph, node.ExpressionStart + 1, in context, out UnitSourceValue positionValue))
        {
#if UNITY_EDITOR
            error = StateScriptSkillRequestBuildError.PositionEvaluationFailed;
#endif
            return false;
        }
        if (!positionValue.TryGetFloat3(out float3 position))
        {
#if UNITY_EDITOR
            error = StateScriptSkillRequestBuildError.PositionTypeMismatch;
#endif
            return false;
        }
        if (!TryEvaluateValue(ref graph, node.ExpressionStart + 2, in context, out UnitSourceValue targetValue))
        {
#if UNITY_EDITOR
            error = StateScriptSkillRequestBuildError.TargetEvaluationFailed;
#endif
            return false;
        }
        if (!targetValue.TryGetEntity(out Entity target))
        {
#if UNITY_EDITOR
            error = StateScriptSkillRequestBuildError.TargetTypeMismatch;
#endif
            return false;
        }
        command.IntValue = (int)rounded;
        command.Position = position;
        command.TargetEntity = target;
        return true;
    }

    private bool TryEvaluateValue(
        ref StateScriptGraphDefinitionBlob graph,
        int expressionIndex,
        in UnitSourceContext context,
        out UnitSourceValue value)
    {
        value = default;
        if ((uint)expressionIndex >= (uint)graph.Expressions.Length)
            return false;
        ref BehaviorExpressionBlob expression = ref graph.Expressions[expressionIndex];
        return CompiledExpressionEvaluator.TryEvaluateValue(ref expression, in context, in Sources, out value);
    }

    private bool TryEvaluateNumber(
        ref StateScriptGraphDefinitionBlob graph,
        int expressionIndex,
        in UnitSourceContext context,
        out float value)
    {
        value = 0f;
        return TryEvaluateValue(ref graph, expressionIndex, in context, out UnitSourceValue sourceValue) &&
               sourceValue.TryGetNumber(out value) && math.isfinite(value);
    }

    private bool TryEvaluateCondition(
        ref StateScriptGraphDefinitionBlob graph,
        int expressionIndex,
        in UnitSourceContext context)
    {
        if ((uint)expressionIndex >= (uint)graph.Expressions.Length)
            return false;
        ref BehaviorExpressionBlob expression = ref graph.Expressions[expressionIndex];
        return CompiledExpressionEvaluator.TryEvaluateConditions(ref expression, in context, in Sources);
    }

#if UNITY_EDITOR
    private void AppendCurrentInputTypeTrace(
        int graphIndex,
        int nodeIndex,
        int expressionIndex,
        bool conditionResult,
        ref StateScriptGraphDefinitionBlob graph,
        in UnitSourceContext context,
        ref DynamicBuffer<StateScriptManagedCommandElement> managedCommands)
    {
        if ((uint)expressionIndex >= (uint)graph.Expressions.Length)
            return;

        ref BehaviorExpressionBlob expression = ref graph.Expressions[expressionIndex];
        bool usesCurrentInputType = false;
        for (int index = 0; index < expression.Instructions.Length; index++)
        {
            ExpressionInstruction instruction = expression.Instructions[index];
            if (instruction.Kind == ExpressionInstructionKind.Source &&
                instruction.SourceId == UnitSourceId.PlayerSkillCurrentInputType)
            {
                usesCurrentInputType = true;
                break;
            }
        }
        if (!usesCurrentInputType)
            return;

        UnitSourceArguments emptyArguments = default;
        float skillId = -999f;
        bool hasSkillId = Sources.TryGet(
                              context.Self,
                              UnitSourceId.PlayerSkillCurrentSkillId,
                              in emptyArguments,
                              out UnitSourceValue skillIdValue) &&
                          skillIdValue.TryGetNumber(out skillId);
        float currentInputType = -999f;
        bool hasCurrentInputType = Sources.TryGet(
                                       context.Self,
                                       UnitSourceId.PlayerSkillCurrentInputType,
                                       in emptyArguments,
                                       out UnitSourceValue currentInputTypeValue) &&
                                   currentInputTypeValue.TryGetNumber(out currentInputType);

        bool hasDirectInputType = false;
        float directInputType = -999f;
        if (hasSkillId)
        {
            UnitSourceArguments directArguments = default;
            directArguments.Values.Add(skillIdValue);
            hasDirectInputType = Sources.TryGet(
                                     context.Self,
                                     UnitSourceId.PlayerSkillGetInputType,
                                     in directArguments,
                                     out UnitSourceValue directInputTypeValue) &&
                                 directInputTypeValue.TryGetNumber(out directInputType);
        }

        int successMask = (hasSkillId ? 1 : 0) |
                          (hasCurrentInputType ? 2 : 0) |
                          (hasDirectInputType ? 4 : 0);
        managedCommands.Add(new StateScriptManagedCommandElement
        {
            Type = StateScriptManagedCommandType.TraceCurrentInputType,
            GraphIndex = graphIndex,
            NodeIndex = nodeIndex,
            IntValue = successMask,
            Position = new float3(
                hasSkillId ? skillId : -999f,
                hasCurrentInputType ? currentInputType : -999f,
                hasDirectInputType ? directInputType : -999f),
            Value = UnitSourceValue.FromBool(conditionResult),
        });
    }
#endif

    private static bool Emit(
        ref StateScriptGraphDefinitionBlob graph,
        int nodeIndex,
        byte outputPortId,
        ref FixedList4096Bytes<StateScriptPulse> pulses)
    {
        if ((uint)nodeIndex >= (uint)graph.Nodes.Length)
            return false;
        StateScriptNodeDefinition node = graph.Nodes[nodeIndex];
        for (int routeIndex = 0; routeIndex < node.OutputRouteCount; routeIndex++)
        {
            StateScriptOutputRoute route = graph.OutputRoutes[node.OutputRouteStart + routeIndex];
            if (route.OutputPortId != outputPortId)
                continue;
            if (pulses.Length + route.TargetCount > math.min(pulses.Capacity, MaxPulseDepth))
                return false;
            for (int targetIndex = route.TargetCount - 1; targetIndex >= 0; targetIndex--)
            {
                StateScriptPulseTarget target = graph.PulseTargets[route.TargetStart + targetIndex];
                pulses.Add(new StateScriptPulse
                {
                    NodeIndex = target.NodeIndex,
                    InputPortId = target.InputPortId,
                });
            }
            return true;
        }
        return true;
    }

    private static bool Approximately(float left, float right)
    {
        return math.abs(left - right) <= math.max(0.000001f * math.max(math.abs(left), math.abs(right)), 1.121039E-44f);
    }
}

[BurstCompile]
[WithAll(typeof(UnitDeathComponent))]
public partial struct StateScriptDeathJob : IJobEntity
{
    private void Execute(
        ref UnitStateScriptComponent component,
        ref DynamicBuffer<StateScriptGraphStateElement> graphStates,
        ref DynamicBuffer<StateScriptNodeStateElement> nodeStates,
        ref DynamicBuffer<StateScriptSourceCommandElement> sourceCommands,
        ref DynamicBuffer<StateScriptSourceCommandArgumentElement> sourceArguments)
    {
        if (component.IsStoppedForDeath != 0)
            return;
        component.IsStoppedForDeath = 1;
        for (int index = 0; index < graphStates.Length; index++)
        {
            StateScriptGraphStateElement graph = graphStates[index];
            graph.IsActive = 0;
            graphStates[index] = graph;
        }
        for (int index = 0; index < nodeStates.Length; index++)
            nodeStates[index] = default;
        sourceCommands.Clear();
        sourceArguments.Clear();
    }
}
