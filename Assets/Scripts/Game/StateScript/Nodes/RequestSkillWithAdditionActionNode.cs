using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;

[FactoryKey("RequestSkillWithAddition", 13, "Request Skill With Addition")]
public sealed class RequestSkillWithAdditionActionNode : StateScriptActionNode
{
    private static readonly ComparatorFactory s_expressionFactory = CreateExpressionFactory();

    private readonly RequestSkillWithAdditionActionNodeData _data;
    private readonly StateScriptOutputPort _output;
    private CompiledValueExpression _skillIdExpression;
    private CompiledValueExpression _positionExpression;
    private CompiledValueExpression _targetEntityExpression;

    public RequestSkillWithAdditionActionNode(RequestSkillWithAdditionActionNodeData data, StateScriptRuntime runtime)
        : base(data, runtime)
    {
        _data = data;
        AddInput("In", RequestSkillWithAddition);
        _output = AddOutput("Out");
    }

    protected override bool OnBind(out string error)
    {
        _skillIdExpression = default;
        _positionExpression = default;
        _targetEntityExpression = default;
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
                out CompiledValueExpression skillIdExpression,
                out error))
        {
            return false;
        }

        if (skillIdExpression.Category != UnitValueCategory.Number)
        {
            error = $"RequestSkillWithAddition SkillId requires Number, but received {skillIdExpression.Category}.";
            return false;
        }

        if (!s_expressionFactory.TryBuildValueExpression(
                _data.Input.Position,
                Runtime.Sources,
                out CompiledValueExpression positionExpression,
                out error))
        {
            return false;
        }

        if (positionExpression.Category != UnitValueCategory.Float3)
        {
            error = $"RequestSkillWithAddition Position requires Float3, but received {positionExpression.Category}.";
            return false;
        }

        if (!s_expressionFactory.TryBuildValueExpression(
                _data.Input.TargetEntity,
                Runtime.Sources,
                out CompiledValueExpression targetEntityExpression,
                out error))
        {
            return false;
        }

        if (targetEntityExpression.Category != UnitValueCategory.Entity)
        {
            error = $"RequestSkillWithAddition TargetEntity requires Entity, but received {targetEntityExpression.Category}.";
            return false;
        }

        _skillIdExpression = skillIdExpression;
        _positionExpression = positionExpression;
        _targetEntityExpression = targetEntityExpression;

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

        if (!entityManager.HasBuffer<SkillReleaseRequest>(entity))
            return;

        SkillReleaseRequest request = SkillReleaseRequestUtility.Create(
            entityManager,
            entity,
            skillId,
            PlayerCurrentSkillUtility.ConsumePendingExtraModifiers(entityManager, entity),
            targetPosition,
            targetEntity);
        entityManager.GetBuffer<SkillReleaseRequest>(entity).Add(request);
        _output.Pulse();
    }

    private bool TryGetSkillId(out int skillId)
    {
        skillId = -1;
        if (!_skillIdExpression.TryEvaluate(Runtime.Sources, out UnitSourceValue skillIdValue) ||
            !skillIdValue.TryGetNumber(out float rawSkillId) ||
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
        if (!_positionExpression.TryEvaluate(Runtime.Sources, out UnitSourceValue positionValue) ||
            !positionValue.TryGetFloat3(out targetPosition))
        {
            UnityEngine.Debug.LogWarning("[RequestSkillWithAddition] Position expression did not return Float3.");
            return false;
        }

        if (!_targetEntityExpression.TryEvaluate(Runtime.Sources, out UnitSourceValue targetValue) ||
            !targetValue.TryGetEntity(out targetEntity))
        {
            UnityEngine.Debug.LogWarning("[RequestSkillWithAddition] TargetEntity expression did not return Entity.");
            return false;
        }

        return true;
    }

    private static ComparatorFactory CreateExpressionFactory()
    {
        ComparatorFactory factory = new();
        ComparatorRegistry.RegisterAll(factory);
        return factory;
    }
}
