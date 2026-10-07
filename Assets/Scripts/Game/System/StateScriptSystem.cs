using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Server;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Core;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation |
                   WorldSystemFilterFlags.ClientSimulation |
                   WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateAfter(typeof(BehaviorTreeSystem))]
public partial class StateScriptSystem : SystemBase
{
    private UnitSourceDispatcher _sources;
    private EntityQuery _localPlayerQuery;
    private EntityQuery _managedCommandQueueQuery;
    private BattleSimulationScope _scope;
    private FixedList128Bytes<Entity> _clientInteractionTargets;

    protected override void OnCreate()
    {
        _sources.Initialize(this);
        StateScriptManagedCommandQueueUtility.GetOrCreateEntity(EntityManager);
        _managedCommandQueueQuery = GetEntityQuery(
            ComponentType.ReadOnly<StateScriptManagedCommandQueueComponent>(),
            ComponentType.ReadWrite<StateScriptManagedCommandElement>());
        _localPlayerQuery = GetEntityQuery(
            ComponentType.ReadOnly<NetworkPlayerComponent>(),
            ComponentType.ReadOnly<UnitStateScriptComponent>());
        RequireForUpdate<StateScriptRuntimeRegistryComponent>();
    }

    protected override void OnUpdate()
    {
        SystemAPI.TryGetSingleton(out _scope);
        BlobAssetReference<StateScriptRuntimeRegistryBlob> registry =
            SystemAPI.GetSingleton<StateScriptRuntimeRegistryComponent>().Value;
        if (!registry.IsCreated)
            return;

        GameWorldRole role = GameWorldContextUtility.Get(EntityManager).Role;
        GameWorldExecutionTarget executionTarget = GameWorldExecutionTargetUtility.FromRole(role);
        Entity localPlayer = Entity.Null;
        bool hasLocalPlayer = role != GameWorldRole.Client || TryGetLocalPlayer(out localPlayer);
        if (role == GameWorldRole.Client && hasLocalPlayer && _scope.Pass == BattleSimulationPass.Players)
            ClientPlayerPhysicsPredictionUtility.RestoreSimulationPosition(EntityManager, localPlayer);
        _sources.Update(this);
        ClientFrameManager clientFrame = null;
        if (role == GameWorldRole.Client && _scope.Pass != BattleSimulationPass.World)
            FrameManagerUtility.TryGet(EntityManager, out clientFrame);
        if (role == GameWorldRole.Client && hasLocalPlayer && clientFrame != null &&
            clientFrame.TryConsumePredictionReplay(out uint authoritativeFrame))
        {
            Dependency.Complete();
            PlayerInputComponent currentInput = default;
            bool restoreCurrentInput = EntityManager.HasComponent<PlayerInputComponent>(localPlayer);
            if (restoreCurrentInput)
                currentInput = EntityManager.GetComponentData<PlayerInputComponent>(localPlayer);
            NativeArray<PlayerInputEventElement> currentInputEvents = default;
            bool restoreCurrentInputEvents = EntityManager.HasBuffer<PlayerInputEventElement>(localPlayer);
            if (restoreCurrentInputEvents)
            {
                currentInputEvents = EntityManager
                    .GetBuffer<PlayerInputEventElement>(localPlayer, true)
                    .ToNativeArray(Allocator.Temp);
            }

            ClientSkillVisualPredictionUtility.RequestRollback(EntityManager, authoritativeFrame);

            ReplayClientPrediction(
                clientFrame,
                localPlayer,
                authoritativeFrame,
                registry,
                executionTarget);

            if (EntityManager.HasComponent<ClientPlayerMovePresentationComponent>(localPlayer))
            {
                ClientPlayerMovePresentationComponent presentation =
                    EntityManager.GetComponentData<ClientPlayerMovePresentationComponent>(localPlayer);
                presentation.CompleteReconciliation(
                    EntityManager.GetComponentData<UnitMoveComponent>(localPlayer).PredictedPosition);
                EntityManager.SetComponentData(localPlayer, presentation);
            }

            if (restoreCurrentInput && EntityManager.Exists(localPlayer))
                EntityManager.SetComponentData(localPlayer, currentInput);
            if (restoreCurrentInputEvents && EntityManager.Exists(localPlayer))
            {
                DynamicBuffer<PlayerInputEventElement> events =
                    EntityManager.GetBuffer<PlayerInputEventElement>(localPlayer);
                events.Clear();
                events.AddRange(currentInputEvents);
            }
            if (currentInputEvents.IsCreated)
                currentInputEvents.Dispose();
            Dependency = default;
            _sources.Update(this);
        }

        // SetValue is part of the immediate pulse chain: later nodes in the same
        // chain must observe its write. Source targets may be Self, Other, or a
        // global entity, so evaluate serially until writes can be safely partitioned.
        if (hasLocalPlayer)
        {
            UnitQueryTree queryTree = GetQueryTree(out int queryCapacity);
            float deltaTime = math.max(0f, SystemAPI.Time.DeltaTime);
            Entity targetEntity = role == GameWorldRole.Client ? localPlayer : Entity.Null;
            Dependency = ScheduleEvaluation(
                Dependency,
                registry,
                queryTree,
                queryCapacity,
                deltaTime,
                executionTarget,
                targetEntity,
                processInputEvents: _scope.Pass != BattleSimulationPass.World,
                evaluateClientInteractions: role == GameWorldRole.Client &&
                                            _scope.Pass != BattleSimulationPass.Players);
        }
        if (_scope.Pass != BattleSimulationPass.Players)
            Dependency = new StateScriptDeathJob().ScheduleParallel(Dependency);
    }

    private UnitQueryTree GetQueryTree(out int capacity)
    {
        capacity = 1;
        if (!SystemAPI.TryGetSingleton(out UnitQuerySingleton singleton) ||
            singleton.TreeEntity == Entity.Null || !EntityManager.Exists(singleton.TreeEntity) ||
            !EntityManager.HasBuffer<UnitQueryNode>(singleton.TreeEntity) ||
            !EntityManager.HasBuffer<UnitQueryEntry>(singleton.TreeEntity))
            return default;
        NativeArray<UnitQueryNode> nodes = EntityManager.GetBuffer<UnitQueryNode>(singleton.TreeEntity, true).AsNativeArray();
        NativeArray<UnitQueryEntry> entries = EntityManager.GetBuffer<UnitQueryEntry>(singleton.TreeEntity, true).AsNativeArray();
        capacity = math.max(1, entries.Length);
        return new UnitQueryTree(nodes, entries);
    }

