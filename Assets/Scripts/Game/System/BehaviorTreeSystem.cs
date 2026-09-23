using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using CrystalMagic.Game.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateAfter(typeof(UnitPerceptionSystem))]
[UpdateBefore(typeof(UnitNavigationSystem))]
[UpdateBefore(typeof(StateScriptSystem))]
public partial class BehaviorTreeSystem : SystemBase
{
    private UnitSourceDispatcher _readSources;
    private UnitSourceDispatcher _writeSources;

    protected override void OnCreate()
    {
        _readSources.InitializeReadOnly(this);
        _writeSources.Initialize(this);
        RequireForUpdate<BehaviorTreeRuntimeRegistryComponent>();
    }

    protected override void OnUpdate()
    {
        BlobAssetReference<BehaviorTreeRuntimeRegistryBlob> registry =
            SystemAPI.GetSingleton<BehaviorTreeRuntimeRegistryComponent>().Value;
        if (!registry.IsCreated)
            return;

        _readSources.Update(this);
        _writeSources.Update(this);

        GameConfig config = ConfigComponent.Instance.Get<GameConfig>();
        int maxImmediateIterations = math.max(
            1,
            config?.BehaviorTreeMaxImmediateIterationsPerTick ?? 256);
        bool captureDebug = DebugComponent.Instance != null && DebugComponent.Instance.IsEnabled;

        Dependency = new BehaviorTreeEvaluationJob
        {
            Registry = registry,
            Sources = _readSources,
            Transforms = GetComponentLookup<LocalTransform>(true),
            PostTransforms = GetComponentLookup<PostTransformMatrix>(true),
            Facings = GetComponentLookup<UnitFacingComponent>(true),
            Navigations = GetComponentLookup<UnitNavigationComponent>(true),
            Moves = GetComponentLookup<UnitMoveComponent>(true),
            Variables = GetComponentLookup<UnitVariableComponent>(true),
            DeltaTime = math.max(0f, SystemAPI.Time.DeltaTime),
            MaxImmediateIterations = maxImmediateIterations,
            CaptureDebug = captureDebug ? (byte)1 : (byte)0,
        }.ScheduleParallel(Dependency);

        Dependency = new BehaviorTreeSourceCommandJob
        {
            Sources = _writeSources,
        }.Schedule(Dependency);

        Dependency = new BehaviorTreeMoveCommandJob().ScheduleParallel(Dependency);
        if (captureDebug)
        {
            Dependency.Complete();
            ReportHitDebugShapes();
        }
    }

    private void ReportHitDebugShapes()
    {
        foreach (DynamicBuffer<BehaviorTreeHitDebugElement> debugShapes in
                 SystemAPI.Query<DynamicBuffer<BehaviorTreeHitDebugElement>>()
                     .WithNone<UnitDeathComponent>())
        {
            for (int index = 0; index < debugShapes.Length; index++)
            {
                BehaviorTreeHitDebugElement debugShape = debugShapes[index];
                DebugQueryShapeReporter.ReportForwardRect(
                    debugShape.QueryOrigin,
                    new float2(1f, 0f),
                    debugShape.Length,
                    debugShape.Width);
                if (debugShape.HasHit != 0)
                    DebugQueryShapeReporter.ReportHit(debugShape.HitOrigin, debugShape.HitPosition);
            }
            debugShapes.Clear();
        }
    }
}

internal struct BehaviorExecutionFrame
{
    public int NodeIndex;
    public int ChildCursor;
    public int PreviousRunningChild;
    public int IterationCount;
    public int SuccessCount;
    public int FailureCount;
    public int RunningCount;
    public BehaviorNodeStatus ChildResult;
    public byte Entered;
    public byte HasChildResult;
}

[BurstCompile]
[WithNone(typeof(UnitInitializationPendingTag))]
[WithNone(typeof(UnitDeathComponent))]
public partial struct BehaviorTreeEvaluationJob : IJobEntity
{
    public BlobAssetReference<BehaviorTreeRuntimeRegistryBlob> Registry;

    [ReadOnly]
    public UnitSourceDispatcher Sources;

    [ReadOnly]
    public ComponentLookup<LocalTransform> Transforms;

    [ReadOnly]
    public ComponentLookup<PostTransformMatrix> PostTransforms;

    [ReadOnly]
    public ComponentLookup<UnitFacingComponent> Facings;

    [ReadOnly]
    public ComponentLookup<UnitNavigationComponent> Navigations;

    [ReadOnly]
    public ComponentLookup<UnitMoveComponent> Moves;

    [ReadOnly]
    public ComponentLookup<UnitVariableComponent> Variables;

    public float DeltaTime;
    public int MaxImmediateIterations;
    public byte CaptureDebug;

