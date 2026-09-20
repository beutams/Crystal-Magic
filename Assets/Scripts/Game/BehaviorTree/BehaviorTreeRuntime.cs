using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public static class BehaviorTreeCompiler
{
    private sealed class CompiledTree
    {
        public int UnitDataId;
        public int RootNodeIndex;
        public readonly List<BehaviorNodeDefinition> Nodes = new();
        public readonly List<int> Children = new();
        public readonly List<CompiledExpression> Expressions = new();
    }

    private struct CompiledExpression
    {
        public ExpressionProgram Program;
        public UnitValueCategory Category;
        public byte IsCondition;
    }

    public static bool TryBuildRegistry(
        IReadOnlyList<BehaviorTreeData> sourceTrees,
        out BlobAssetReference<BehaviorTreeRuntimeRegistryBlob> registry,
        out string error)
    {
        registry = default;
        error = string.Empty;
        if (sourceTrees == null)
        {
            error = "Behavior tree table is unavailable.";
            return false;
        }

        List<BehaviorTreeData> orderedTrees = new(sourceTrees.Count);
        for (int index = 0; index < sourceTrees.Count; index++)
        {
            if (sourceTrees[index] != null)
                orderedTrees.Add(sourceTrees[index]);
        }
        orderedTrees.Sort(static (left, right) => left.UnitDataId.CompareTo(right.UnitDataId));
        for (int index = 1; index < orderedTrees.Count; index++)
        {
            if (orderedTrees[index - 1].UnitDataId == orderedTrees[index].UnitDataId)
            {
                error = $"Duplicate behavior tree UnitDataId '{orderedTrees[index].UnitDataId}'.";
                return false;
            }
        }

        List<CompiledTree> compiledTrees = new(orderedTrees.Count);
        for (int index = 0; index < orderedTrees.Count; index++)
        {
            if (!TryCompileTree(orderedTrees[index], out CompiledTree compiled, out error))
            {
                error = $"Behavior tree '{orderedTrees[index].Name}' failed to compile: {error}";
                return false;
            }
            compiledTrees.Add(compiled);
        }

        BlobBuilder builder = new(Allocator.Temp);
        ref BehaviorTreeRuntimeRegistryBlob root = ref builder.ConstructRoot<BehaviorTreeRuntimeRegistryBlob>();
        BlobBuilderArray<BehaviorTreeDefinitionBlob> treeArray =
            builder.Allocate(ref root.Trees, compiledTrees.Count);
        for (int treeIndex = 0; treeIndex < compiledTrees.Count; treeIndex++)
            WriteTree(ref builder, ref treeArray[treeIndex], compiledTrees[treeIndex]);

        registry = builder.CreateBlobAssetReference<BehaviorTreeRuntimeRegistryBlob>(Allocator.Persistent);
        builder.Dispose();
        return true;
    }

    public static int FindTreeIndex(
        in BlobAssetReference<BehaviorTreeRuntimeRegistryBlob> registry,
        int unitDataId)
    {
        if (!registry.IsCreated)
            return -1;

        ref BlobArray<BehaviorTreeDefinitionBlob> trees = ref registry.Value.Trees;
        int low = 0;
        int high = trees.Length - 1;
        while (low <= high)
        {
            int middle = low + ((high - low) >> 1);
            int candidate = trees[middle].UnitDataId;
            if (candidate == unitDataId)
                return middle;
            if (candidate < unitDataId)
                low = middle + 1;
            else
                high = middle - 1;
        }
        return -1;
    }

    private static bool TryCompileTree(
        BehaviorTreeData source,
        out CompiledTree compiled,
        out string error)
    {
        compiled = null;
        error = string.Empty;
        if (source == null || source.Nodes == null || source.Nodes.Count == 0)
        {
            error = "Tree has no nodes.";
            return false;
        }

        Dictionary<string, int> nodeIndices = new(StringComparer.Ordinal);
        for (int index = 0; index < source.Nodes.Count; index++)
        {
            BehaviorNodeData node = source.Nodes[index];
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

        if (string.IsNullOrWhiteSpace(source.RootNodeGuid) ||
            !nodeIndices.TryGetValue(source.RootNodeGuid, out int rootNodeIndex))
        {
            error = "Root node is missing.";
            return false;
        }
        if (!TryValidateGraph(source, nodeIndices, rootNodeIndex, out error))
            return false;

        compiled = new CompiledTree
        {
            UnitDataId = source.UnitDataId,
            RootNodeIndex = rootNodeIndex,
        };
        UnitSourceResolver schemaResolver = new(Entity.Null);
        ComparatorFactory expressionFactory = CreateExpressionFactory();

        for (int index = 0; index < source.Nodes.Count; index++)
        {
            BehaviorNodeData sourceNode = source.Nodes[index];
            if (!TryCompileNode(
                    sourceNode,
                    nodeIndices,
                    schemaResolver,
                    expressionFactory,
                    compiled,
                    out BehaviorNodeDefinition definition,
                    out error))
            {
                error = $"Node '{sourceNode.Guid}' ({sourceNode.Type}): {error}";
                compiled = null;
                return false;
            }
            compiled.Nodes.Add(definition);
        }

        return true;
    }

    private static bool TryValidateGraph(
        BehaviorTreeData source,
        Dictionary<string, int> nodeIndices,
        int rootNodeIndex,
        out string error)
    {
        byte[] visitState = new byte[source.Nodes.Count];
        string validationError = string.Empty;
        if (Visit(rootNodeIndex))
        {
            error = string.Empty;
            return true;
        }
        error = validationError;
        return false;

        bool Visit(int nodeIndex)
        {
            if (visitState[nodeIndex] == 1)
            {
                validationError = $"Cycle detected at node '{source.Nodes[nodeIndex].Guid}'.";
                return false;
            }
            if (visitState[nodeIndex] == 2)
                return true;

            visitState[nodeIndex] = 1;
            List<string> children = source.Nodes[nodeIndex].ChildGuids;
            if (children != null)
            {
                for (int childIndex = 0; childIndex < children.Count; childIndex++)
                {
                    if (!nodeIndices.TryGetValue(children[childIndex], out int childNodeIndex))
                    {
                        validationError = $"Child '{children[childIndex]}' is missing.";
                        return false;
                    }
                    if (!Visit(childNodeIndex))
                        return false;
                }
            }

            visitState[nodeIndex] = 2;
            return true;
        }
    }

    private static bool TryCompileNode(
        BehaviorNodeData source,
        Dictionary<string, int> nodeIndices,
        UnitSourceResolver schemaResolver,
        ComparatorFactory expressionFactory,
        CompiledTree tree,
        out BehaviorNodeDefinition definition,
        out string error)
    {
        definition = default;
        error = string.Empty;
        if (!TryResolveNodeType(source, out BehaviorNodeRuntimeType type))
        {
            error = $"Unsupported node type '{source?.Type}'.";
            return false;
        }

        definition.Type = type;
        definition.ChildStart = tree.Children.Count;
        definition.ExpressionStart = tree.Expressions.Count;
        if (!TryCopyFixedString(source.Guid, out definition.Guid))
        {
            error = "Guid is too long.";
            return false;
        }

        List<string> childGuids = source.ChildGuids ?? new List<string>();
        if (IsSingleChildNode(type) && childGuids.Count > 1)
        {
            error = "Decorator/root nodes may have at most one child.";
            return false;
        }
        if (childGuids.Count > ushort.MaxValue)
        {
            error = "Node has too many children.";
            return false;
        }
        for (int childIndex = 0; childIndex < childGuids.Count; childIndex++)
        {
            if (!nodeIndices.TryGetValue(childGuids[childIndex], out int resolvedChild))
            {
                error = $"Child '{childGuids[childIndex]}' is missing.";
                return false;
            }
            tree.Children.Add(resolvedChild);
        }
        definition.ChildCount = (ushort)childGuids.Count;

        switch (source)
        {
            case ParallelBehaviorNodeData parallel:
                definition.IntParameters.x = (int)parallel.SuccessPolicy;
                definition.IntParameters.y = (int)parallel.FailurePolicy;
                break;
            case RepeaterBehaviorNodeData repeater:
                definition.IntParameters.x = (int)repeater.ExecutionMode;
                definition.IntParameters.y = repeater.RepeatCount;
                break;
            case CooldownBehaviorNodeData cooldown:
                definition.FloatParameters0.x = math.max(0f, cooldown.CooldownSeconds);
                break;
            case TimeoutBehaviorNodeData timeout:
                definition.FloatParameters0.x = math.max(0f, timeout.TimeoutSeconds);
                break;
            case WaitBehaviorNodeData wait:
                definition.FloatParameters0.x = math.max(0f, wait.DurationSeconds);
                break;
            case CheckBehaviorNodeData check:
                if (!TryAddConditions(check.Conditions, schemaResolver, expressionFactory, tree, out error))
                    return false;
                break;
            case HitCheckBehaviorNodeData hitCheck:
                definition.FloatParameters0 = new float4(
                    hitCheck.Center.x,
                    hitCheck.Center.y,
                    hitCheck.Size.x,
                    hitCheck.Size.y);
                definition.FloatParameters1.x = math.max(0f, hitCheck.TargetPadding);
                if (!TryAddValueExpression(
                        hitCheck.Target,
                        UnitValueCategory.Entity,
                        schemaResolver,
                        expressionFactory,
                        tree,
                        out error))
                {
                    return false;
                }
                break;
            case SetBehaviorNodeData set:
                if (!TryCompileSet(set, schemaResolver, expressionFactory, tree, ref definition, out error))
                    return false;
                break;
            case MoveToBehaviorNodeData moveTo:
                if (!TryAddConditions(moveTo.Conditions, schemaResolver, expressionFactory, tree, out error) ||
                    !TryAddValueExpression(moveTo.Destination, UnitValueCategory.Float3, schemaResolver, expressionFactory, tree, out error) ||
                    !TryAddValueExpression(moveTo.StopDistance, UnitValueCategory.Number, schemaResolver, expressionFactory, tree, out error) ||
                    !TryAddValueExpression(moveTo.Speed, UnitValueCategory.Number, schemaResolver, expressionFactory, tree, out error))
                {
                    return false;
                }
                break;
        }

        int expressionCount = tree.Expressions.Count - definition.ExpressionStart;
        if (expressionCount > byte.MaxValue)
        {
            error = "Node has too many expressions.";
            return false;
        }
        definition.ExpressionCount = (byte)expressionCount;
        return true;
    }

    private static bool TryCompileSet(
        SetBehaviorNodeData source,
        UnitSourceResolver schemaResolver,
        ComparatorFactory expressionFactory,
        CompiledTree tree,
        ref BehaviorNodeDefinition definition,
        out string error)
    {
        error = string.Empty;
        if (source == null || string.IsNullOrWhiteSpace(source.SetKey) ||
            !UnitComponentSourceRegistry.TryGetSet(
                source.SetKey,
                out UnitSourceId sourceId,
                out UnitSourceSetSchemaEntry schema))
        {
            error = $"Set source '{source?.SetKey}' is unavailable.";
            return false;
        }

        List<ValueExpression> inputs = source.Inputs ?? new List<ValueExpression>();
        if (inputs.Count != schema.Parameters.Count)
        {
            error = $"Set source requires {schema.Parameters.Count} input(s).";
            return false;
        }
        if (schema.RequiresKey && !TryCopyFixedString(source.Key, out definition.Key))
        {
            error = "Set key is empty or too long.";
            return false;
        }

        definition.SetSourceId = sourceId;
        definition.SetSourceTarget = source.SourceTarget;
        definition.IntParameters.x = schema.RequiresKey ? 1 : 0;
        for (int index = 0; index < inputs.Count; index++)
        {
            if (!TryAddValueExpression(
                    inputs[index],
                    schema.Parameters[index].Category,
                    schemaResolver,
                    expressionFactory,
                    tree,
                    out error))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryAddConditions(
        IReadOnlyList<ConditionConfig> conditions,
        UnitSourceResolver schemaResolver,
        ComparatorFactory expressionFactory,
        CompiledTree tree,
        out string error)
    {
        Comparator comparator = expressionFactory.BuildComparator(conditions, schemaResolver);
        if (!comparator.IsValid)
        {
            error = "Condition expression is invalid.";
            return false;
        }
        tree.Expressions.Add(new CompiledExpression
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
        CompiledTree tree,
        out string error)
    {
        if (!expressionFactory.TryBuildValueExpression(
                source,
                schemaResolver,
                out CompiledValueExpression expression,
                out error))
        {
            return false;
        }
        if (expectedCategory != UnitValueCategory.Any && expression.Category != expectedCategory)
        {
            error = $"Expected {expectedCategory}, received {expression.Category}.";
            return false;
        }
        tree.Expressions.Add(new CompiledExpression
        {
            Program = expression.Program,
            Category = expression.Category,
        });
        return true;
    }

    private static void WriteTree(
        ref BlobBuilder builder,
        ref BehaviorTreeDefinitionBlob target,
        CompiledTree source)
    {
        target.UnitDataId = source.UnitDataId;
        target.RootNodeIndex = source.RootNodeIndex;

        BlobBuilderArray<BehaviorNodeDefinition> nodes = builder.Allocate(ref target.Nodes, source.Nodes.Count);
        for (int index = 0; index < source.Nodes.Count; index++)
            nodes[index] = source.Nodes[index];

        BlobBuilderArray<int> children = builder.Allocate(ref target.Children, source.Children.Count);
        for (int index = 0; index < source.Children.Count; index++)
            children[index] = source.Children[index];

        BlobBuilderArray<BehaviorExpressionBlob> expressions =
            builder.Allocate(ref target.Expressions, source.Expressions.Count);
        for (int expressionIndex = 0; expressionIndex < source.Expressions.Count; expressionIndex++)
        {
            CompiledExpression sourceExpression = source.Expressions[expressionIndex];
            ref BehaviorExpressionBlob targetExpression = ref expressions[expressionIndex];
            targetExpression.Category = sourceExpression.Category;
            targetExpression.IsCondition = sourceExpression.IsCondition;

            BlobBuilderArray<ExpressionInstruction> instructions = builder.Allocate(
                ref targetExpression.Instructions,
                sourceExpression.Program.Instructions.Length);
            for (int instructionIndex = 0;
                 instructionIndex < sourceExpression.Program.Instructions.Length;
                 instructionIndex++)
            {
                instructions[instructionIndex] = sourceExpression.Program.Instructions[instructionIndex];
            }

            BlobBuilderArray<UnitSourceValue> literals = builder.Allocate(
                ref targetExpression.Literals,
                sourceExpression.Program.Literals.Length);
            for (int literalIndex = 0; literalIndex < sourceExpression.Program.Literals.Length; literalIndex++)
                literals[literalIndex] = sourceExpression.Program.Literals[literalIndex];
        }
    }

    private static bool TryResolveNodeType(BehaviorNodeData source, out BehaviorNodeRuntimeType type)
    {
        type = source switch
        {
            RootBehaviorNodeData => BehaviorNodeRuntimeType.Root,
            SelectorBehaviorNodeData => BehaviorNodeRuntimeType.Selector,
            SequenceBehaviorNodeData => BehaviorNodeRuntimeType.Sequence,
            ParallelBehaviorNodeData => BehaviorNodeRuntimeType.Parallel,
            InverterBehaviorNodeData => BehaviorNodeRuntimeType.Inverter,
            SucceederBehaviorNodeData => BehaviorNodeRuntimeType.Succeeder,
            FailerBehaviorNodeData => BehaviorNodeRuntimeType.Failer,
            RepeaterBehaviorNodeData => BehaviorNodeRuntimeType.Repeater,
            UntilSuccessBehaviorNodeData => BehaviorNodeRuntimeType.UntilSuccess,
            UntilFailureBehaviorNodeData => BehaviorNodeRuntimeType.UntilFailure,
            CooldownBehaviorNodeData => BehaviorNodeRuntimeType.Cooldown,
            TimeoutBehaviorNodeData => BehaviorNodeRuntimeType.Timeout,
            CheckBehaviorNodeData => BehaviorNodeRuntimeType.Check,
            HitCheckBehaviorNodeData => BehaviorNodeRuntimeType.HitCheck,
            SetBehaviorNodeData => BehaviorNodeRuntimeType.Set,
            WaitBehaviorNodeData => BehaviorNodeRuntimeType.Wait,
            MoveToBehaviorNodeData => BehaviorNodeRuntimeType.MoveTo,
            _ => default,
        };
        return source is RootBehaviorNodeData or SelectorBehaviorNodeData or SequenceBehaviorNodeData or
            ParallelBehaviorNodeData or InverterBehaviorNodeData or SucceederBehaviorNodeData or
            FailerBehaviorNodeData or RepeaterBehaviorNodeData or UntilSuccessBehaviorNodeData or
            UntilFailureBehaviorNodeData or CooldownBehaviorNodeData or TimeoutBehaviorNodeData or
            CheckBehaviorNodeData or HitCheckBehaviorNodeData or SetBehaviorNodeData or
            WaitBehaviorNodeData or MoveToBehaviorNodeData;
    }

    private static bool IsSingleChildNode(BehaviorNodeRuntimeType type)
    {
        return type is BehaviorNodeRuntimeType.Root or BehaviorNodeRuntimeType.Inverter or
            BehaviorNodeRuntimeType.Succeeder or BehaviorNodeRuntimeType.Failer or
            BehaviorNodeRuntimeType.Repeater or BehaviorNodeRuntimeType.UntilSuccess or
            BehaviorNodeRuntimeType.UntilFailure or BehaviorNodeRuntimeType.Cooldown or
            BehaviorNodeRuntimeType.Timeout;
    }

    private static bool TryCopyFixedString(string source, out FixedString128Bytes value)
    {
        value = default;
        return !string.IsNullOrWhiteSpace(source) && value.CopyFrom(source) == CopyError.None;
    }

    private static ComparatorFactory CreateExpressionFactory()
    {
        ComparatorFactory factory = new();
        ComparatorRegistry.RegisterAll(factory);
        return factory;
    }
}