    private JobHandle ScheduleEvaluation(
        JobHandle dependency,
        BlobAssetReference<StateScriptRuntimeRegistryBlob> registry,
        UnitQueryTree queryTree,
        int queryCapacity,
        float deltaTime,
        GameWorldExecutionTarget executionTarget,
        Entity targetEntity,
        bool processInputEvents,
        bool evaluateClientInteractions = false)
    {
        DynamicBuffer<StateScriptManagedCommandElement> managedCommands =
            _managedCommandQueueQuery.GetSingletonBuffer<StateScriptManagedCommandElement>();
        managedCommands.Clear();
        FixedList128Bytes<Entity> interactionTargets = evaluateClientInteractions
            ? CollectClientInteractionTargets(targetEntity)
            : default;
        NativeList<UnitQueryHit> queryResults = new(queryCapacity, Allocator.TempJob);
        NativeList<Entity> queryExclusions = new(queryCapacity, Allocator.TempJob);
        JobHandle evaluationHandle = new StateScriptEvaluationJob
        {
            Registry = registry,
            Sources = _sources,
            Difficulties = GetComponentLookup<DungeonDifficultyComponent>(true),
            InputEvents = GetBufferLookup<PlayerInputEventElement>(true),
            PlayerInputs = GetComponentLookup<PlayerInputComponent>(),
            QueryTree = queryTree,
            QueryResults = queryResults,
            QueryExclusions = queryExclusions,
            ManagedCommands = managedCommands,
            DeltaTime = deltaTime,
            ExecutionTarget = executionTarget,
            TargetEntity = targetEntity,
            ClientInteractionTargets = interactionTargets,
            Scope = _scope,
            ProcessInputEventBuffer = processInputEvents ? (byte)1 : (byte)0,
        }.Schedule(dependency);

        JobHandle queryResultsDisposeHandle = queryResults.Dispose(evaluationHandle);
        JobHandle queryExclusionsDisposeHandle = queryExclusions.Dispose(evaluationHandle);
        return JobHandle.CombineDependencies(queryResultsDisposeHandle, queryExclusionsDisposeHandle);
    }

    private FixedList128Bytes<Entity> CollectClientInteractionTargets(Entity localPlayer)
    {
        FixedList128Bytes<Entity> targets = default;
        // Keep a completed target for one more world tick. The player can acknowledge
        // its transaction first; the target still needs to consume the UI completion
        // and reset its monitor so the next interaction can start.
        for (int index = 0; index < _clientInteractionTargets.Length; index++)
        {
            Entity previous = _clientInteractionTargets[index];
            if (EntityManager.Exists(previous))
                targets.Add(previous);
        }
        _clientInteractionTargets.Clear();
        if (SystemAPI.TryGetSingletonEntity<GameInteractionComponent>(out Entity runtime) &&
            EntityManager.HasBuffer<InteractionTransactionElement>(runtime))
        {
            DynamicBuffer<InteractionTransactionElement> transactions =
                EntityManager.GetBuffer<InteractionTransactionElement>(runtime, true);
            for (int index = 0; index < transactions.Length; index++)
            {
                InteractionTransactionElement transaction = transactions[index];
                if (transaction.Actor != localPlayer || !EntityManager.Exists(transaction.Target))
                    continue;
                _clientInteractionTargets.Add(transaction.Target);
                if (!targets.Contains(transaction.Target))
                    targets.Add(transaction.Target);
            }
        }
        // TryRequest permits one transaction per actor: at most the current target
        // and the previous target being cleaned up. Keep the snapshot inline.
        return targets;
    }

    private void ReplayClientPrediction(
        ClientFrameManager frame,
        Entity player,
        uint authoritativeFrame,
        BlobAssetReference<StateScriptRuntimeRegistryBlob> registry,
        GameWorldExecutionTarget executionTarget)
    {
        if (authoritativeFrame >= frame.currentFrame ||
            !EntityManager.HasComponent<NetworkIdentityComponent>(player))
        {
            return;
        }

        Guid unitId = EntityManager.GetComponentData<NetworkIdentityComponent>(player).id;
        float fixedDeltaTime = math.max(0.001f, frame.frameInterval / 1000f);
        for (uint replayFrame = authoritativeFrame + 1;
             replayFrame < frame.currentFrame;
             replayFrame++)
        {
            ApplyReplayInput(frame, player, replayFrame);
            ClientPlayerPhysicsPredictionUtility.RestoreSimulationPosition(EntityManager, player);
            _sources.Update(this);
            // Rollback setup and replayed input can add components or buffers.
            // Never retain a DynamicBuffer view across those structural changes.
            UnitQueryTree queryTree = GetQueryTree(out int queryCapacity);
            JobHandle replayDependency = ScheduleEvaluation(
                default,
                registry,
                queryTree,
                queryCapacity,
                fixedDeltaTime,
                executionTarget,
                player,
                processInputEvents: true);
            replayDependency.Complete();

            ClientSkillVisualPredictionUtility.CaptureSkillRequests(
                EntityManager,
                player,
                replayFrame);

            // Replay the same movement and collision pipeline as a normal player tick.
            // Recording velocity * dt here would let the replay pass through obstacles.
            World.PushTime(new TimeData((replayFrame + 1d) * fixedDeltaTime, fixedDeltaTime));
            try
            {
                World.GetExistingSystemManaged<StateScriptManagedCommandSystem>()?.ReplayClientSkills();
                World.GetExistingSystem<UnitMoveSystem>().Update(World.Unmanaged);
                World.GetExistingSystemManaged<FixedStepSimulationSystemGroup>().Update();
                ClientPlayerPhysicsPredictionUtility.CaptureSimulationPosition(EntityManager, player);
            }
            finally
            {
                World.PopTime();
            }

            frame.RecordPlayerStates(
                replayFrame,
                ClientPlayerPredictionSnapshot.Capture(EntityManager, player, unitId));

            if (replayFrame == uint.MaxValue)
                break;
        }
    }

    private void ApplyReplayInput(ClientFrameManager frame, Entity player, uint replayFrame)
    {
        if (!EntityManager.Exists(player) || !EntityManager.HasComponent<PlayerInputComponent>(player))
            return;

        if (EntityManager.HasBuffer<PlayerInputEventElement>(player))
            EntityManager.GetBuffer<PlayerInputEventElement>(player).Clear();

        PlayerInputComponent frameInput = EntityManager.GetComponentData<PlayerInputComponent>(player);
        frameInput.IsPrimaryHeld = frameInput.ContinuousPrimaryHeld;
        frameInput.IsInteractHeld = 0;
        frameInput.IsSkillHeld = 0;
        frameInput.IsUsePropHeld = 0;
        frameInput.PropIndex = -1;
        EntityManager.SetComponentData(player, frameInput);

        if (!frame.inputOrder.TryGetValue(replayFrame, out Queue<NetworkStateData> inputs))
            return;

        NetworkStateApplyContext context = new(
            EntityManager,
            replayFrame,
            frame.frameInterval,
            updateClientPresentationClock: false);
        foreach (NetworkStateData input in inputs)
        {
            if (input is NetworkPlayerInputStateData)
                input.Apply(context);
            else if (input is NetworkPlayerOperationData operation)
                ApplyReplayOperation(player, operation);
        }
    }

