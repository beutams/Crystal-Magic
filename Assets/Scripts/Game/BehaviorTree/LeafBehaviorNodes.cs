using System;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[FactoryKey(BehaviorNodeTypes.Set, 11, "Set")]
public sealed class SetBehaviorNode : ActionBehaviorNode
{
    private static readonly ComparatorFactory s_expressionFactory = CreateExpressionFactory();
    private readonly SetBehaviorNodeData _data;
    private UnitSourceSet _set;
    private string _key;
    private Func<UnitValue>[] _inputGetters;
    private UnitValue[] _inputValues;

    public SetBehaviorNode(SetBehaviorNodeData data)
        : base(data)
    {
        _data = data;
    }

    protected override bool OnBind(UnitSourceResolver sources, out string error)
    {
        _set = null;
        _key = string.Empty;
        _inputGetters = null;
        _inputValues = null;
        if (_data == null || string.IsNullOrWhiteSpace(_data.SetKey))
        {
            error = "Set key is empty.";
            return false;
        }

        if (!sources.TryGetDefinition(_data.SetKey, out UnitSourceSet set))
        {
            error = $"Set '{_data.SetKey}' is unavailable on this unit.";
            return false;
        }

        if (_data.Inputs == null || _data.Inputs.Count != set.Parameters.Count)
        {
            error = $"Set '{_data.SetKey}' requires {set.Parameters.Count} input(s).";
            return false;
        }

        if (set.RequiresKey && string.IsNullOrWhiteSpace(_data.Key))
        {
            error = $"Set '{_data.SetKey}' requires a configured key.";
            return false;
        }

        Func<UnitValue>[] inputGetters = new Func<UnitValue>[set.Parameters.Count];
        for (int i = 0; i < set.Parameters.Count; i++)
        {
            if (!s_expressionFactory.TryBuildValueExpression(
                    _data.Inputs[i],
                    sources,
                    out UnitValueCategory category,
                    out Func<UnitValue> getter,
                    out error))
            {
                return false;
            }

            if (!set.Parameters[i].Accepts(category))
            {
                error = $"Set '{_data.SetKey}' input '{set.Parameters[i].Name}' requires {set.Parameters[i].Category}, but received {category}.";
                return false;
            }

            inputGetters[i] = getter;
        }

        _set = set;
        _key = _data.Key ?? string.Empty;
        _inputGetters = inputGetters;
        _inputValues = new UnitValue[inputGetters.Length];
        error = string.Empty;
        return true;
    }

    protected override BehaviorNodeStatus OnTick(BehaviorContext context)
    {
        if (_set == null || _inputGetters == null)
            return BehaviorNodeStatus.Failure;

        for (int i = 0; i < _inputGetters.Length; i++)
        {
            Func<UnitValue> getter = _inputGetters[i];
            if (getter == null)
                return BehaviorNodeStatus.Failure;

            _inputValues[i] = getter();
        }

        bool didSet = _set.RequiresKey
            ? _set.TrySet(_key, _inputValues[0])
            : _set.TrySet(_inputValues);
        return didSet
            ? BehaviorNodeStatus.Success
            : BehaviorNodeStatus.Failure;
    }

    private static ComparatorFactory CreateExpressionFactory()
    {
        ComparatorFactory factory = new();
        ComparatorRegistry.RegisterAll(factory);
        return factory;
    }
}
[FactoryKey(BehaviorNodeTypes.HitCheck, 13, "Hit Check")]
public sealed class HitCheckBehaviorNode : ABehaviorNode
{
    private static readonly ComparatorFactory s_expressionFactory = CreateExpressionFactory();

    private readonly HitCheckBehaviorNodeData _data;
    private Func<UnitValue> _targetGetter;
    private float _horizontalFacingSign = 1f;

    public HitCheckBehaviorNode(HitCheckBehaviorNodeData data)
        : base(data)
    {
        _data = data;
    }

    protected override bool OnBind(UnitSourceResolver sources, out string error)
    {
        _targetGetter = null;
        _data.Target ??= new ValueExpression { Literal = UnitValue.FromEntity(Entity.Null) };
        if (!s_expressionFactory.TryBuildValueExpression(
                _data.Target,
                sources,
                out UnitValueCategory category,
                out Func<UnitValue> targetGetter,
                out error))
        {
            return false;
        }

        if (category != UnitValueCategory.Entity)
        {
            error = $"HitCheck Target requires Entity, but received {category}.";
            return false;
        }

        _targetGetter = targetGetter;
        error = string.Empty;
        return true;
    }