    private void Execute(
        Entity entity,
        ref UnitBehaviorTreeComponent component,
        ref DynamicBuffer<BehaviorNodeStateElement> states,
        ref DynamicBuffer<BehaviorTreeCommandElement> commands,
        ref DynamicBuffer<BehaviorTreeCommandArgumentElement> commandArguments,
        ref DynamicBuffer<BehaviorTreeMoveCommandElement> moveCommands,
        ref DynamicBuffer<BehaviorTreeHitDebugElement> hitDebugShapes)
    {
        commands.Clear();
        commandArguments.Clear();
        moveCommands.Clear();
        hitDebugShapes.Clear();

        if (component.InitializationError != BehaviorTreeInitializationError.None ||
            component.TreeIndex < 0 ||
            component.TreeIndex >= Registry.Value.Trees.Length)
        {
            return;
        }

        ref BehaviorTreeDefinitionBlob tree = ref Registry.Value.Trees[component.TreeIndex];
        if (tree.RootNodeIndex < 0 ||
            tree.RootNodeIndex >= tree.Nodes.Length ||
            states.Length != tree.Nodes.Length)
        {
            component.InitializationError = BehaviorTreeInitializationError.InvalidTree;
            return;
        }

        component.TickVersion++;
        if (component.TickVersion == 0)
            component.TickVersion = 1;
        component.CurrentNodeIndex = -1;

        Entity other = Variables.TryGetComponent(entity, out UnitVariableComponent variables)
            ? variables.Other
            : Entity.Null;
        UnitSourceContext context = new(entity, other);

        FixedList4096Bytes<BehaviorExecutionFrame> stack = default;
        stack.Add(new BehaviorExecutionFrame
        {
            NodeIndex = tree.RootNodeIndex,
            PreviousRunningChild = -1,
        });

        int maxSteps = math.max(64, tree.Nodes.Length * (MaxImmediateIterations + 4) * 4);
        int step = 0;
        while (stack.Length > 0 && step++ < maxSteps)
        {
            int frameIndex = stack.Length - 1;
            BehaviorExecutionFrame frame = stack[frameIndex];
            if ((uint)frame.NodeIndex >= (uint)tree.Nodes.Length)
            {
                CompleteNode(
                    ref stack,
                    ref states,
                    ref component,
                    BehaviorNodeStatus.Failure);
                continue;
            }

            BehaviorNodeDefinition definition = tree.Nodes[frame.NodeIndex];
            if (frame.Entered == 0)
            {
                frame.Entered = 1;
                frame.PreviousRunningChild = -1;
                component.CurrentNodeIndex = frame.NodeIndex;

                BehaviorNodeStateElement state = states[frame.NodeIndex];
                switch (definition.Type)
                {
                    case BehaviorNodeRuntimeType.Selector:
                        frame.PreviousRunningChild = state.RunningChildIndex;
                        frame.ChildCursor = 0;
                        break;
                    case BehaviorNodeRuntimeType.Sequence:
                        frame.ChildCursor = state.RunningChildIndex >= 0
                            ? state.RunningChildIndex
                            : 0;
                        break;
                    case BehaviorNodeRuntimeType.Cooldown:
                        if (state.Time > 0f)
                        {
                            state.Time = math.max(0f, state.Time - DeltaTime);
                            states[frame.NodeIndex] = state;
                            if (state.Time > 0f)
                            {
                                stack[frameIndex] = frame;
                                CompleteNode(
                                    ref stack,
                                    ref states,
                                    ref component,
                                    BehaviorNodeStatus.Failure);
                                continue;
                            }
                        }
                        break;
                    case BehaviorNodeRuntimeType.Check:
                    {
                        BehaviorNodeStatus status = EvaluateCheck(ref tree, in definition, in context)
                            ? BehaviorNodeStatus.Success
                            : BehaviorNodeStatus.Failure;
                        stack[frameIndex] = frame;
                        CompleteNode(ref stack, ref states, ref component, status);
                        continue;
                    }
                    case BehaviorNodeRuntimeType.Set:
                    {
                        BehaviorNodeStatus status = QueueSet(
                            ref tree,
                            in definition,
                            in context,
                            ref commands,
                            ref commandArguments)
                            ? BehaviorNodeStatus.Success
                            : BehaviorNodeStatus.Failure;
                        stack[frameIndex] = frame;
                        CompleteNode(ref stack, ref states, ref component, status);
                        continue;
                    }
                    case BehaviorNodeRuntimeType.Wait:
                    {
                        state.Time += DeltaTime;
                        BehaviorNodeStatus status;
                        if (state.Time < math.max(0f, definition.FloatParameters0.x))
                        {
                            status = BehaviorNodeStatus.Running;
                        }
                        else
                        {
                            state.Time = 0f;
                            status = BehaviorNodeStatus.Success;
                        }
                        states[frame.NodeIndex] = state;
                        stack[frameIndex] = frame;
                        CompleteNode(ref stack, ref states, ref component, status);
                        continue;
                    }
                    case BehaviorNodeRuntimeType.HitCheck:
                    {
                        BehaviorNodeStatus status = EvaluateHitCheck(
                            entity,
                            ref tree,
                            in definition,
                            in context,
                            ref state,
                            ref hitDebugShapes);
                        states[frame.NodeIndex] = state;
                        stack[frameIndex] = frame;
                        CompleteNode(ref stack, ref states, ref component, status);
                        continue;
                    }
                    case BehaviorNodeRuntimeType.MoveTo:
                    {
                        BehaviorNodeStatus status = EvaluateMoveTo(
                            entity,
                            ref tree,
                            in definition,
                            in context,
                            ref moveCommands);
                        stack[frameIndex] = frame;
                        CompleteNode(ref stack, ref states, ref component, status);
                        continue;
                    }
                }

                stack[frameIndex] = frame;
            }

            frameIndex = stack.Length - 1;
            frame = stack[frameIndex];
            definition = tree.Nodes[frame.NodeIndex];
            switch (definition.Type)
            {
                case BehaviorNodeRuntimeType.Root:
                    if (definition.ChildCount == 0)
                    {
                        CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Failure);
                    }
                    else if (frame.HasChildResult != 0)
                    {
                        CompleteNode(ref stack, ref states, ref component, frame.ChildResult);
                    }
                    else if (!PushChild(ref stack, GetChild(ref tree, in definition, 0)))
                    {
                        CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Failure);
                    }
                    break;

                case BehaviorNodeRuntimeType.Selector:
                    TickSelector(ref tree, in definition, ref stack, ref states, ref component);
                    break;

                case BehaviorNodeRuntimeType.Sequence:
                    TickSequence(ref tree, in definition, ref stack, ref states, ref component);
                    break;

                case BehaviorNodeRuntimeType.Parallel:
                    TickParallel(ref tree, in definition, ref stack, ref states, ref component);
                    break;

                case BehaviorNodeRuntimeType.Inverter:
                case BehaviorNodeRuntimeType.Succeeder:
                case BehaviorNodeRuntimeType.Failer:
                    TickSimpleDecorator(ref tree, in definition, ref stack, ref states, ref component);
                    break;

                case BehaviorNodeRuntimeType.Repeater:
                    TickRepeater(ref tree, in definition, ref stack, ref states, ref component);
                    break;

                case BehaviorNodeRuntimeType.UntilSuccess:
                case BehaviorNodeRuntimeType.UntilFailure:
                    TickUntil(ref tree, in definition, ref stack, ref states, ref component);
                    break;

                case BehaviorNodeRuntimeType.Cooldown:
                    TickCooldown(ref tree, in definition, ref stack, ref states, ref component);
                    break;

                case BehaviorNodeRuntimeType.Timeout:
                    TickTimeout(ref tree, in definition, ref stack, ref states, ref component);
                    break;

                default:
                    CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Failure);
                    break;
            }
        }

        if (stack.Length > 0)
        {
            component.LastStatus = BehaviorNodeStatus.Failure;
            component.CurrentNodeIndex = -1;
        }
    }

    private bool EvaluateCheck(
        ref BehaviorTreeDefinitionBlob tree,
        in BehaviorNodeDefinition definition,
        in UnitSourceContext context)
    {
        if (definition.ExpressionCount != 1)
            return false;

        ref BehaviorExpressionBlob expression = ref tree.Expressions[definition.ExpressionStart];
        return CompiledExpressionEvaluator.TryEvaluateConditions(
            ref expression,
            in context,
            in Sources);
    }

    private bool QueueSet(
        ref BehaviorTreeDefinitionBlob tree,
        in BehaviorNodeDefinition definition,
        in UnitSourceContext context,
        ref DynamicBuffer<BehaviorTreeCommandElement> commands,
        ref DynamicBuffer<BehaviorTreeCommandArgumentElement> arguments)
    {
        int argumentStart = arguments.Length;
        for (int index = 0; index < definition.ExpressionCount; index++)
        {
            ref BehaviorExpressionBlob expression =
                ref tree.Expressions[definition.ExpressionStart + index];
            if (!CompiledExpressionEvaluator.TryEvaluateValue(
                    ref expression,
                    in context,
                    in Sources,
                    out UnitSourceValue value))
            {
                arguments.ResizeUninitialized(argumentStart);
                return false;
            }
            arguments.Add(new BehaviorTreeCommandArgumentElement { Value = value });
        }

        commands.Add(new BehaviorTreeCommandElement
        {
            SourceId = definition.SetSourceId,
            TargetEntity = context.Resolve(definition.SetSourceTarget),
            ArgumentStart = argumentStart,
            ArgumentCount = definition.ExpressionCount,
            Key = definition.Key,
            HasKey = (byte)definition.IntParameters.x,
        });
        return true;
    }

    private BehaviorNodeStatus EvaluateHitCheck(
        Entity entity,
        ref BehaviorTreeDefinitionBlob tree,
        in BehaviorNodeDefinition definition,
        in UnitSourceContext context,
        ref BehaviorNodeStateElement state,
        ref DynamicBuffer<BehaviorTreeHitDebugElement> hitDebugShapes)
    {
        if (definition.ExpressionCount != 1 ||
            !Transforms.TryGetComponent(entity, out LocalTransform selfTransform))
        {
            return BehaviorNodeStatus.Failure;
        }

        float2 planarScale = math.max(0f, selfTransform.Scale);
        if (PostTransforms.TryGetComponent(entity, out PostTransformMatrix postTransform))
        {
            planarScale *= new float2(
                math.length(postTransform.Value.c0.xyz),
                math.length(postTransform.Value.c1.xyz));
        }

        if (Facings.TryGetComponent(entity, out UnitFacingComponent facing))
        {
            float2 normalizedFacing = math.normalizesafe(facing.Direction, new float2(1f, 0f));
            if (math.abs(normalizedFacing.x) > 0.0001f)
                state.Auxiliary = math.sign(normalizedFacing.x);
        }
        if (math.abs(state.Auxiliary) <= 0.0001f)
            state.Auxiliary = 1f;

        float2 center = definition.FloatParameters0.xy * planarScale;
        center.x *= state.Auxiliary;
        float2 halfSize = math.max(float2.zero, definition.FloatParameters0.zw * 0.5f) * planarScale;
        halfSize += math.max(0f, definition.FloatParameters1.x) * planarScale;
        BehaviorTreeHitDebugElement debugShape = new()
        {
            QueryOrigin = new float3(
                selfTransform.Position.x + center.x - halfSize.x,
                selfTransform.Position.y + center.y,
                selfTransform.Position.z),
            Length = halfSize.x * 2f,
            Width = halfSize.y * 2f,
            HitOrigin = selfTransform.Position,
        };

        ref BehaviorExpressionBlob targetExpression =
            ref tree.Expressions[definition.ExpressionStart];
        if (!CompiledExpressionEvaluator.TryEvaluateValue(
                ref targetExpression,
                in context,
                in Sources,
                out UnitSourceValue targetValue) ||
            !targetValue.TryGetEntity(out Entity target) ||
            target == Entity.Null ||
            !Transforms.TryGetComponent(target, out LocalTransform targetTransform))
        {
            if (CaptureDebug != 0)
                hitDebugShapes.Add(debugShape);
            return BehaviorNodeStatus.Failure;
        }

        float2 localPosition = targetTransform.Position.xy - selfTransform.Position.xy;
        bool hit = math.all(math.abs(localPosition - center) <= halfSize);
        if (CaptureDebug != 0)
        {
            debugShape.HasHit = hit ? (byte)1 : (byte)0;
            debugShape.HitPosition = new float3(
                targetTransform.Position.x,
                targetTransform.Position.y,
                selfTransform.Position.z);
            hitDebugShapes.Add(debugShape);
        }
        return hit ? BehaviorNodeStatus.Success : BehaviorNodeStatus.Failure;
    }

    private BehaviorNodeStatus EvaluateMoveTo(
        Entity entity,
        ref BehaviorTreeDefinitionBlob tree,
        in BehaviorNodeDefinition definition,
        in UnitSourceContext context,
        ref DynamicBuffer<BehaviorTreeMoveCommandElement> moveCommands)
    {
        if (definition.ExpressionCount != 4 ||
            !Navigations.HasComponent(entity) ||
            !Moves.HasComponent(entity) ||
            !Transforms.TryGetComponent(entity, out LocalTransform transform))
        {
            return BehaviorNodeStatus.Failure;
        }

        ref BehaviorExpressionBlob conditions = ref tree.Expressions[definition.ExpressionStart];
        if (!CompiledExpressionEvaluator.TryEvaluateConditions(
                ref conditions,
                in context,
                in Sources))
        {
            return BehaviorNodeStatus.Failure;
        }

        ref BehaviorExpressionBlob destinationExpression =
            ref tree.Expressions[definition.ExpressionStart + 1];
        ref BehaviorExpressionBlob stopDistanceExpression =
            ref tree.Expressions[definition.ExpressionStart + 2];
        ref BehaviorExpressionBlob speedExpression =
            ref tree.Expressions[definition.ExpressionStart + 3];
        if (!CompiledExpressionEvaluator.TryEvaluateValue(
                ref destinationExpression,
                in context,
                in Sources,
                out UnitSourceValue destinationValue) ||
            !destinationValue.TryGetFloat3(out float3 destination) ||
            !CompiledExpressionEvaluator.TryEvaluateValue(
                ref stopDistanceExpression,
                in context,
                in Sources,
                out UnitSourceValue stopDistanceValue) ||
            !stopDistanceValue.TryGetNumber(out float stopDistance) ||
            !CompiledExpressionEvaluator.TryEvaluateValue(
                ref speedExpression,
                in context,
                in Sources,
                out UnitSourceValue speedValue) ||
            !speedValue.TryGetNumber(out float speed))
        {
            return BehaviorNodeStatus.Failure;
        }

        float arrivalDistance = math.max(0f, stopDistance);
        moveCommands.Add(new BehaviorTreeMoveCommandElement
        {
            Destination = destination,
            StopDistance = arrivalDistance,
            Speed = speed < 0f ? -1f : math.max(0f, speed),
        });
        return math.distancesq(transform.Position.xy, destination.xy) <= arrivalDistance * arrivalDistance
            ? BehaviorNodeStatus.Success
            : BehaviorNodeStatus.Running;
    }

    private static void TickSelector(
        ref BehaviorTreeDefinitionBlob tree,
        in BehaviorNodeDefinition definition,
        ref FixedList4096Bytes<BehaviorExecutionFrame> stack,
        ref DynamicBuffer<BehaviorNodeStateElement> states,
        ref UnitBehaviorTreeComponent component)
    {
        int frameIndex = stack.Length - 1;
        BehaviorExecutionFrame frame = stack[frameIndex];
        BehaviorNodeStateElement state = states[frame.NodeIndex];
        if (frame.HasChildResult != 0)
        {
            frame.HasChildResult = 0;
            if (frame.ChildResult == BehaviorNodeStatus.Failure)
            {
                frame.ChildCursor++;
                stack[frameIndex] = frame;
            }
            else
            {
                int nextRunning = frame.ChildResult == BehaviorNodeStatus.Running
                    ? frame.ChildCursor
                    : -1;
                if (frame.PreviousRunningChild >= 0 && frame.PreviousRunningChild != nextRunning)
                {
                    ResetSubtree(
                        ref tree,
                        GetChild(ref tree, in definition, frame.PreviousRunningChild),
                        ref states);
                }
                state.RunningChildIndex = nextRunning;
                states[frame.NodeIndex] = state;
                stack[frameIndex] = frame;
                CompleteNode(ref stack, ref states, ref component, frame.ChildResult);
                return;
            }
        }

        if (frame.ChildCursor >= definition.ChildCount)
        {
            if (frame.PreviousRunningChild >= 0)
            {
                ResetSubtree(
                    ref tree,
                    GetChild(ref tree, in definition, frame.PreviousRunningChild),
                    ref states);
            }
            state.RunningChildIndex = -1;
            states[frame.NodeIndex] = state;
            CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Failure);
            return;
        }

        if (!PushChild(ref stack, GetChild(ref tree, in definition, frame.ChildCursor)))
            CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Failure);
    }

    private static void TickSequence(
        ref BehaviorTreeDefinitionBlob tree,
        in BehaviorNodeDefinition definition,
        ref FixedList4096Bytes<BehaviorExecutionFrame> stack,
        ref DynamicBuffer<BehaviorNodeStateElement> states,
        ref UnitBehaviorTreeComponent component)
    {
        int frameIndex = stack.Length - 1;
        BehaviorExecutionFrame frame = stack[frameIndex];
        BehaviorNodeStateElement state = states[frame.NodeIndex];
        if (frame.HasChildResult != 0)
        {
            frame.HasChildResult = 0;
            if (frame.ChildResult == BehaviorNodeStatus.Success)
            {
                frame.ChildCursor++;
                stack[frameIndex] = frame;
            }
            else
            {
                state.RunningChildIndex = frame.ChildResult == BehaviorNodeStatus.Running
                    ? frame.ChildCursor
                    : -1;
                states[frame.NodeIndex] = state;
                stack[frameIndex] = frame;
                CompleteNode(ref stack, ref states, ref component, frame.ChildResult);
                return;
            }
        }

        if (frame.ChildCursor >= definition.ChildCount)
        {
            state.RunningChildIndex = -1;
            states[frame.NodeIndex] = state;
            CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Success);
            return;
        }

        if (!PushChild(ref stack, GetChild(ref tree, in definition, frame.ChildCursor)))
            CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Failure);
    }

    private static void TickParallel(
        ref BehaviorTreeDefinitionBlob tree,
        in BehaviorNodeDefinition definition,
        ref FixedList4096Bytes<BehaviorExecutionFrame> stack,
        ref DynamicBuffer<BehaviorNodeStateElement> states,
        ref UnitBehaviorTreeComponent component)
    {
        int frameIndex = stack.Length - 1;
        BehaviorExecutionFrame frame = stack[frameIndex];
        if (definition.ChildCount == 0)
        {
            CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Failure);
            return;
        }

        if (frame.HasChildResult != 0)
        {
            frame.HasChildResult = 0;
            switch (frame.ChildResult)
            {
                case BehaviorNodeStatus.Success:
                    frame.SuccessCount++;
                    break;
                case BehaviorNodeStatus.Failure:
                    frame.FailureCount++;
                    break;
                case BehaviorNodeStatus.Running:
                    frame.RunningCount++;
                    break;
            }
            frame.ChildCursor++;
            stack[frameIndex] = frame;
        }

        if (frame.ChildCursor < definition.ChildCount)
        {
            if (!PushChild(ref stack, GetChild(ref tree, in definition, frame.ChildCursor)))
                CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Failure);
            return;
        }

        bool failure = definition.IntParameters.y == (int)ParallelFailurePolicy.RequireAny
            ? frame.FailureCount > 0
            : frame.FailureCount == definition.ChildCount;
        bool success = definition.IntParameters.x == (int)ParallelSuccessPolicy.RequireAny
            ? frame.SuccessCount > 0
            : frame.SuccessCount == definition.ChildCount;
        BehaviorNodeStatus result = failure
            ? BehaviorNodeStatus.Failure
            : success
                ? BehaviorNodeStatus.Success
                : frame.RunningCount + frame.SuccessCount + frame.FailureCount > 0
                    ? BehaviorNodeStatus.Running
                    : BehaviorNodeStatus.Failure;
        CompleteNode(ref stack, ref states, ref component, result);
    }

    private static void TickSimpleDecorator(
        ref BehaviorTreeDefinitionBlob tree,
        in BehaviorNodeDefinition definition,
        ref FixedList4096Bytes<BehaviorExecutionFrame> stack,
        ref DynamicBuffer<BehaviorNodeStateElement> states,
        ref UnitBehaviorTreeComponent component)
    {
        BehaviorExecutionFrame frame = stack[stack.Length - 1];
        if (definition.ChildCount == 0)
        {
            BehaviorNodeStatus emptyResult = definition.Type == BehaviorNodeRuntimeType.Succeeder
                ? BehaviorNodeStatus.Success
                : BehaviorNodeStatus.Failure;
            CompleteNode(ref stack, ref states, ref component, emptyResult);
            return;
        }

        if (frame.HasChildResult == 0)
        {
            if (!PushChild(ref stack, GetChild(ref tree, in definition, 0)))
                CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Failure);
            return;
        }

        BehaviorNodeStatus result = definition.Type switch
        {
            BehaviorNodeRuntimeType.Inverter => frame.ChildResult switch
            {
                BehaviorNodeStatus.Success => BehaviorNodeStatus.Failure,
                BehaviorNodeStatus.Failure => BehaviorNodeStatus.Success,
                _ => BehaviorNodeStatus.Running,
            },
            BehaviorNodeRuntimeType.Succeeder => frame.ChildResult == BehaviorNodeStatus.Running
                ? BehaviorNodeStatus.Running
                : BehaviorNodeStatus.Success,
            _ => frame.ChildResult == BehaviorNodeStatus.Running
                ? BehaviorNodeStatus.Running
                : BehaviorNodeStatus.Failure,
        };
        CompleteNode(ref stack, ref states, ref component, result);
    }

    private void TickRepeater(
        ref BehaviorTreeDefinitionBlob tree,
        in BehaviorNodeDefinition definition,
        ref FixedList4096Bytes<BehaviorExecutionFrame> stack,
        ref DynamicBuffer<BehaviorNodeStateElement> states,
        ref UnitBehaviorTreeComponent component)
    {
        int frameIndex = stack.Length - 1;
        BehaviorExecutionFrame frame = stack[frameIndex];
        BehaviorNodeStateElement state = states[frame.NodeIndex];
        int repeatCount = definition.IntParameters.y;
        if (definition.ChildCount == 0)
        {
            CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Failure);
            return;
        }
        if (repeatCount == 0)
        {
            CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Success);
            return;
        }

        if (frame.HasChildResult != 0)
        {
            frame.HasChildResult = 0;
            int child = GetChild(ref tree, in definition, 0);
            if (frame.ChildResult == BehaviorNodeStatus.Running)
            {
                CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Running);
                return;
            }
            if (frame.ChildResult != BehaviorNodeStatus.Success)
            {
                ResetSubtree(ref tree, child, ref states);
                state.Counter = 0;
                states[frame.NodeIndex] = state;
                CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Failure);
                return;
            }

            state.Counter++;
            ResetSubtree(ref tree, child, ref states);
            if (repeatCount >= 0 && state.Counter >= repeatCount)
            {
                state.Counter = 0;
                states[frame.NodeIndex] = state;
                CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Success);
                return;
            }

            frame.IterationCount++;
            states[frame.NodeIndex] = state;
            stack[frameIndex] = frame;
            if (definition.IntParameters.x == (int)RepeaterExecutionMode.OncePerTick ||
                frame.IterationCount >= MaxImmediateIterations)
            {
                CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Running);
                return;
            }
        }

        if (!PushChild(ref stack, GetChild(ref tree, in definition, 0)))
            CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Failure);
    }

    private void TickUntil(
        ref BehaviorTreeDefinitionBlob tree,
        in BehaviorNodeDefinition definition,
        ref FixedList4096Bytes<BehaviorExecutionFrame> stack,
        ref DynamicBuffer<BehaviorNodeStateElement> states,
        ref UnitBehaviorTreeComponent component)
    {
        int frameIndex = stack.Length - 1;
        BehaviorExecutionFrame frame = stack[frameIndex];
        if (definition.ChildCount == 0)
        {
            CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Failure);
            return;
        }

        if (frame.HasChildResult != 0)
        {
            frame.HasChildResult = 0;
            bool completed = definition.Type == BehaviorNodeRuntimeType.UntilSuccess
                ? frame.ChildResult == BehaviorNodeStatus.Success
                : frame.ChildResult == BehaviorNodeStatus.Failure;
            if (completed)
            {
                CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Success);
                return;
            }
            if (frame.ChildResult == BehaviorNodeStatus.Running)
            {
                CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Running);
                return;
            }

            ResetSubtree(ref tree, GetChild(ref tree, in definition, 0), ref states);
            frame.IterationCount++;
            stack[frameIndex] = frame;
            if (frame.IterationCount >= MaxImmediateIterations)
            {
                CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Running);
                return;
            }
        }

        if (!PushChild(ref stack, GetChild(ref tree, in definition, 0)))
            CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Failure);
    }

    private static void TickCooldown(
        ref BehaviorTreeDefinitionBlob tree,
        in BehaviorNodeDefinition definition,
        ref FixedList4096Bytes<BehaviorExecutionFrame> stack,
        ref DynamicBuffer<BehaviorNodeStateElement> states,
        ref UnitBehaviorTreeComponent component)
    {
        BehaviorExecutionFrame frame = stack[stack.Length - 1];
        if (definition.ChildCount == 0)
        {
            CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Failure);
            return;
        }
        if (frame.HasChildResult == 0)
        {
            if (!PushChild(ref stack, GetChild(ref tree, in definition, 0)))
                CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Failure);
            return;
        }

        if (frame.ChildResult != BehaviorNodeStatus.Running)
        {
            BehaviorNodeStateElement state = states[frame.NodeIndex];
            state.Time = math.max(0f, definition.FloatParameters0.x);
            states[frame.NodeIndex] = state;
        }
        CompleteNode(ref stack, ref states, ref component, frame.ChildResult);
    }

    private void TickTimeout(
        ref BehaviorTreeDefinitionBlob tree,
        in BehaviorNodeDefinition definition,
        ref FixedList4096Bytes<BehaviorExecutionFrame> stack,
        ref DynamicBuffer<BehaviorNodeStateElement> states,
        ref UnitBehaviorTreeComponent component)
    {
        BehaviorExecutionFrame frame = stack[stack.Length - 1];
        if (definition.ChildCount == 0)
        {
            CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Failure);
            return;
        }
        if (frame.HasChildResult == 0)
        {
            if (!PushChild(ref stack, GetChild(ref tree, in definition, 0)))
                CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Failure);
            return;
        }

        BehaviorNodeStateElement state = states[frame.NodeIndex];
        if (frame.ChildResult == BehaviorNodeStatus.Running)
        {
            state.Flags = 1;
            state.Time += DeltaTime;
            if (definition.FloatParameters0.x > 0f && state.Time >= definition.FloatParameters0.x)
            {
                ResetSubtree(ref tree, GetChild(ref tree, in definition, 0), ref states);
                state.Time = 0f;
                state.Flags = 0;
                states[frame.NodeIndex] = state;
                CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Failure);
                return;
            }
            states[frame.NodeIndex] = state;
            CompleteNode(ref stack, ref states, ref component, BehaviorNodeStatus.Running);
            return;
        }

        state.Time = 0f;
        state.Flags = 0;
        states[frame.NodeIndex] = state;
        CompleteNode(ref stack, ref states, ref component, frame.ChildResult);
    }

    private static int GetChild(
        ref BehaviorTreeDefinitionBlob tree,
        in BehaviorNodeDefinition definition,
        int childIndex)
    {
        if ((uint)childIndex >= definition.ChildCount)
            return -1;
        return tree.Children[definition.ChildStart + childIndex];
    }

    private static bool PushChild(
        ref FixedList4096Bytes<BehaviorExecutionFrame> stack,
        int childNodeIndex)
    {
        if (childNodeIndex < 0 || stack.Length >= stack.Capacity)
            return false;
        stack.Add(new BehaviorExecutionFrame
        {
            NodeIndex = childNodeIndex,
            PreviousRunningChild = -1,
        });
        return true;
    }

    private static void CompleteNode(
        ref FixedList4096Bytes<BehaviorExecutionFrame> stack,
        ref DynamicBuffer<BehaviorNodeStateElement> states,
        ref UnitBehaviorTreeComponent component,
        BehaviorNodeStatus status)
    {
        int frameIndex = stack.Length - 1;
        BehaviorExecutionFrame frame = stack[frameIndex];
        if ((uint)frame.NodeIndex < (uint)states.Length)
        {
            BehaviorNodeStateElement state = states[frame.NodeIndex];
            state.LastStatus = status;
            state.LastTickVersion = component.TickVersion;
            states[frame.NodeIndex] = state;
        }

        stack.Length = frameIndex;
        if (stack.Length == 0)
        {
            component.LastStatus = status;
            return;
        }

        int parentIndex = stack.Length - 1;
        BehaviorExecutionFrame parent = stack[parentIndex];
        parent.ChildResult = status;
        parent.HasChildResult = 1;
        stack[parentIndex] = parent;
    }

    private static void ResetSubtree(
        ref BehaviorTreeDefinitionBlob tree,
        int rootNodeIndex,
        ref DynamicBuffer<BehaviorNodeStateElement> states)
    {
        if ((uint)rootNodeIndex >= (uint)tree.Nodes.Length)
            return;

        FixedList4096Bytes<int> pending = default;
        pending.Add(rootNodeIndex);
        while (pending.Length > 0)
        {
            int pendingIndex = pending.Length - 1;
            int nodeIndex = pending[pendingIndex];
            pending.Length = pendingIndex;
            states[nodeIndex] = BehaviorNodeStateElement.CreateDefault();

            BehaviorNodeDefinition node = tree.Nodes[nodeIndex];
            for (int childIndex = 0; childIndex < node.ChildCount; childIndex++)
            {
                if (pending.Length >= pending.Capacity)
                    return;
                pending.Add(GetChild(ref tree, in node, childIndex));
            }
        }
    }
}

