using System;
using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;

[FactoryKey("RequestSkillWithAddition", 13, "Request Skill With Addition")]
public sealed class RequestSkillWithAdditionActionNode : StateScriptActionNode
{
    private static readonly ComparatorFactory s_expressionFactory = CreateExpressionFactory();

    private readonly RequestSkillWithAdditionActionNodeData _data;
    private readonly StateScriptOutputPort _output;
    private Func<UnitValue> _skillIdGetter;
    private Func<UnitValue> _positionGetter;
    private Func<UnitValue> _targetEntityGetter;

    public RequestSkillWithAdditionActionNode(RequestSkillWithAdditionActionNodeData data, StateScriptRuntime runtime)
        : base(data, runtime)
    {
        _data = data;
        AddInput("In", RequestSkillWithAddition);
        _output = AddOutput("Out");
    }

    protected override bool OnBind(out string error)
    {
        _skillIdGetter = null;
        _positionGetter = null;
        _targetEntityGetter = null;
        _data.SkillId ??= RequestSkillWithAdditionActionNodeData.CreateDefaultSkillIdExpression();
        _data.Input ??= SkillRequestInputData.CreateDefault();
        _data.Input.EnsureValid();

        if (!Runtime.EntityManager.HasComponent<UnitSkillReleaseComponent>(Runtime.Entity))
        {
            error = "RequestSkillWithAddition requires UnitSkillReleaseComponent.";
            return false;
        }

        if (!s_expressionFactory.TryBuildValueExpression(
                _data.SkillId,
                Runtime.Sources,
                out UnitValueCategory category,
                out Func<UnitValue> skillIdGetter,
                out error))
        {
            return false;
        }

        if (category != UnitValueCategory.Number)
        {
            error = $"RequestSkillWithAddition SkillId requires Number, but received {category}.";
            return false;
        }

        if (!s_expressionFactory.TryBuildValueExpression(
                _data.Input.Position,
                Runtime.Sources,
                out category,
                out Func<UnitValue> positionGetter,
                out error))
        {
            return false;
        }

        if (category != UnitValueCategory.Float3)
        {
            error = $"RequestSkillWithAddition Position requires Float3, but received {category}.";
            return false;
        }

        if (!s_expressionFactory.TryBuildValueExpression(
                _data.Input.TargetEntity,
                Runtime.Sources,
                out category,
                out Func<UnitValue> targetEntityGetter,
                out error))
        {
            return false;
        }

        if (category != UnitValueCategory.Entity)
        {
            error = $"RequestSkillWithAddition TargetEntity requires Entity, but received {category}.";
            return false;
        }

        _skillIdGetter = skillIdGetter;
        _positionGetter = positionGetter;
        _targetEntityGetter = targetEntityGetter;

        if (!Runtime.EntityManager.HasComponent<PlayerCurrentSkillComponent>(Runtime.Entity))
        {
            error = "RequestSkillWithAddition requires PlayerCurrentSkillComponent.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void RequestSkillWithAddition()
    {
        EntityManager entityManager = Runtime.EntityManager;
        Entity entity = Runtime.Entity;
        if (!TryGetSkillId(out int skillId) ||
            !entityManager.HasComponent<UnitSkillReleaseComponent>(entity) ||
            !TryGetInput(out float3 targetPosition, out Entity targetEntity))
        {
            return;
        }

        UnitSkillReleaseComponent releaseComponent = entityManager.GetComponentObject<UnitSkillReleaseComponent>(entity);
        if (releaseComponent == null)
            return;

        SkillReleaseRequest request = SkillReleaseRequestUtility.Create(
            entityManager,
            entity,
            skillId,
            PlayerCurrentSkillUtility.ConsumePendingExtraModifiers(entityManager, entity),
            targetPosition,
            targetEntity);
        releaseComponent.PendingRequests.Add(request);
        _output.Pulse();
    }

    private bool TryGetSkillId(out int skillId)
    {
        skillId = -1;
        if (_skillIdGetter == null ||
            !_skillIdGetter().TryGetNumber(out float rawSkillId) ||
            !math.isfinite(rawSkillId))
        {
            UnityEngine.Debug.LogWarning("[RequestSkillWithAddition] SkillId expression did not return a number.");
            return false;
        }

        float roundedSkillId = math.round(rawSkillId);
        if (roundedSkillId < 0f ||
            roundedSkillId > int.MaxValue ||
            math.abs(rawSkillId - roundedSkillId) > 0.0001f)
        {
            UnityEngine.Debug.LogWarning(
                $"[RequestSkillWithAddition] SkillId must be a non-negative integer, received {rawSkillId}.");
            return false;
        }

        skillId = (int)roundedSkillId;
        return true;
    }

    private bool TryGetInput(out float3 targetPosition, out Entity targetEntity)
    {
        targetPosition = float3.zero;
        targetEntity = Entity.Null;
        if (_positionGetter == null || !_positionGetter().TryGetFloat3(out targetPosition))
        {
            UnityEngine.Debug.LogWarning("[RequestSkillWithAddition] Position expression did not return Float3.");
            return false;
        }

        UnitValue targetValue = _targetEntityGetter == null ? UnitValue.None : _targetEntityGetter();
        if (targetValue.Category != UnitValueCategory.Entity)
        {
            UnityEngine.Debug.LogWarning("[RequestSkillWithAddition] TargetEntity expression did not return Entity.");
            return false;
        }

        targetEntity = targetValue.Entity;
        return true;
    }

    private static ComparatorFactory CreateExpressionFactory()
    {
        ComparatorFactory factory = new();
        ComparatorRegistry.RegisterAll(factory);
        return factory;
    }
}