    protected override BehaviorNodeStatus OnTick(BehaviorContext context)
    {
        if (context == null || _targetGetter == null)
            return BehaviorNodeStatus.Failure;

        EntityManager entityManager = context.EntityManager;
        if (!entityManager.Exists(context.Entity) ||
            !entityManager.HasComponent<LocalTransform>(context.Entity))
        {
            return BehaviorNodeStatus.Failure;
        }

        LocalTransform selfTransform = entityManager.GetComponentData<LocalTransform>(context.Entity);
        float2 origin = selfTransform.Position.xy;
        float2 planarScale = GetPlanarScale(entityManager, context.Entity, selfTransform);
        float2 center = new float2(_data.Center.x, _data.Center.y) * planarScale;
        float2 halfSize = math.max(float2.zero, new float2(_data.Size.x, _data.Size.y) * 0.5f) * planarScale;
        halfSize += new float2(math.max(0f, _data.TargetPadding)) * planarScale;

        // HitCheck keeps the prefab's original angle: its local X/Y are world axes.
        // Horizontal facing mirrors only the X offset, while a vertical facing retains
        // the most recent horizontal direction used by the two-direction sprite.
        if (UnitFacingUtility.TryGetFacing(entityManager, context.Entity, out float2 facing) &&
            math.abs(facing.x) > 0.0001f)
        {
            _horizontalFacingSign = math.sign(facing.x);
        }

        center.x *= _horizontalFacingSign;

        // Report it through the shared debug-query path so it remains visible
        // for the same one-second fade window as other spatial queries.
        float2 queryStart = origin + new float2(center.x - halfSize.x, center.y);
        DebugQueryShapeReporter.ReportForwardRect(
            new float3(queryStart.x, queryStart.y, selfTransform.Position.z),
            new float2(1f, 0f),
            halfSize.x * 2f,
            halfSize.y * 2f);

        UnitValue targetValue = _targetGetter();
        Entity target = targetValue.Category == UnitValueCategory.Entity ? targetValue.Entity : Entity.Null;
        if (target == Entity.Null ||
            !entityManager.Exists(target) ||
            !entityManager.HasComponent<LocalTransform>(target))
        {
            return BehaviorNodeStatus.Failure;
        }

        float2 targetPosition = entityManager.GetComponentData<LocalTransform>(target).Position.xy;
        float2 localPosition = targetPosition - origin;
        bool hit = math.all(math.abs(localPosition - center) <= halfSize);
        if (hit)
        {
            DebugQueryShapeReporter.ReportHit(
                new float3(origin.x, origin.y, selfTransform.Position.z),
                new float3(targetPosition.x, targetPosition.y, selfTransform.Position.z));
        }

        return hit ? BehaviorNodeStatus.Success : BehaviorNodeStatus.Failure;
    }

    private static float2 GetPlanarScale(EntityManager entityManager, Entity entity, LocalTransform transform)
    {
        float uniformScale = math.max(0f, transform.Scale);
        float2 scale = new(uniformScale);
        if (!entityManager.HasComponent<PostTransformMatrix>(entity))
            return scale;

        float4x4 matrix = entityManager.GetComponentData<PostTransformMatrix>(entity).Value;
        return scale * new float2(
            math.length(matrix.c0.xyz),
            math.length(matrix.c1.xyz));
    }

    private static ComparatorFactory CreateExpressionFactory()
    {
        ComparatorFactory factory = new();
        ComparatorRegistry.RegisterAll(factory);
        return factory;
    }
}

[FactoryKey(BehaviorNodeTypes.Wait, 12, "Wait")]
public sealed class WaitBehaviorNode : ActionBehaviorNode
{
    private readonly WaitBehaviorNodeData _data;
    private float _elapsedSeconds;

    public WaitBehaviorNode(WaitBehaviorNodeData data)
        : base(data)
    {
        _data = data;
    }

    protected override BehaviorNodeStatus OnTick(BehaviorContext context)
    {
        float duration = Mathf.Max(0f, _data?.DurationSeconds ?? 0f);
        _elapsedSeconds += Mathf.Max(0f, context?.DeltaTime ?? 0f);
        if (_elapsedSeconds < duration)
            return BehaviorNodeStatus.Running;

        _elapsedSeconds = 0f;
        return BehaviorNodeStatus.Success;
    }

    public override void Reset()
    {
        _elapsedSeconds = 0f;
        base.Reset();
    }
}