[BurstCompile]
[WithNone(typeof(UnitInitializationPendingTag))]
public partial struct BehaviorTreeSourceCommandJob : IJobEntity
{
    public UnitSourceDispatcher Sources;

    private void Execute(
        ref DynamicBuffer<BehaviorTreeCommandElement> commands,
        ref DynamicBuffer<BehaviorTreeCommandArgumentElement> commandArguments)
    {
        for (int commandIndex = 0; commandIndex < commands.Length; commandIndex++)
        {
            BehaviorTreeCommandElement command = commands[commandIndex];
            if (command.ArgumentStart < 0 ||
                command.ArgumentCount > 0 &&
                command.ArgumentStart + command.ArgumentCount > commandArguments.Length)
            {
                continue;
            }

            UnitSourceArguments arguments = default;
            if (command.ArgumentCount > arguments.Values.Capacity)
                continue;
            for (int argumentIndex = 0; argumentIndex < command.ArgumentCount; argumentIndex++)
            {
                arguments.Values.Add(
                    commandArguments[command.ArgumentStart + argumentIndex].Value);
            }
            arguments.Key = command.Key;
            arguments.HasKey = command.HasKey;
            Sources.TrySet(command.TargetEntity, command.SourceId, in arguments);
        }

        commands.Clear();
        commandArguments.Clear();
    }
}

[BurstCompile]
[WithNone(typeof(UnitInitializationPendingTag))]
public partial struct BehaviorTreeMoveCommandJob : IJobEntity
{
    private void Execute(
        ref UnitNavigationComponent navigation,
        ref UnitMoveComponent move,
        ref DynamicBuffer<BehaviorTreeMoveCommandElement> commands)
    {
        for (int index = 0; index < commands.Length; index++)
        {
            BehaviorTreeMoveCommandElement command = commands[index];
            UnitNavigationUtility.SetDestination(
                ref navigation,
                command.Destination,
                command.StopDistance);
            move.CommandMoveSpeed = command.Speed;
        }
        commands.Clear();
    }
}