    private void ApplyReplayOperation(Entity player, NetworkPlayerOperationData operation)
    {
        PlayerInputComponent input = EntityManager.GetComponentData<PlayerInputComponent>(player);
        switch (operation)
        {
            case NetworkPrimaryPressData primary:
                input.PointerWorldPosition = new float3(primary.pointerX, primary.pointerY, primary.pointerZ);
                PlayerInputEventUtility.Append(
                    EntityManager,
                    player,
                    PlayerInputOperationType.PrimaryPressed,
                    input);
                break;
            case NetworkInteractData:
                PlayerInputEventUtility.Append(
                    EntityManager,
                    player,
                    PlayerInputOperationType.Interact,
                    input);
                break;
            case NetworkSkillChainSelectData select:
                input.SkillChainIndex = select.skillChainIndex;
                EntityManager.SetComponentData(player, input);
                PlayerInputEventUtility.Append(
                    EntityManager,
                    player,
                    PlayerInputOperationType.SelectSkillChain,
                    input);
                break;
            case NetworkPropUseData prop:
                input.PropIndex = prop.slotIndex;
                PlayerInputEventUtility.Append(
                    EntityManager,
                    player,
                    PlayerInputOperationType.UseProp,
                    input);
                break;
        }
    }

    private bool TryGetLocalPlayer(out Entity player)
    {
        if (_localPlayerQuery.IsEmptyIgnoreFilter)
        {
            player = Entity.Null;
            return false;
        }

        using NativeArray<Entity> entities = _localPlayerQuery.ToEntityArray(Allocator.Temp);
        player = entities.Length > 0 ? entities[0] : Entity.Null;
        return player != Entity.Null;
    }
}

