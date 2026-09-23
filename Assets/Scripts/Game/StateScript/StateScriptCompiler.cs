using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public static class StateScriptCompiler
{
    private sealed class CompiledUnit
    {
        public int UnitDataId;
        public readonly List<CompiledGraph> Graphs = new();
    }

    private sealed class CompiledGraph
    {
        public int EntryNodeIndex;
        public int ExecutionConditionExpressionIndex = -1;
        public FixedString128Bytes Guid;
        public FixedString128Bytes Name;
        public readonly List<StateScriptNodeDefinition> Nodes = new();
        public readonly List<StateScriptOutputRoute> Routes = new();
        public readonly List<StateScriptPulseTarget> Targets = new();
        public readonly List<int> StateNodeIndices = new();
        public readonly List<CompiledExpression> Expressions = new();
        public readonly List<FixedString128Bytes> Strings = new();
    }

    private struct CompiledExpression
    {
        public ExpressionProgram Program;
        public UnitValueCategory Category;
        public byte IsCondition;
    }

    public static bool TryBuildRegistry(
        IReadOnlyList<StateScriptData> sourceRows,
        out BlobAssetReference<StateScriptRuntimeRegistryBlob> registry,
        out string error)
    {
        registry = default;
        error = string.Empty;
        if (sourceRows == null)
        {
            error = "State script table is unavailable.";
            return false;
        }

        List<StateScriptData> orderedRows = new(sourceRows.Count);
        for (int index = 0; index < sourceRows.Count; index++)
        {
            if (sourceRows[index] != null)
                orderedRows.Add(sourceRows[index]);
        }
        orderedRows.Sort(static (left, right) => left.Id.CompareTo(right.Id));
        for (int index = 1; index < orderedRows.Count; index++)
        {
            if (orderedRows[index - 1].Id == orderedRows[index].Id)
            {
                error = $"Duplicate state script UnitDataId '{orderedRows[index].Id}'.";
                return false;
            }
        }

        List<CompiledUnit> units = new(orderedRows.Count);
        for (int index = 0; index < orderedRows.Count; index++)
        {
            if (!TryCompileUnit(orderedRows[index], out CompiledUnit unit, out error))
            {
                error = $"State script UnitDataId '{orderedRows[index].Id}' failed to compile: {error}";
                return false;
            }
            units.Add(unit);
        }

        BlobBuilder builder = new(Allocator.Temp);
        ref StateScriptRuntimeRegistryBlob root = ref builder.ConstructRoot<StateScriptRuntimeRegistryBlob>();
        BlobBuilderArray<StateScriptUnitDefinitionBlob> targetUnits =
            builder.Allocate(ref root.Units, units.Count);
        for (int unitIndex = 0; unitIndex < units.Count; unitIndex++)
            WriteUnit(ref builder, ref targetUnits[unitIndex], units[unitIndex]);

        registry = builder.CreateBlobAssetReference<StateScriptRuntimeRegistryBlob>(Allocator.Persistent);
        builder.Dispose();
        return true;
    }

    public static int FindUnitIndex(
        in BlobAssetReference<StateScriptRuntimeRegistryBlob> registry,
        int unitDataId)
    {
        if (!registry.IsCreated)
            return -1;

        ref BlobArray<StateScriptUnitDefinitionBlob> units = ref registry.Value.Units;
        int low = 0;
        int high = units.Length - 1;
        while (low <= high)
        {
            int middle = low + ((high - low) >> 1);
            int candidate = units[middle].UnitDataId;
            if (candidate == unitDataId)
                return middle;
            if (candidate < unitDataId)
                low = middle + 1;
            else
                high = middle - 1;
        }
        return -1;
    }

    private static bool TryCompileUnit(
        StateScriptData source,
        out CompiledUnit unit,
        out string error)
    {
        source.EnsureValid();
        unit = new CompiledUnit { UnitDataId = source.Id };
        error = string.Empty;
        for (int graphIndex = 0; graphIndex < source.Graphs.Count; graphIndex++)
        {
            StateScriptInstanceData graph = source.Graphs[graphIndex];
            if (!TryCompileGraph(graph, out CompiledGraph compiled, out error))
            {
                string graphName = graph?.Name ?? graphIndex.ToString();
                error = $"Graph '{graphName}': {error}";
                unit = null;
                return false;
            }
            unit.Graphs.Add(compiled);
        }
        return true;
    }

    private static bool TryCompileGraph(
        StateScriptInstanceData source,
        out CompiledGraph graph,
        out string error)
    {
        graph = null;
        error = string.Empty;
        if (source == null || source.Nodes == null || source.Nodes.Count == 0)
        {
            error = "Graph has no nodes.";
            return false;
        }

        Dictionary<string, int> nodeIndices = new(StringComparer.Ordinal);
        for (int index = 0; index < source.Nodes.Count; index++)
        {
            StateScriptNodeData node = source.Nodes[index];
            if (node == null || string.IsNullOrWhiteSpace(node.Guid))
            {
                error = $"Node {index} has no Guid.";
                return false;
            }
            if (!nodeIndices.TryAdd(node.Guid, index))
            {
                error = $"Duplicate node Guid '{node.Guid}'.";
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(source.EntryNodeGuid) ||
            !nodeIndices.TryGetValue(source.EntryNodeGuid, out int entryNodeIndex) ||
            source.Nodes[entryNodeIndex] is not StateScriptEntryNodeData)
        {
            error = "Entry node is missing or invalid.";
            return false;
        }

        graph = new CompiledGraph { EntryNodeIndex = entryNodeIndex };
        if (!TryCopyFixedString(source.Guid, out graph.Guid) ||
            !TryCopyFixedString(source.Name, out graph.Name))
        {
            error = "Graph Guid or name is too long.";
            return false;
        }

        UnitSourceResolver schemaResolver = new(Entity.Null);
        ComparatorFactory expressionFactory = CreateExpressionFactory();
        if (source.ExecutionConditions != null && source.ExecutionConditions.Count > 0)
        {
            graph.ExecutionConditionExpressionIndex = graph.Expressions.Count;
            if (!TryAddConditions(
                    source.ExecutionConditions,
                    schemaResolver,
                    expressionFactory,
                    graph,
                    out error))
            {
                error = $"Execution conditions: {error}";
                return false;
            }
        }

        for (int index = 0; index < source.Nodes.Count; index++)
        {
            if (!TryCompileNode(
                    source.Nodes[index],
                    schemaResolver,
                    expressionFactory,
                    graph,
                    out StateScriptNodeDefinition definition,
                    out error))
            {
                error = $"Node '{source.Nodes[index].Guid}' ({source.Nodes[index].Type}): {error}";
                return false;
            }
            graph.Nodes.Add(definition);
        }

        Dictionary<(int NodeIndex, byte PortId), List<StateScriptPulseTarget>> groupedRoutes = new();
        List<List<int>> adjacency = new(source.Nodes.Count);
        for (int index = 0; index < source.Nodes.Count; index++)
            adjacency.Add(new List<int>());
        for (int edgeIndex = 0; edgeIndex < source.Edges.Count; edgeIndex++)
        {
            StateScriptEdgeData edge = source.Edges[edgeIndex];
            if (edge == null ||
                !nodeIndices.TryGetValue(edge.OutputNodeGuid ?? string.Empty, out int outputNodeIndex) ||
                !nodeIndices.TryGetValue(edge.InputNodeGuid ?? string.Empty, out int inputNodeIndex) ||
                !TryGetOutputPort(graph.Nodes[outputNodeIndex].Type, edge.OutputPortName, out byte outputPortId) ||
                !TryGetInputPort(graph.Nodes[inputNodeIndex].Type, edge.InputPortName, out byte inputPortId))
            {
                error = $"Edge {edgeIndex} is invalid.";
                return false;
            }

            (int, byte) key = (outputNodeIndex, outputPortId);
            if (!groupedRoutes.TryGetValue(key, out List<StateScriptPulseTarget> targets))
            {
                targets = new List<StateScriptPulseTarget>();
                groupedRoutes.Add(key, targets);
            }
            targets.Add(new StateScriptPulseTarget { NodeIndex = inputNodeIndex, InputPortId = inputPortId });
            adjacency[outputNodeIndex].Add(inputNodeIndex);
        }

        if (!ValidateReachability(entryNodeIndex, adjacency, out int unreachableIndex))
        {
            error = $"Node '{source.Nodes[unreachableIndex].Guid}' is unreachable from Entry.";
            return false;
        }

        for (int nodeIndex = 0; nodeIndex < graph.Nodes.Count; nodeIndex++)
        {
            StateScriptNodeDefinition definition = graph.Nodes[nodeIndex];
            definition.OutputRouteStart = graph.Routes.Count;
            for (byte portId = 0; portId <= MaxOutputPort(definition.Type); portId++)
            {
                if (!groupedRoutes.TryGetValue((nodeIndex, portId), out List<StateScriptPulseTarget> targets))
                    continue;
                if (targets.Count > ushort.MaxValue)
                {
                    error = $"Node '{definition.Guid}' output has too many targets.";
                    return false;
                }
                graph.Routes.Add(new StateScriptOutputRoute
                {
                    OutputPortId = portId,
                    TargetStart = graph.Targets.Count,
                    TargetCount = (ushort)targets.Count,
                });
                graph.Targets.AddRange(targets);
            }
            definition.OutputRouteCount = (ushort)(graph.Routes.Count - definition.OutputRouteStart);
            graph.Nodes[nodeIndex] = definition;
        }

        List<int> states = new();
        for (int nodeIndex = 0; nodeIndex < graph.Nodes.Count; nodeIndex++)
        {
            if (IsStateNode(graph.Nodes[nodeIndex].Type))
                states.Add(nodeIndex);
        }
        List<StateScriptNodeDefinition> compiledNodes = graph.Nodes;
        states.Sort((left, right) =>
        {
            int order = compiledNodes[left].TickOrder.CompareTo(compiledNodes[right].TickOrder);
            return order != 0 ? order : left.CompareTo(right);
        });
        graph.StateNodeIndices.AddRange(states);
        return true;
    }

    private static bool TryCompileNode(
        StateScriptNodeData source,
        UnitSourceResolver schemaResolver,
        ComparatorFactory expressionFactory,
        CompiledGraph graph,
        out StateScriptNodeDefinition definition,
        out string error)
    {
        definition = default;
        error = string.Empty;
        if (!TryResolveNodeType(source, out StateScriptNodeRuntimeType type))
        {
            error = $"Unsupported node type '{source?.Type}'.";
            return false;
        }
        definition.Type = type;
        definition.ExpressionStart = graph.Expressions.Count;
        definition.StringStart = graph.Strings.Count;
        if (!TryCopyFixedString(source.Guid, out definition.Guid))
        {
            error = "Guid is too long.";
            return false;
        }
        if (source is StateStateScriptNodeData stateData)
            definition.TickOrder = stateData.TickOrder;

        switch (source)
        {
            case CompareStateScriptNodeData compare:
                if (!TryAddConditions(new[] { compare.Condition }, schemaResolver, expressionFactory, graph, out error))
                    return false;
                break;
            case SetValueStateScriptNodeData setValue:
                if (!TryCompileSet(setValue, schemaResolver, expressionFactory, graph, ref definition, out error))
                    return false;
                break;
            case RequestSkillActionNodeData requestSkill:
                if (!TryCompileSkillRequest(requestSkill.SkillId, requestSkill.Input, schemaResolver, expressionFactory, graph, out error))
                    return false;
                break;
            case RequestSkillWithAdditionActionNodeData requestSkillWithAddition:
                if (!TryCompileSkillRequest(requestSkillWithAddition.SkillId, requestSkillWithAddition.Input, schemaResolver, expressionFactory, graph, out error))
                    return false;
                break;
            case PublishGameEventStateScriptNodeData publish:
                if (string.IsNullOrWhiteSpace(publish.EventName) || !TryCopyFixedString(publish.EventName.Trim(), out definition.Text))
                {
                    error = "EventName is empty or too long.";
                    return false;
                }
                if (!TryAddValueExpression(publish.Reference, UnitValueCategory.Any, schemaResolver, expressionFactory, graph, out error))
                    return false;
                break;
            case RequestInteractionActionNodeData interaction:
                interaction.Target ??= RequestInteractionActionNodeData.CreateDefaultTargetExpression();
                if (!TryAddValueExpression(interaction.Target, UnitValueCategory.Entity, schemaResolver, expressionFactory, graph, out error))
                    return false;
                break;
            case CompleteInteractionActionNodeData completeInteraction:
                completeInteraction.Result ??= CompleteInteractionActionNodeData.CreateDefaultResultExpression();
                definition.IntParameters.x = (int)completeInteraction.ResultCode;
                if (!TryAddValueExpression(completeInteraction.Result, UnitValueCategory.Any, schemaResolver, expressionFactory, graph, out error))
                    return false;
                break;
            case AcknowledgeInteractionActionNodeData:
            case CollectInteractionActionNodeData:
            case StartNpcInteractionActionNodeData:
                break;
            case SpawnUnitActionNodeData spawn:
                if (!TryCompileSpawn(spawn, graph, ref definition, out error))
                    return false;
                break;
            case QueryUnitsActionNodeData query:
                if (!TryCompileQueryUnits(query, schemaResolver, expressionFactory, graph, ref definition, out error))
                    return false;
                break;
            case ExecuteEffectActionNodeData executeEffect:
                if (!TryCompileExecuteEffect(
                        executeEffect,
                        schemaResolver,
                        expressionFactory,
                        graph,
                        ref definition,
                        out error))
                {
                    return false;
                }
                break;
            case DestroySelfActionNodeData:
                break;
            case TimerStateScriptNodeData timer:
                if (!TryAddValueExpression(timer.Duration, UnitValueCategory.Number, schemaResolver, expressionFactory, graph, out error))
                    return false;
                break;
            case KeepStateScriptNodeData keep:
                definition.FloatParameters0.x = math.max(0f, keep.DurationSeconds);
                break;
            case MonitorStateScriptNodeData monitor:
                if (!TryAddConditions(new[] { monitor.Condition }, schemaResolver, expressionFactory, graph, out error))
                    return false;
                break;
            case NumberMonitorStateScriptNodeData numberMonitor:
                if (!TryAddValueExpression(numberMonitor.Value, UnitValueCategory.Number, schemaResolver, expressionFactory, graph, out error))
                    return false;
                break;
            case AdditionStateScriptNodeData addition:
                if (string.IsNullOrWhiteSpace(addition.EventName) || !TryCopyFixedString(addition.EventName.Trim(), out definition.Text))
                {
                    error = "EventName is empty or too long.";
                    return false;
                }
                break;
        }

        int expressionCount = graph.Expressions.Count - definition.ExpressionStart;
        int stringCount = graph.Strings.Count - definition.StringStart;
        if (expressionCount > byte.MaxValue || stringCount > ushort.MaxValue)
        {
            error = "Node contains too many expressions or strings.";
            return false;
        }
        definition.ExpressionCount = (byte)expressionCount;
        definition.StringCount = (ushort)stringCount;
        return true;
    }

    private static bool TryCompileSet(
        SetValueStateScriptNodeData source,
        UnitSourceResolver schemaResolver,
        ComparatorFactory expressionFactory,
        CompiledGraph graph,
        ref StateScriptNodeDefinition definition,
        out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(source.SetterKey) ||
            !UnitComponentSourceRegistry.TryGetSet(source.SetterKey, out UnitSourceId sourceId, out UnitSourceSetSchemaEntry schema))
        {
            error = $"Set source '{source.SetterKey}' is unavailable.";
            return false;
        }
        List<ValueExpression> values = source.GetOrCreateValues(schema.Parameters.Count);
        if (values.Count != schema.Parameters.Count)
        {
            error = $"Set source requires {schema.Parameters.Count} value(s).";
            return false;
        }
        if (schema.RequiresKey && (string.IsNullOrWhiteSpace(source.Key) || !TryCopyFixedString(source.Key, out definition.Key)))
        {
            error = "Set key is empty or too long.";
            return false;
        }
        definition.SetSourceId = sourceId;
        definition.SetSourceTarget = source.SourceTarget;
        definition.IntParameters.x = schema.RequiresKey ? 1 : 0;
        for (int index = 0; index < values.Count; index++)
        {
            if (!TryAddValueExpression(values[index], schema.Parameters[index].Category, schemaResolver, expressionFactory, graph, out error))
                return false;
        }
        return true;
    }

    private static bool TryCompileSkillRequest(
        ValueExpression skillId,
        SkillRequestInputData input,
        UnitSourceResolver schemaResolver,
        ComparatorFactory expressionFactory,
        CompiledGraph graph,
        out string error)
    {
        input ??= SkillRequestInputData.CreateDefault();
        input.EnsureValid();
        return TryAddValueExpression(skillId, UnitValueCategory.Number, schemaResolver, expressionFactory, graph, out error) &&
               TryAddValueExpression(input.Position, UnitValueCategory.Float3, schemaResolver, expressionFactory, graph, out error) &&
               TryAddValueExpression(input.TargetEntity, UnitValueCategory.Entity, schemaResolver, expressionFactory, graph, out error);
    }

    private static bool TryCompileSpawn(
        SpawnUnitActionNodeData source,
        CompiledGraph graph,
        ref StateScriptNodeDefinition definition,
        out string error)
    {
        error = string.Empty;
        definition.IntParameters = new int4(
            math.max(1, source.Count),
            source.CopyFactionFromSpawner ? 1 : 0,
            source.ShareVariablesWithSpawner ? 1 : 0,
            source.RestoreRuntimeState ? 1 : 0);
        definition.FloatParameters0 = new float4(
            math.max(0f, source.SpawnRadius),
            math.clamp(source.MinSpawnRadius, 0f, math.max(0f, source.SpawnRadius)),
            0f,
            0f);
        definition.FloatParameters1 = new float4(
            source.CenterOffset.x,
            source.CenterOffset.y,
            source.CenterOffset.z,
            0f);
        if (!string.IsNullOrWhiteSpace(source.VariableListKey))
        {
            if (!TryCopyFixedString(source.VariableListKey.Trim(), out definition.Text))
            {
                error = "VariableListKey is too long.";
                return false;
            }
            return true;
        }

        if (source.CandidateUnitNames != null)
        {
            for (int index = 0; index < source.CandidateUnitNames.Length; index++)
            {
                string candidate = source.CandidateUnitNames[index];
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;
                if (!TryCopyFixedString(candidate.Trim(), out FixedString128Bytes fixedCandidate))
                {
                    error = "Candidate unit name is too long.";
                    return false;
                }
                graph.Strings.Add(fixedCandidate);
            }
        }
        if (graph.Strings.Count == definition.StringStart && !string.IsNullOrWhiteSpace(source.UnitName))
        {
            if (!TryCopyFixedString(source.UnitName.Trim(), out FixedString128Bytes fixedName))
            {
                error = "UnitName is too long.";
                return false;
            }
            graph.Strings.Add(fixedName);
        }
        if (graph.Strings.Count == definition.StringStart)
        {
            error = "SpawnUnit requires VariableListKey or at least one unit name.";
            return false;
        }
        return true;
    }

    private static bool TryCompileQueryUnits(
        QueryUnitsActionNodeData source,
        UnitSourceResolver schemaResolver,
        ComparatorFactory expressionFactory,
        CompiledGraph graph,
        ref StateScriptNodeDefinition definition,
        out string error)
    {
        source.EnsureValid();
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(source.ResultKey) ||
            !TryCopyFixedString(source.ResultKey.Trim(), out definition.Text))
        {
            error = "ResultKey is empty or too long.";
            return false;
        }
        if (definition.Text.Length > 112)
        {
            error = "ResultKey is too long for indexed result entries.";
            return false;
        }

        definition.IntParameters = new int4(
            (int)source.Shape,
            (int)source.FactionMask,
            source.UnitDataId,
            math.max(0, source.MaxCount));
        definition.FloatParameters0 = new float4(
            (int)source.SortMode,
            source.ExcludeSelf ? 1f : 0f,
            source.ExcludeDead ? 1f : 0f,
            source.RememberResultsInExcludedEntities ? 1f : 0f);
        definition.FloatParameters1.x = source.RequireAvailableInteraction ? 1f : 0f;
        if (!string.IsNullOrWhiteSpace(source.ExcludedEntitiesKey))
        {
            string excludedEntitiesKey = source.ExcludedEntitiesKey.Trim();
            if (!TryCopyFixedString(excludedEntitiesKey, out definition.Key) || definition.Key.Length > 112)
            {
                error = "ExcludedEntitiesKey is too long for indexed entries.";
                return false;
            }
            if (source.RememberResultsInExcludedEntities &&
                string.Equals(excludedEntitiesKey, source.ResultKey.Trim(), StringComparison.Ordinal))
            {
                error = "ResultKey and ExcludedEntitiesKey must differ when results are remembered.";
                return false;
            }
        }
        else if (source.RememberResultsInExcludedEntities)
        {
            error = "RememberResultsInExcludedEntities requires ExcludedEntitiesKey.";
            return false;
        }
        return TryAddValueExpression(source.Center, UnitValueCategory.Float3, schemaResolver, expressionFactory, graph, out error) &&
               TryAddValueExpression(source.Direction, UnitValueCategory.Float2, schemaResolver, expressionFactory, graph, out error) &&
               TryAddValueExpression(source.Size, UnitValueCategory.Float2, schemaResolver, expressionFactory, graph, out error) &&
               TryAddValueExpression(source.Radius, UnitValueCategory.Number, schemaResolver, expressionFactory, graph, out error) &&
               TryAddValueExpression(source.Angle, UnitValueCategory.Number, schemaResolver, expressionFactory, graph, out error);
    }

    private static bool TryCompileExecuteEffect(
        ExecuteEffectActionNodeData source,
        UnitSourceResolver schemaResolver,
        ComparatorFactory expressionFactory,
        CompiledGraph graph,
        ref StateScriptNodeDefinition definition,
        out string error)
    {
        source.EnsureValid();
        definition.IntParameters.x = (int)source.OriginSource;
        definition.IntParameters.y = math.max(1, source.RepeatCount);
        return TryAddValueExpression(
                   source.TargetEntity,
                   UnitValueCategory.Entity,
                   schemaResolver,
                   expressionFactory,
                   graph,
                   out error) &&
               TryAddValueExpression(
                   source.OtherEntity,
                   UnitValueCategory.Entity,
                   schemaResolver,
                   expressionFactory,
                   graph,
                   out error) &&
               TryAddValueExpression(
                   source.Position,
                   UnitValueCategory.Float3,
                   schemaResolver,
                   expressionFactory,
                   graph,
                   out error) &&
               TryAddValueExpression(
                   source.TriggerValue,
                   UnitValueCategory.Number,
                   schemaResolver,
                   expressionFactory,
                   graph,
                   out error) &&
               TryAddValueExpression(
                   source.SourceSkillId,
                   UnitValueCategory.Number,
                   schemaResolver,
                   expressionFactory,
                   graph,
                   out error);
    }

    private static bool TryAddConditions(
        IReadOnlyList<ConditionConfig> conditions,
        UnitSourceResolver schemaResolver,
        ComparatorFactory expressionFactory,
        CompiledGraph graph,
        out string error)
    {
        Comparator comparator = expressionFactory.BuildComparator(conditions, schemaResolver);
        if (!comparator.IsValid)
        {
            error = "Condition expression is invalid.";
            return false;
        }
        graph.Expressions.Add(new CompiledExpression
        {
            Program = comparator.Program,
            Category = UnitValueCategory.Bool,
            IsCondition = 1,
        });
        error = string.Empty;
        return true;
    }

    private static bool TryAddValueExpression(
        ValueExpression source,
        UnitValueCategory expectedCategory,
        UnitSourceResolver schemaResolver,
        ComparatorFactory expressionFactory,
        CompiledGraph graph,
        out string error)
    {
        if (!expressionFactory.TryBuildValueExpression(source, schemaResolver, out CompiledValueExpression expression, out error))
            return false;
        if (expectedCategory != UnitValueCategory.Any && expression.Category != expectedCategory)
        {
            error = $"Expected {expectedCategory}, received {expression.Category}.";
            return false;
        }
        graph.Expressions.Add(new CompiledExpression
        {
            Program = expression.Program,
            Category = expression.Category,
        });
        return true;
    }

    private static bool ValidateReachability(int entryNodeIndex, List<List<int>> adjacency, out int unreachableIndex)
    {
        bool[] visited = new bool[adjacency.Count];
        Queue<int> pending = new();
        visited[entryNodeIndex] = true;
        pending.Enqueue(entryNodeIndex);
        while (pending.Count > 0)
        {
            int current = pending.Dequeue();
            for (int index = 0; index < adjacency[current].Count; index++)
            {
                int next = adjacency[current][index];
                if (visited[next])
                    continue;
                visited[next] = true;
                pending.Enqueue(next);
            }
        }
        for (int index = 0; index < visited.Length; index++)
        {
            if (!visited[index])
            {
                unreachableIndex = index;
                return false;
            }
        }
        unreachableIndex = -1;
        return true;
    }

    private static bool TryResolveNodeType(StateScriptNodeData source, out StateScriptNodeRuntimeType type)
    {
        type = source switch
        {
            StateScriptEntryNodeData => StateScriptNodeRuntimeType.Entry,
            CompareStateScriptNodeData => StateScriptNodeRuntimeType.Compare,
            SetValueStateScriptNodeData => StateScriptNodeRuntimeType.SetValue,
            RequestSkillActionNodeData => StateScriptNodeRuntimeType.RequestSkill,
            PublishGameEventStateScriptNodeData => StateScriptNodeRuntimeType.PublishGameEvent,
            RequestSkillWithAdditionActionNodeData => StateScriptNodeRuntimeType.RequestSkillWithAddition,
            RequestInteractionActionNodeData => StateScriptNodeRuntimeType.RequestInteraction,
            SpawnUnitActionNodeData => StateScriptNodeRuntimeType.SpawnUnit,
            QueryUnitsActionNodeData => StateScriptNodeRuntimeType.QueryUnits,
            ExecuteEffectActionNodeData => StateScriptNodeRuntimeType.ExecuteEffect,
            DestroySelfActionNodeData => StateScriptNodeRuntimeType.DestroySelf,
            CompleteInteractionActionNodeData => StateScriptNodeRuntimeType.CompleteInteraction,
            AcknowledgeInteractionActionNodeData => StateScriptNodeRuntimeType.AcknowledgeInteraction,
            CollectInteractionActionNodeData => StateScriptNodeRuntimeType.CollectInteraction,
            StartNpcInteractionActionNodeData => StateScriptNodeRuntimeType.StartNpcInteraction,
            TimerStateScriptNodeData => StateScriptNodeRuntimeType.Timer,
            KeepStateScriptNodeData => StateScriptNodeRuntimeType.Keep,
            MonitorStateScriptNodeData => StateScriptNodeRuntimeType.Monitor,
            NumberMonitorStateScriptNodeData => StateScriptNodeRuntimeType.NumberMonitor,
            AdditionStateScriptNodeData => StateScriptNodeRuntimeType.Addition,
            _ => default,
        };
        return source is StateScriptEntryNodeData or CompareStateScriptNodeData or SetValueStateScriptNodeData or
            RequestSkillActionNodeData or PublishGameEventStateScriptNodeData or
            RequestSkillWithAdditionActionNodeData or RequestInteractionActionNodeData or SpawnUnitActionNodeData or
            QueryUnitsActionNodeData or ExecuteEffectActionNodeData or DestroySelfActionNodeData or
            CompleteInteractionActionNodeData or AcknowledgeInteractionActionNodeData or
            CollectInteractionActionNodeData or StartNpcInteractionActionNodeData or
            TimerStateScriptNodeData or KeepStateScriptNodeData or MonitorStateScriptNodeData or
            NumberMonitorStateScriptNodeData or AdditionStateScriptNodeData;
    }

    private static bool TryGetInputPort(StateScriptNodeRuntimeType type, string name, out byte portId)
    {
        portId = 0;
        if (type == StateScriptNodeRuntimeType.Entry)
            return false;
        if (type == StateScriptNodeRuntimeType.Compare)
            return string.Equals(name, "Check", StringComparison.Ordinal);
        if (!IsStateNode(type))
            return string.Equals(name, "In", StringComparison.Ordinal);
        if (string.Equals(name, "Start", StringComparison.Ordinal))
            return true;
        if (string.Equals(name, "Abort", StringComparison.Ordinal))
        {
            portId = StateScriptPortId.Abort;
            return true;
        }
        if (type == StateScriptNodeRuntimeType.Keep && string.Equals(name, "Keep", StringComparison.Ordinal))
        {
            portId = StateScriptPortId.Keep;
            return true;
        }
        return false;
    }

    private static bool TryGetOutputPort(StateScriptNodeRuntimeType type, string name, out byte portId)
    {
        portId = 0;
        if (type == StateScriptNodeRuntimeType.Entry || !IsStateNode(type) && type != StateScriptNodeRuntimeType.Compare)
            return string.Equals(name, "Out", StringComparison.Ordinal);
        if (type == StateScriptNodeRuntimeType.Compare)
        {
            if (string.Equals(name, "True", StringComparison.Ordinal))
                return true;
            if (string.Equals(name, "False", StringComparison.Ordinal))
            {
                portId = StateScriptPortId.False;
                return true;
            }
            return false;
        }
        string[] baseNames = { "OnStart", "OnTick", "OnComplete", "OnAbort", "OnStop" };
        for (byte index = 0; index < baseNames.Length; index++)
        {
            if (string.Equals(name, baseNames[index], StringComparison.Ordinal))
            {
                portId = index;
                return true;
            }
        }
        if (type == StateScriptNodeRuntimeType.Keep)
        {
            string[] names = { "OnTimeStart", "OnTimeTick", "OnTimeComplete", "OnTimeStop" };
            for (byte index = 0; index < names.Length; index++)
            {
                if (string.Equals(name, names[index], StringComparison.Ordinal))
                {
                    portId = (byte)(StateScriptPortId.OnTimeStart + index);
                    return true;
                }
            }
        }
        if (type == StateScriptNodeRuntimeType.Monitor)
        {
            if (string.Equals(name, "True", StringComparison.Ordinal)) { portId = StateScriptPortId.MonitorTrue; return true; }
            if (string.Equals(name, "False", StringComparison.Ordinal)) { portId = StateScriptPortId.MonitorFalse; return true; }
            if (string.Equals(name, "OnChangeTrue", StringComparison.Ordinal)) { portId = StateScriptPortId.OnChangeTrue; return true; }
            if (string.Equals(name, "OnChangeFalse", StringComparison.Ordinal)) { portId = StateScriptPortId.OnChangeFalse; return true; }
        }
        if (type == StateScriptNodeRuntimeType.NumberMonitor && string.Equals(name, "OnValueChange", StringComparison.Ordinal))
        {
            portId = StateScriptPortId.OnValueChange;
            return true;
        }
        return false;
    }

    private static byte MaxOutputPort(StateScriptNodeRuntimeType type)
    {
        if (type == StateScriptNodeRuntimeType.Compare)
            return 1;
        if (type == StateScriptNodeRuntimeType.Keep || type == StateScriptNodeRuntimeType.Monitor)
            return 8;
        if (type == StateScriptNodeRuntimeType.NumberMonitor)
            return 5;
        return IsStateNode(type) ? (byte)4 : (byte)0;
    }

    public static bool IsStateNode(StateScriptNodeRuntimeType type)
    {
        return type is StateScriptNodeRuntimeType.Timer or StateScriptNodeRuntimeType.Keep or
            StateScriptNodeRuntimeType.Monitor or StateScriptNodeRuntimeType.NumberMonitor or
            StateScriptNodeRuntimeType.Addition;
    }

    private static void WriteUnit(
        ref BlobBuilder builder,
        ref StateScriptUnitDefinitionBlob target,
        CompiledUnit source)
    {
        target.UnitDataId = source.UnitDataId;
        BlobBuilderArray<StateScriptGraphDefinitionBlob> graphs = builder.Allocate(ref target.Graphs, source.Graphs.Count);
        for (int graphIndex = 0; graphIndex < source.Graphs.Count; graphIndex++)
            WriteGraph(ref builder, ref graphs[graphIndex], source.Graphs[graphIndex]);
    }

    private static void WriteGraph(
        ref BlobBuilder builder,
        ref StateScriptGraphDefinitionBlob target,
        CompiledGraph source)
    {
        target.EntryNodeIndex = source.EntryNodeIndex;
        target.ExecutionConditionExpressionIndex = source.ExecutionConditionExpressionIndex;
        target.Guid = source.Guid;
        target.Name = source.Name;
        BlobBuilderArray<StateScriptNodeDefinition> nodes = builder.Allocate(ref target.Nodes, source.Nodes.Count);
        for (int index = 0; index < source.Nodes.Count; index++) nodes[index] = source.Nodes[index];
        BlobBuilderArray<StateScriptOutputRoute> routes = builder.Allocate(ref target.OutputRoutes, source.Routes.Count);
        for (int index = 0; index < source.Routes.Count; index++) routes[index] = source.Routes[index];
        BlobBuilderArray<StateScriptPulseTarget> targets = builder.Allocate(ref target.PulseTargets, source.Targets.Count);
        for (int index = 0; index < source.Targets.Count; index++) targets[index] = source.Targets[index];
        BlobBuilderArray<int> stateNodes = builder.Allocate(ref target.StateNodeIndices, source.StateNodeIndices.Count);
        for (int index = 0; index < source.StateNodeIndices.Count; index++) stateNodes[index] = source.StateNodeIndices[index];
        BlobBuilderArray<FixedString128Bytes> strings = builder.Allocate(ref target.Strings, source.Strings.Count);
        for (int index = 0; index < source.Strings.Count; index++) strings[index] = source.Strings[index];
        BlobBuilderArray<BehaviorExpressionBlob> expressions = builder.Allocate(ref target.Expressions, source.Expressions.Count);
        for (int expressionIndex = 0; expressionIndex < source.Expressions.Count; expressionIndex++)
        {
            CompiledExpression sourceExpression = source.Expressions[expressionIndex];
            ref BehaviorExpressionBlob targetExpression = ref expressions[expressionIndex];
            targetExpression.Category = sourceExpression.Category;
            targetExpression.IsCondition = sourceExpression.IsCondition;
            BlobBuilderArray<ExpressionInstruction> instructions = builder.Allocate(ref targetExpression.Instructions, sourceExpression.Program.Instructions.Length);
            for (int index = 0; index < sourceExpression.Program.Instructions.Length; index++)
                instructions[index] = sourceExpression.Program.Instructions[index];
            BlobBuilderArray<UnitSourceValue> literals = builder.Allocate(ref targetExpression.Literals, sourceExpression.Program.Literals.Length);
            for (int index = 0; index < sourceExpression.Program.Literals.Length; index++)
                literals[index] = sourceExpression.Program.Literals[index];
        }
    }

    private static bool TryCopyFixedString(string value, out FixedString128Bytes destination)
    {
        destination = default;
        return destination.CopyFrom(value ?? string.Empty) == CopyError.None;
    }

    private static ComparatorFactory CreateExpressionFactory()
    {
        ComparatorFactory factory = new();
        ComparatorRegistry.RegisterAll(factory);
        return factory;
    }
}