[BurstCompile]
[WithNone(typeof(UnitInitializationPendingTag))]
[WithNone(typeof(UnitDeathComponent))]
[WithNone(typeof(BattleSpectatorComponent))]
public partial struct StateScriptEvaluationJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<DungeonDifficultyComponent> Difficulties;
    private const int MaxPulseDepth = 128;
    private const byte FlagPrimary = 1 << 0;
    private const byte FlagSecondary = 1 << 1;

    public BlobAssetReference<StateScriptRuntimeRegistryBlob> Registry;

    public UnitSourceDispatcher Sources;

    [ReadOnly]
    public BufferLookup<PlayerInputEventElement> InputEvents;

    // 此 Job 串行执行。Source 内持有同一输入组件的只读 lookup；处理事件时
    // 临时恢复该次输入快照，让后续表达式看到当时的鼠标/技能链，结束后还原。
    [NativeDisableContainerSafetyRestriction]
    public ComponentLookup<PlayerInputComponent> PlayerInputs;

    [ReadOnly]
    public UnitQueryTree QueryTree;

    public NativeList<UnitQueryHit> QueryResults;

    public NativeList<Entity> QueryExclusions;

    public DynamicBuffer<StateScriptManagedCommandElement> ManagedCommands;

    public float DeltaTime;

    public GameWorldExecutionTarget ExecutionTarget;

    public Entity TargetEntity;
    public FixedList128Bytes<Entity> ClientInteractionTargets;
    public BattleSimulationScope Scope;

    public byte ProcessInputEventBuffer;

    private bool IsClientInteractionTarget(Entity entity)
    {
        for (int index = 0; index < ClientInteractionTargets.Length; index++)
            if (ClientInteractionTargets[index] == entity)
                return true;
        return false;
    }

    private void Execute(
        Entity entity,
        ref UnitStateScriptComponent component,
        ref DynamicBuffer<StateScriptGraphStateElement> graphStates,
        ref DynamicBuffer<StateScriptNodeStateElement> nodeStates,
        ref DynamicBuffer<StateScriptSourceCommandElement> sourceCommands,
        ref DynamicBuffer<StateScriptSourceCommandArgumentElement> sourceArguments,
        ref DynamicBuffer<StateScriptExternalResultElement> externalResults)
    {
        DynamicBuffer<StateScriptManagedCommandElement> managedCommands = ManagedCommands;
        if ((Scope.Pass != BattleSimulationPass.All && !Scope.Includes(PlayerInputs.HasComponent(entity))) ||
            (TargetEntity != Entity.Null && entity != TargetEntity && !IsClientInteractionTarget(entity)))
            return;

        sourceCommands.Clear();
        sourceArguments.Clear();
        if (component.IsStoppedForDeath != 0 ||
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

        bool hasInputEvents = ProcessInputEventBuffer != 0 &&
                              InputEvents.HasBuffer(entity) &&
                              InputEvents[entity].Length > 0;

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

            bool enabled = IsNodeEnabled(graph.Nodes[graph.EntryNodeIndex]) &&
                (graph.ExecutionConditionExpressionIndex < 0 ||
                 TryEvaluateCondition(ref graph, graph.ExecutionConditionExpressionIndex, in context));
            if (!enabled)
            {
                if (graphState.IsActive != 0)
                {
                    StopGraph(
                        entity,
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
#if UNITY_EDITOR && STATE_SCRIPT_TRACE
                AddManagedCommand(entity, ref managedCommands, new StateScriptManagedCommandElement
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
                if (!IsNodeEnabled(graph.Nodes[nodeIndex]))
                    continue;
                int stateIndex = graphState.NodeStateStart + nodeIndex;
                StateScriptNodeStateElement state = nodeStates[stateIndex];
                if (state.Status == StateScriptStateStatus.Pending && state.PendingTick < component.TickVersion)
                {
                    state.Status = StateScriptStateStatus.Running;
                    nodeStates[stateIndex] = state;
                }
            }

            // 没有输入事件的单位保持原来的逐图更新顺序。
            if (!hasInputEvents && !TickGraph(entity, graphIndex, ref graph, graphState.NodeStateStart,
                    in context, ref component, ref nodeStates, ref sourceCommands,
                    ref sourceArguments, ref managedCommands))
            {
                component.InitializationError = StateScriptInitializationError.InvalidDefinition;
                externalResults.Clear();
                return;
            }
        }

        if (!hasInputEvents)
        {
            externalResults.Clear();
            return;
        }

        // 先按事件顺序处理所有监听节点，再推进一次状态时间；事件多不会多走计时器。
        if (!ProcessInputEvents(entity, ref unit, in context, ref component, ref graphStates,
                ref nodeStates, ref sourceCommands, ref sourceArguments, ref managedCommands))
        {
            component.InitializationError = StateScriptInitializationError.InvalidDefinition;
            externalResults.Clear();
            return;
        }

        for (int graphIndex = 0; graphIndex < unit.Graphs.Length; graphIndex++)
        {
            StateScriptGraphStateElement graphState = graphStates[graphIndex];
            if (graphState.IsActive == 0)
                continue;
            ref StateScriptGraphDefinitionBlob graph = ref unit.Graphs[graphIndex];
            if (!TickGraph(entity, graphIndex, ref graph, graphState.NodeStateStart,
                    in context, ref component, ref nodeStates, ref sourceCommands,
                    ref sourceArguments, ref managedCommands))
            {
                component.InitializationError = StateScriptInitializationError.InvalidDefinition;
                externalResults.Clear();
                return;
            }
        }
        externalResults.Clear();
    }

    private bool TickGraph(
        Entity entity, int graphIndex, ref StateScriptGraphDefinitionBlob graph, int stateStart,
        in UnitSourceContext context, ref UnitStateScriptComponent component,
        ref DynamicBuffer<StateScriptNodeStateElement> nodeStates,
        ref DynamicBuffer<StateScriptSourceCommandElement> sourceCommands,
        ref DynamicBuffer<StateScriptSourceCommandArgumentElement> sourceArguments,
        ref DynamicBuffer<StateScriptManagedCommandElement> managedCommands)
    {
        FixedList4096Bytes<StateScriptPulse> pulses = default;
        for (int order = 0; order < graph.StateNodeIndices.Length; order++)
        {
            int nodeIndex = graph.StateNodeIndices[order];
            if (!IsNodeEnabled(graph.Nodes[nodeIndex]) ||
                nodeStates[stateStart + nodeIndex].Status != StateScriptStateStatus.Running)
                continue;
            if (!UpdateStateNode(entity, graphIndex, nodeIndex, ref graph, stateStart, in context,
                    ref component, ref nodeStates, ref sourceCommands, ref sourceArguments,
                    ref managedCommands, ref pulses) ||
                !DrainPulses(entity, graphIndex, ref graph, stateStart, in context,
                    ref component, ref nodeStates, ref sourceCommands, ref sourceArguments, ref managedCommands, ref pulses))
                return false;
        }
        return true;
    }

    private bool ProcessInputEvents(
        Entity entity,
        ref StateScriptUnitDefinitionBlob unit,
        in UnitSourceContext context,
        ref UnitStateScriptComponent component,
        ref DynamicBuffer<StateScriptGraphStateElement> graphStates,
        ref DynamicBuffer<StateScriptNodeStateElement> nodeStates,
        ref DynamicBuffer<StateScriptSourceCommandElement> sourceCommands,
        ref DynamicBuffer<StateScriptSourceCommandArgumentElement> sourceArguments,
        ref DynamicBuffer<StateScriptManagedCommandElement> managedCommands)
    {
        if (!InputEvents.HasBuffer(entity) || !PlayerInputs.HasComponent(entity))
            return true;

        DynamicBuffer<PlayerInputEventElement> events = InputEvents[entity];
        PlayerInputComponent currentInput = PlayerInputs[entity];
        bool success = true;
        for (int eventIndex = 0; eventIndex < events.Length && success; eventIndex++)
        {
            PlayerInputEventElement inputEvent = events[eventIndex];
            PlayerInputs[entity] = inputEvent.Input;
            for (int graphIndex = 0; graphIndex < unit.Graphs.Length && success; graphIndex++)
            {
                StateScriptGraphStateElement graphState = graphStates[graphIndex];
                if (graphState.IsActive == 0)
                    continue;
                ref StateScriptGraphDefinitionBlob graph = ref unit.Graphs[graphIndex];
                FixedList4096Bytes<StateScriptPulse> pulses = default;
                for (int order = 0; order < graph.StateNodeIndices.Length; order++)
                {
                    int nodeIndex = graph.StateNodeIndices[order];
                    StateScriptNodeDefinition node = graph.Nodes[nodeIndex];
                    int stateIndex = graphState.NodeStateStart + nodeIndex;
                    if (node.Type != StateScriptNodeRuntimeType.PlayerInputEvent ||
                        node.IntParameters.x != (int)inputEvent.Type || !IsNodeEnabled(node) ||
                        nodeStates[stateIndex].Status == StateScriptStateStatus.Stop)
                        continue;

                    StateScriptNodeStateElement state = nodeStates[stateIndex];
                    state.LastPulseTick = component.TickVersion;
                    nodeStates[stateIndex] = state;
                    success = EmitAndDrain(entity, graphIndex, nodeIndex, StateScriptPortId.OnInputEvent,
                        ref graph, graphState.NodeStateStart, in context, ref component, ref nodeStates,
                        ref sourceCommands, ref sourceArguments, ref managedCommands, ref pulses);
                    if (!success)
                        break;
                }
            }
        }
        PlayerInputs[entity] = currentInput;
        return success;
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
        if (!IsNodeEnabled(node))
            return true;
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
#if UNITY_EDITOR && STATE_SCRIPT_TRACE
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
#if UNITY_EDITOR && STATE_SCRIPT_TRACE
                        , out StateScriptSkillRequestBuildError buildError
#endif
                    ))
                {
#if UNITY_EDITOR && STATE_SCRIPT_TRACE
                    AddManagedCommand(entity, ref managedCommands, new StateScriptManagedCommandElement
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
                AddManagedCommand(entity, ref managedCommands, skillCommand);
#if UNITY_EDITOR && STATE_SCRIPT_TRACE
                AddManagedCommand(entity, ref managedCommands, new StateScriptManagedCommandElement
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

            case StateScriptNodeRuntimeType.NotifyUI:
                if (pulse.InputPortId != StateScriptPortId.In || node.ExpressionCount != 3 ||
                    !TryEvaluateNumber(ref graph, node.ExpressionStart, in context, out float noticeFrom) ||
                    !TryEvaluateNumber(ref graph, node.ExpressionStart + 1, in context, out float noticeTo) ||
                    !TryEvaluateNumber(ref graph, node.ExpressionStart + 2, in context, out float noticeGroup))
                    return true;
                AddManagedCommand(entity, ref managedCommands, new StateScriptManagedCommandElement
                {
                    Type = StateScriptManagedCommandType.NotifyUI, GraphIndex = graphIndex, NodeIndex = pulse.NodeIndex,
                    Position = new float3(noticeFrom, noticeTo, noticeGroup),
                });
                return Emit(ref graph, pulse.NodeIndex, StateScriptPortId.Out, ref pulses);

            case StateScriptNodeRuntimeType.PublishGameEvent:
                if (pulse.InputPortId != StateScriptPortId.In || node.ExpressionCount != 1 ||
                    !TryEvaluateValue(ref graph, node.ExpressionStart, in context, out UnitSourceValue reference))
                    return true;
                AddManagedCommand(entity, ref managedCommands, new StateScriptManagedCommandElement
                {
                    Type = StateScriptManagedCommandType.PublishGameEvent,
                    GraphIndex = graphIndex,
                    NodeIndex = pulse.NodeIndex,
                    Value = reference,
                });
                return Emit(ref graph, pulse.NodeIndex, StateScriptPortId.Out, ref pulses);

            case StateScriptNodeRuntimeType.RequestInteraction:
                if (pulse.InputPortId != StateScriptPortId.In || node.ExpressionCount != 1 ||
                    !TryEvaluateValue(ref graph, node.ExpressionStart, in context, out UnitSourceValue targetValue) ||
                    !targetValue.TryGetEntity(out Entity target) || target == Entity.Null)
                    return true;
                AddManagedCommand(entity, ref managedCommands, new StateScriptManagedCommandElement
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
                AddManagedCommand(entity, ref managedCommands, new StateScriptManagedCommandElement
                {
                    Type = StateScriptManagedCommandType.SpawnUnit,
                    GraphIndex = graphIndex,
                    NodeIndex = pulse.NodeIndex,
                    IntValue = (int)component.TickVersion,
                });
                return Emit(ref graph, pulse.NodeIndex, StateScriptPortId.Out, ref pulses);

            case StateScriptNodeRuntimeType.CompleteInteraction:
                if (pulse.InputPortId != StateScriptPortId.In || node.ExpressionCount != 1 ||
                    !TryEvaluateValue(ref graph, node.ExpressionStart, in context, out UnitSourceValue interactionResult))
                    return true;
                AddManagedCommand(entity, ref managedCommands, new StateScriptManagedCommandElement
                {
                    Type = StateScriptManagedCommandType.CompleteInteraction,
                    GraphIndex = graphIndex,
                    NodeIndex = pulse.NodeIndex,
                    IntValue = node.IntParameters.x,
                    Value = interactionResult,
                });
                return Emit(ref graph, pulse.NodeIndex, StateScriptPortId.Out, ref pulses);

            case StateScriptNodeRuntimeType.AcknowledgeInteraction:
            case StateScriptNodeRuntimeType.CollectInteraction:
            case StateScriptNodeRuntimeType.StartNpcInteraction:
                if (pulse.InputPortId != StateScriptPortId.In)
                    return false;
                AddManagedCommand(entity, ref managedCommands, new StateScriptManagedCommandElement
                {
                    Type = node.Type switch
                    {
                        StateScriptNodeRuntimeType.AcknowledgeInteraction => StateScriptManagedCommandType.AcknowledgeInteraction,
                        StateScriptNodeRuntimeType.CollectInteraction => StateScriptManagedCommandType.CollectInteraction,
                        _ => StateScriptManagedCommandType.StartNpcInteraction,
                    },
                    GraphIndex = graphIndex,
                    NodeIndex = pulse.NodeIndex,
                });
                return Emit(ref graph, pulse.NodeIndex, StateScriptPortId.Out, ref pulses);

            case StateScriptNodeRuntimeType.QueryUnits:
                if (pulse.InputPortId != StateScriptPortId.In ||
                    !TryQueryUnits(entity, in node, in context, ref graph, component.TickVersion))
                {
                    return true;
                }
                return Emit(ref graph, pulse.NodeIndex, StateScriptPortId.Out, ref pulses);

            case StateScriptNodeRuntimeType.BuildUnitRoster:
                if (pulse.InputPortId != StateScriptPortId.In ||
                    !TryBuildUnitRoster(entity, in node, in context, ref graph, component.TickVersion))
                    return true;
                return Emit(ref graph, pulse.NodeIndex, StateScriptPortId.Out, ref pulses);

            case StateScriptNodeRuntimeType.ExecuteEffect:
                if (pulse.InputPortId != StateScriptPortId.In ||
                    !TryBuildEffectCommand(in node, ref graph, in context, out StateScriptManagedCommandElement effectCommand))
                {
                    return true;
                }
                effectCommand.Type = StateScriptManagedCommandType.ExecuteEffect;
                effectCommand.GraphIndex = graphIndex;
                effectCommand.NodeIndex = pulse.NodeIndex;
                AddManagedCommand(entity, ref managedCommands, effectCommand);
                return Emit(ref graph, pulse.NodeIndex, StateScriptPortId.Out, ref pulses);

            case StateScriptNodeRuntimeType.DestroySelf:
                if (pulse.InputPortId != StateScriptPortId.In)
                    return false;
                AddManagedCommand(entity, ref managedCommands, new StateScriptManagedCommandElement
                {
                    Type = StateScriptManagedCommandType.DestroySelf,
                    GraphIndex = graphIndex,
                    NodeIndex = pulse.NodeIndex,
                });
                return Emit(ref graph, pulse.NodeIndex, StateScriptPortId.Out, ref pulses);

            case StateScriptNodeRuntimeType.Timer:
            case StateScriptNodeRuntimeType.Keep:
            case StateScriptNodeRuntimeType.Monitor:
            case StateScriptNodeRuntimeType.NumberMonitor:
            case StateScriptNodeRuntimeType.Addition:
            case StateScriptNodeRuntimeType.PlayerInputEvent:
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
#if UNITY_EDITOR && STATE_SCRIPT_TRACE
                AddManagedCommand(entity, ref managedCommands, new StateScriptManagedCommandElement
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
                AddManagedCommand(entity, ref managedCommands, new StateScriptManagedCommandElement
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
                    entity,
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
            case StateScriptNodeRuntimeType.PlayerInputEvent:
                if (ProcessInputEventBuffer != 0 &&
                    node.IntParameters.x == (int)PlayerInputOperationType.PrimaryPressed &&
                    node.IntParameters.y != 0 && state.LastPulseTick != component.TickVersion &&
                    PlayerInputs.HasComponent(entity) && PlayerInputs[entity].ContinuousPrimaryHeld != 0)
                {
                    state.LastPulseTick = component.TickVersion;
                    states[stateIndex] = state;
                    if (!EmitAndDrain(entity, graphIndex, nodeIndex, StateScriptPortId.OnInputEvent,
                            ref graph, stateStart, in context, ref component, ref states,
                            ref sourceCommands, ref sourceArguments, ref managedCommands, ref pulses))
                        return false;
                }
                break;

            case StateScriptNodeRuntimeType.Timer:
                state.Time += DeltaTime;
                states[stateIndex] = state;
                if (state.Time >= state.Auxiliary)
                {
#if UNITY_EDITOR && STATE_SCRIPT_TRACE
                    AddManagedCommand(entity, ref managedCommands, new StateScriptManagedCommandElement
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
                        if (node.FloatParameters0.y > 0.5f)
                            break;

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
        Entity entity,
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
                AppendStopAddition(entity, graphIndex, nodeIndex, executionVersion, ref managedCommands);
        }
    }

    private static void AppendStopAddition(
        Entity entity,
        int graphIndex,
        int nodeIndex,
        uint executionVersion,
        ref DynamicBuffer<StateScriptManagedCommandElement> managedCommands)
    {
        AddManagedCommand(entity, ref managedCommands, new StateScriptManagedCommandElement
        {
            Type = StateScriptManagedCommandType.StopAddition,
            GraphIndex = graphIndex,
            NodeIndex = nodeIndex,
            ExecutionVersion = executionVersion,
        });
    }

    private static void AddManagedCommand(
        Entity sourceEntity,
        ref DynamicBuffer<StateScriptManagedCommandElement> managedCommands,
        StateScriptManagedCommandElement command)
    {
        command.SourceEntity = sourceEntity;
        managedCommands.Add(command);
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
#if UNITY_EDITOR && STATE_SCRIPT_TRACE
        , out StateScriptSkillRequestBuildError error
#endif
        )
    {
        command = default;
#if UNITY_EDITOR && STATE_SCRIPT_TRACE
        error = StateScriptSkillRequestBuildError.None;
#endif
        if (node.ExpressionCount != 3)
        {
#if UNITY_EDITOR && STATE_SCRIPT_TRACE
            error = StateScriptSkillRequestBuildError.InvalidExpressionCount;
#endif
            return false;
        }
        if (!TryEvaluateNumber(ref graph, node.ExpressionStart, in context, out float rawSkillId) ||
            !math.isfinite(rawSkillId))
        {
#if UNITY_EDITOR && STATE_SCRIPT_TRACE
            error = StateScriptSkillRequestBuildError.SkillIdEvaluationFailed;
#endif
            return false;
        }
        float rounded = math.round(rawSkillId);
        if (rounded < 0f || rounded > int.MaxValue || math.abs(rawSkillId - rounded) > 0.0001f)
        {
#if UNITY_EDITOR && STATE_SCRIPT_TRACE
            error = StateScriptSkillRequestBuildError.SkillIdNotInteger;
#endif
            return false;
        }
        if (!TryEvaluateValue(ref graph, node.ExpressionStart + 1, in context, out UnitSourceValue positionValue))
        {
#if UNITY_EDITOR && STATE_SCRIPT_TRACE
            error = StateScriptSkillRequestBuildError.PositionEvaluationFailed;
#endif
            return false;
        }
        if (!positionValue.TryGetFloat3(out float3 position))
        {
#if UNITY_EDITOR && STATE_SCRIPT_TRACE
            error = StateScriptSkillRequestBuildError.PositionTypeMismatch;
#endif
            return false;
        }
        if (!TryEvaluateValue(ref graph, node.ExpressionStart + 2, in context, out UnitSourceValue targetValue))
        {
#if UNITY_EDITOR && STATE_SCRIPT_TRACE
            error = StateScriptSkillRequestBuildError.TargetEvaluationFailed;
#endif
            return false;
        }
        if (!targetValue.TryGetEntity(out Entity target))
        {
#if UNITY_EDITOR && STATE_SCRIPT_TRACE
            error = StateScriptSkillRequestBuildError.TargetTypeMismatch;
#endif
            return false;
        }
        command.IntValue = (int)rounded;
        command.Position = position;
        command.TargetEntity = target;
        return true;
    }

    private bool IsNodeEnabled(in StateScriptNodeDefinition node)
    {
        return (node.ExecutionTargets & ExecutionTarget) != 0;
    }

    private bool TryBuildEffectCommand(
        in StateScriptNodeDefinition node,
        ref StateScriptGraphDefinitionBlob graph,
        in UnitSourceContext context,
        out StateScriptManagedCommandElement command)
    {
        command = default;
        if (node.ExpressionCount != 5 ||
            !TryEvaluateValue(ref graph, node.ExpressionStart, in context, out UnitSourceValue targetValue) ||
            !targetValue.TryGetEntity(out Entity target) ||
            !TryEvaluateValue(ref graph, node.ExpressionStart + 1, in context, out UnitSourceValue otherValue) ||
            !otherValue.TryGetEntity(out Entity other) ||
            !TryEvaluateValue(ref graph, node.ExpressionStart + 2, in context, out UnitSourceValue positionValue) ||
            !positionValue.TryGetFloat3(out float3 position) ||
            !TryEvaluateNumber(ref graph, node.ExpressionStart + 3, in context, out float triggerValue) ||
            !TryEvaluateNumber(ref graph, node.ExpressionStart + 4, in context, out float rawSkillId))
        {
            return false;
        }

        float roundedSkillId = math.round(rawSkillId);
        if (roundedSkillId < -1f ||
            roundedSkillId > int.MaxValue ||
            math.abs(rawSkillId - roundedSkillId) > 0.0001f)
        {
            return false;
        }

        Entity origin = (StateScriptEffectOriginSource)node.IntParameters.x switch
        {
            StateScriptEffectOriginSource.Self => context.Self,
            StateScriptEffectOriginSource.Other => context.Other,
            _ => Entity.Null,
        };
        command.IntValue = math.max(1, node.IntParameters.y);
        command.EffectContext = new EffectRequestContext
        {
            TriggerSource = SkillTriggerSource.Script,
            HasOriginEntity = origin != Entity.Null ? (byte)1 : (byte)0,
            OriginEntity = origin,
            HasTargetEntity = target != Entity.Null ? (byte)1 : (byte)0,
            TargetEntity = target,
            HasOtherEntity = other != Entity.Null ? (byte)1 : (byte)0,
            OtherEntity = other,
            HasPosition = 1,
            Position = position,
            TriggerValue = triggerValue,
            SourceSkillId = (int)roundedSkillId,
        };
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

#if UNITY_EDITOR && STATE_SCRIPT_TRACE
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
        AddManagedCommand(context.Self, ref managedCommands, new StateScriptManagedCommandElement
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
    private bool TryQueryUnits(
        Entity entity,
        in StateScriptNodeDefinition node,
        in UnitSourceContext context,
        ref StateScriptGraphDefinitionBlob graph,
        uint tickVersion)
    {
        if ((node.ExpressionCount != 5 && node.ExpressionCount != 7) ||
            !TryEvaluateValue(ref graph, node.ExpressionStart, in context, out UnitSourceValue centerValue) ||
            !centerValue.TryGetFloat3(out float3 center) ||
            !TryEvaluateValue(ref graph, node.ExpressionStart + 1, in context, out UnitSourceValue directionValue) ||
            !directionValue.TryGetFloat2(out float2 direction) ||
            !TryEvaluateValue(ref graph, node.ExpressionStart + 2, in context, out UnitSourceValue sizeValue) ||
            !sizeValue.TryGetFloat2(out float2 size) ||
            !TryEvaluateNumber(ref graph, node.ExpressionStart + 3, in context, out float radius) ||
            !TryEvaluateNumber(ref graph, node.ExpressionStart + 4, in context, out float angle))
        {
            return false;
        }

        UnitQueryShape shape = (UnitQueryShapeType)node.IntParameters.x switch
        {
            UnitQueryShapeType.WholeWorld => new UnitQueryShape { Type = UnitQueryShapeType.WholeWorld },
            UnitQueryShapeType.Circle => UnitQueryShape.Circle(center, radius),
            UnitQueryShapeType.AxisAlignedRect => UnitQueryShape.AxisAlignedRect(center, size),
            UnitQueryShapeType.ForwardRect => UnitQueryShape.ForwardRect(center, direction, size.x, size.y),
            UnitQueryShapeType.Cone => UnitQueryShape.Cone(center, direction, radius, angle),
            _ => default,
        };

        QueryResults.Clear();
        QueryExclusions.Clear();
        if (!TryReadQueryExclusions(entity, node.Key, out int excludedEntityCount))
            return false;
        StateScriptUnitQueryVisitor visitor = new()
        {
            Results = QueryResults,
            ExcludedEntities = QueryExclusions.AsArray(),
            Self = entity,
            UnitDataId = node.IntParameters.z,
            ExcludeSelf = node.FloatParameters0.y > 0.5f ? (byte)1 : (byte)0,
            RequireAvailableInteraction = node.FloatParameters1.x > 0.5f ? (byte)1 : (byte)0,
            QueryCenter = center,
            Sources = Sources,
        };
        QueryTree.Query(
            in shape,
            (UnitFactionMask)node.IntParameters.y,
            ref visitor,
            includeDead: node.FloatParameters0.z <= 0.5f);

        bool selectHighest = (StateScriptUnitQuerySortMode)(int)node.FloatParameters0.x ==
                             StateScriptUnitQuerySortMode.HighestPriorityRandomTie;
        if (node.ExpressionCount == 7)
        {
            var random = Unity.Mathematics.Random.CreateFromIndex(math.hash(new uint3(
                (uint)entity.Index, tickVersion, (uint)node.ExpressionStart)));
            float bestPriority = float.NegativeInfinity;
            int ties = 0;
            UnitQueryHit selected = default;
            for (int i = QueryResults.Length - 1; i >= 0; i--)
            {
                UnitQueryHit hit = QueryResults[i];
                UnitSourceContext candidateContext = new(hit.Entity, entity);
                if (!TryEvaluateCondition(ref graph, node.ExpressionStart + 5, in candidateContext) ||
                    !TryEvaluateNumber(ref graph, node.ExpressionStart + 6, in candidateContext, out float priority) ||
                    !math.isfinite(priority))
                {
                    QueryResults.RemoveAtSwapBack(i);
                    continue;
                }
                if (!selectHighest) continue;
                if (priority > bestPriority)
                {
                    bestPriority = priority;
                    ties = 1;
                    selected = hit;
                }
                else if (priority == bestPriority && random.NextInt(++ties) == 0)
                    selected = hit;
            }
            if (selectHighest && ties > 0)
            {
                QueryResults.Clear();
                QueryResults.Add(selected);
            }
        }
        if (!selectHighest) SortQueryResults(
            (StateScriptUnitQuerySortMode)(int)node.FloatParameters0.x,
            center,
            ref QueryResults);
        int resultCount = node.IntParameters.w > 0
            ? math.min(node.IntParameters.w, QueryResults.Length)
            : QueryResults.Length;
        if (!WriteQueryResults(entity, node.Text, resultCount))
            return false;
        return node.FloatParameters0.w <= 0.5f ||
               AppendQueryResultsToExclusions(entity, node.Key, excludedEntityCount, resultCount);
    }

    private bool TryBuildUnitRoster(Entity entity, in StateScriptNodeDefinition node,
        in UnitSourceContext context, ref StateScriptGraphDefinitionBlob graph, uint tickVersion)
    {
        TryBuildResultKey(node.Text, "count", out FixedString128Bytes countKey);
        int oldCount = ReadRosterInt(entity, countKey);
        // Invalid/empty templates must never leave an old roster available to spawn.
        SetVariable(entity, countKey, UnitSourceValue.FromInt(0));
        TryBuildResultKey(node.Text, "budget", out FixedString128Bytes effectiveBudgetKey);
        SetVariable(entity, effectiveBudgetKey, UnitSourceValue.FromFloat(0f));
        if (node.ExpressionCount != 3 ||
            !TryEvaluateValue(ref graph, node.ExpressionStart, in context, out UnitSourceValue templateValue) ||
            !templateValue.TryGetEntity(out Entity templateEntity) || templateEntity == Entity.Null ||
            !TryEvaluateNumber(ref graph, node.ExpressionStart + 1, in context, out float budget) ||
            !TryEvaluateValue(ref graph, node.ExpressionStart + 2, in context, out UnitSourceValue centerValue) ||
            !centerValue.TryGetFloat3(out float3 center) || !math.all(math.isfinite(center)))
            return false;
        TryBuildResultKey(node.Key, "count", out FixedString128Bytes templateCountKey);
        int choiceCount = ReadRosterInt(templateEntity, templateCountKey);
        if (choiceCount <= 0) return false;
        using NativeList<UnitRosterUtility.Choice> choices = new(choiceCount, Allocator.Temp);
        NativeList<int> roster = new(Allocator.Temp);
        try
        {
            for (int i = 0; i < choiceCount; i++)
            {
                FixedString128Bytes unitKey = RosterEntryKey(node.Key, i, "unit");
                UnitSourceArguments args = default;
                args.Values.Add(UnitSourceValue.FromString(in unitKey));
                if (!Sources.TryGet(templateEntity, UnitSourceId.UnitVariablesGetString, in args, out UnitSourceValue unitValue) ||
                    !unitValue.TryGetString(out FixedString128Bytes unit))
                    return false;
                choices.Add(new UnitRosterUtility.Choice
                {
                    Unit = unit,
                    MinCount = ReadRosterInt(templateEntity, RosterEntryKey(node.Key, i, "minCount")),
                    Cost = ReadRosterInt(templateEntity, RosterEntryKey(node.Key, i, "cost")),
                    Weight = ReadRosterInt(templateEntity, RosterEntryKey(node.Key, i, "weight")),
                });
            }
            var random = Unity.Mathematics.Random.CreateFromIndex(math.hash(new uint4(
                (uint)entity.Index, (uint)entity.Version, tickVersion, (uint)node.ExpressionStart)));
            float budgetMultiplier = Difficulties.TryGetComponent(entity, out DungeonDifficultyComponent difficulty)
                ? difficulty.BudgetMultiplier : 1f;
            if (!UnitRosterUtility.Build(choices.AsArray(), budget, budgetMultiplier, ref random, ref roster))
                return false;
            SetVariable(entity, effectiveBudgetKey, UnitSourceValue.FromFloat(budget * budgetMultiplier));
            for (int i = 0; i < roster.Length; i++)
            {
                float2 offset = random.NextFloat2Direction() * math.sqrt(random.NextFloat()) * node.FloatParameters0.x;
                if (!SetVariable(entity, RosterEntryKey(node.Text, i, "unit"), UnitSourceValue.FromString(choices[roster[i]].Unit)) ||
                    !SetVariable(entity, RosterEntryKey(node.Text, i, "position"), UnitSourceValue.FromFloat3(center + new float3(offset, 0))))
                    return false;
                SetVariable(entity, RosterEntryKey(node.Text, i, "hasMonsterData"), UnitSourceValue.FromBool(false));
            }
            for (int i = roster.Length; i < oldCount; i++)
            {
                RemoveRosterVariable(entity, RosterEntryKey(node.Text, i, "unit"));
                RemoveRosterVariable(entity, RosterEntryKey(node.Text, i, "position"));
                RemoveRosterVariable(entity, RosterEntryKey(node.Text, i, "hasMonsterData"));
            }
            return SetVariable(entity, countKey, UnitSourceValue.FromInt(roster.Length));
        }
        finally { roster.Dispose(); }
    }

    private int ReadRosterInt(Entity entity, in FixedString128Bytes key)
    {
        UnitSourceArguments args = default;
        args.Values.Add(UnitSourceValue.FromString(in key));
        return Sources.TryGet(entity, UnitSourceId.UnitVariablesGetNumber, in args, out UnitSourceValue value) &&
               value.TryGetInt(out int number) ? number : 0;
    }

    private void RemoveRosterVariable(Entity entity, in FixedString128Bytes key)
    {
        UnitSourceArguments args = default;
        args.Values.Add(UnitSourceValue.FromString(in key));
        Sources.TrySet(entity, UnitSourceId.UnitVariablesRemove, in args);
    }

    private static FixedString128Bytes RosterEntryKey(in FixedString128Bytes prefix, int index, in FixedString32Bytes suffix)
    {
        TryBuildResultKey(prefix, index, out FixedString128Bytes entry);
        TryBuildResultKey(entry, suffix, out FixedString128Bytes key);
        return key;
    }

    private bool TryReadQueryExclusions(
        Entity entity,
        in FixedString128Bytes exclusionKey,
        out int storedCount)
    {
        storedCount = 0;
        if (exclusionKey.Length == 0)
            return true;
        if (!TryBuildResultKey(exclusionKey, "count", out FixedString128Bytes countKey))
            return false;

        UnitSourceArguments countArguments = default;
        countArguments.Values.Add(UnitSourceValue.FromString(in countKey));
        if (!Sources.TryGet(
                entity,
                UnitSourceId.UnitVariablesGetNumber,
                in countArguments,
                out UnitSourceValue countValue))
        {
            return true;
        }
        if (!countValue.TryGetInt(out storedCount) || storedCount < 0)
            return false;

        for (int index = 0; index < storedCount; index++)
        {
            if (!TryBuildResultKey(exclusionKey, index, out FixedString128Bytes entryKey))
                return false;
            UnitSourceArguments entryArguments = default;
            entryArguments.Values.Add(UnitSourceValue.FromString(in entryKey));
            if (Sources.TryGet(
                    entity,
                    UnitSourceId.UnitVariablesGetEntity,
                    in entryArguments,
                    out UnitSourceValue entryValue) &&
                entryValue.TryGetEntity(out Entity excludedEntity) &&
                excludedEntity != Entity.Null)
            {
                QueryExclusions.Add(excludedEntity);
            }
        }
        return true;
    }

    private bool AppendQueryResultsToExclusions(
        Entity entity,
        in FixedString128Bytes exclusionKey,
        int storedCount,
        int resultCount)
    {
        if (exclusionKey.Length == 0)
            return false;
        for (int index = 0; index < resultCount; index++)
        {
            if (!TryBuildResultKey(exclusionKey, storedCount + index, out FixedString128Bytes entryKey) ||
                !SetVariable(entity, entryKey, UnitSourceValue.FromEntity(QueryResults[index].Entity)))
            {
                return false;
            }
        }
        if (!TryBuildResultKey(exclusionKey, "count", out FixedString128Bytes countKey))
            return false;
        return SetVariable(entity, countKey, UnitSourceValue.FromInt(storedCount + resultCount));
    }

    private bool WriteQueryResults(Entity entity, in FixedString128Bytes resultKey, int resultCount)
    {
        if (!TryBuildResultKey(resultKey, "count", out FixedString128Bytes countKey))
            return false;

        int oldCount = 0;
        UnitSourceArguments getCountArguments = default;
        getCountArguments.Values.Add(UnitSourceValue.FromString(in countKey));
        if (Sources.TryGet(
                entity,
                UnitSourceId.UnitVariablesGetNumber,
                in getCountArguments,
                out UnitSourceValue oldCountValue))
        {
            oldCountValue.TryGetInt(out oldCount);
        }

        for (int index = resultCount; index < oldCount; index++)
        {
            if (!TryBuildResultKey(resultKey, index, out FixedString128Bytes staleKey))
                return false;
            UnitSourceArguments removeArguments = default;
            removeArguments.Values.Add(UnitSourceValue.FromString(in staleKey));
            Sources.TrySet(entity, UnitSourceId.UnitVariablesRemove, in removeArguments);
        }

        for (int index = 0; index < resultCount; index++)
        {
            if (!TryBuildResultKey(resultKey, index, out FixedString128Bytes entryKey) ||
                !SetVariable(entity, entryKey, UnitSourceValue.FromEntity(QueryResults[index].Entity)))
            {
                return false;
            }
        }
        return SetVariable(entity, countKey, UnitSourceValue.FromInt(resultCount));
    }

    private bool SetVariable(
        Entity entity,
        in FixedString128Bytes key,
        in UnitSourceValue value)
    {
        UnitSourceArguments arguments = default;
        arguments.Key = key;
        arguments.HasKey = 1;
        arguments.Values.Add(value);
        return Sources.TrySet(entity, UnitSourceId.UnitVariablesSet, in arguments);
    }

    private static bool TryBuildResultKey(
        in FixedString128Bytes prefix,
        int index,
        out FixedString128Bytes key)
    {
        key = prefix;
        return key.Append('.') == FormatError.None &&
               key.Append(index) == FormatError.None;
    }

    private static bool TryBuildResultKey(
        in FixedString128Bytes prefix,
        in FixedString32Bytes suffix,
        out FixedString128Bytes key)
    {
        key = prefix;
        return key.Append('.') == FormatError.None &&
               key.Append(suffix) == FormatError.None;
    }

    private static void SortQueryResults(
        StateScriptUnitQuerySortMode sortMode,
        float3 center,
        ref NativeList<UnitQueryHit> results)
    {
        if (sortMode == StateScriptUnitQuerySortMode.None)
            return;

        for (int index = 1; index < results.Length; index++)
        {
            UnitQueryHit current = results[index];
            float currentDistance = math.lengthsq(current.Position.xy - center.xy);
            int insertIndex = index;
            while (insertIndex > 0)
            {
                UnitQueryHit previous = results[insertIndex - 1];
                float previousDistance = math.lengthsq(previous.Position.xy - center.xy);
                int comparison = currentDistance.CompareTo(previousDistance);
                if (sortMode == StateScriptUnitQuerySortMode.DistanceDescending)
                    comparison = -comparison;
                if (comparison == 0)
                {
                    comparison = current.Entity.Index.CompareTo(previous.Entity.Index);
                    if (comparison == 0)
                        comparison = current.Entity.Version.CompareTo(previous.Entity.Version);
                }
                if (comparison >= 0)
                    break;

                results[insertIndex] = previous;
                insertIndex--;
            }
            results[insertIndex] = current;
        }
    }

    private struct StateScriptUnitQueryVisitor : IUnitQueryVisitor
    {
        public NativeList<UnitQueryHit> Results;

        [ReadOnly]
        public NativeArray<Entity> ExcludedEntities;

        public Entity Self;
        public int UnitDataId;
        public byte ExcludeSelf;
        public byte RequireAvailableInteraction;
        public float3 QueryCenter;
        public UnitSourceDispatcher Sources;

        public bool Visit(in UnitQueryEntry entry)
        {
            if (ExcludeSelf != 0 && entry.Entity == Self ||
                UnitDataId >= 0 && entry.UnitDataId != UnitDataId ||
                IsExcluded(entry.Entity))
            {
                return true;
            }

            if (RequireAvailableInteraction != 0)
            {
                UnitSourceArguments arguments = default;
                if (!Sources.TryGet(
                        entry.Entity,
                        UnitSourceId.UnitInteractableEnabled,
                        in arguments,
                        out UnitSourceValue enabledValue) ||
                    !enabledValue.TryGetBool(out bool enabled) || !enabled)
                {
                    return true;
                }

                if (Sources.TryGet(
                        entry.Entity,
                        UnitSourceId.UnitInteractableRangeSq,
                        in arguments,
                        out UnitSourceValue rangeValue) &&
                    rangeValue.TryGetNumber(out float rangeSq) && rangeSq > 0f &&
                    math.lengthsq((entry.Position - QueryCenter).xy) > rangeSq)
                {
                    return true;
                }
            }

            Results.Add(new UnitQueryHit
            {
                Entity = entry.Entity,
                Position = entry.Position,
                Faction = entry.Faction,
            });
            return true;
        }

        private bool IsExcluded(Entity entity)
        {
            for (int index = 0; index < ExcludedEntities.Length; index++)
            {
                if (ExcludedEntities[index] == entity)
                    return true;
            }
            return false;
        }
    }

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
[WithNone(typeof(UnitInitializationPendingTag))]
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
