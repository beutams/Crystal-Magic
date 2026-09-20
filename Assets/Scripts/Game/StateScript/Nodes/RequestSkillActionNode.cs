using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;

[FactoryKey("RequestSkill", 11, "Request Skill")]
public sealed class RequestSkillActionNode : StateScriptActionNode
{
    private static readonly ComparatorFactory s_expressionFactory = CreateExpressionFactory();

    private readonly RequestSkillActionNodeData _data;
    private readonly StateScriptOutputPort _output;
    private CompiledValueExpression _skillIdExpression;
    private CompiledValueExpression _positionExpression;
    private CompiledValueExpression _targetEntityExpression;

    public RequestSkillActionNode(RequestSkillActionNodeData data, StateScriptRuntime runtime)
        : base(data, runtime)
    {
        _data = data;
        AddInput("In", RequestSkill);
        _output = AddOutput("Out");
    }

    protected override bool OnBind(out string error)
    {
        _skillIdExpression = default;
        _positionExpression = default;
        _targetEntityExpression = default;
        _data.SkillId ??= RequestSkillActionNodeData.CreateDefaultSkillIdExpression();
        _data.Input ??= SkillRequestInputData.CreateDefault();
        _data.Input.EnsureValid();
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
            error = $"RequestSkill SkillId requires Number, but received {skillIdExpression.Category}.";
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
            error = $"RequestSkill Position requires Float3, but received {positionExpression.Category}.";
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
            error = $"RequestSkill TargetEntity requires Entity, but received {targetEntityExpression.Category}.";
            return false;
        }

        if (!Runtime.EntityManager.HasComponent<UnitSkillReleaseComponent>(Runtime.Entity))
        {
            error = "RequestSkill requires UnitSkillReleaseComponent.";
            return false;
        }

        _skillIdExpression = skillIdExpression;
        _positionExpression = positionExpression;
        _targetEntityExpression = targetEntityExpression;
        error = string.Empty;
        return true;
    }

    private void RequestSkill()
    {
        if (!TryGetSkillId(out int skillId) || !TryGetInput(out float3 targetPosition, out Entity targetEntity))
            return;

        EntityManager entityManager = Runtime.EntityManager;
        Entity entity = Runtime.Entity;
        if (!entityManager.HasComponent<UnitSkillReleaseComponent>(entity))
            return;

        if (!entityManager.HasBuffer<SkillReleaseRequest>(entity))
            return;

        SkillReleaseRequest request = SkillReleaseRequestUtility.Create(
            entityManager,
            entity,
            skillId,
            new SkillModifierSet(),
            targetPosition,
            targetEntity);
        entityManager.GetBuffer<SkillReleaseRequest>(entity).Add(request);
        _output.Pulse();
    }

    private bool TryGetSkillId(out int skillId)
    {
        skillId = -1;
        if (!_skillIdExpression.TryEvaluate(Runtime.Sources, out UnitSourceValue skillIdValue) ||
            !skillIdValue.TryGetNumber(out float rawSkillId) || !math.isfinite(rawSkillId))
        {
            UnityEngine.Debug.LogWarning("[RequestSkill] SkillId expression did not return a number.");
            return false;
        }

        float roundedSkillId = math.round(rawSkillId);
        if (roundedSkillId < 0f || roundedSkillId > int.MaxValue || math.abs(rawSkillId - roundedSkillId) > 0.0001f)
        {
            UnityEngine.Debug.LogWarning($"[RequestSkill] SkillId must be a non-negative integer, received {rawSkillId}.");
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
            UnityEngine.Debug.LogWarning("[RequestSkill] Position expression did not return Float3.");
            return false;
        }

        if (!_targetEntityExpression.TryEvaluate(Runtime.Sources, out UnitSourceValue targetValue) ||
            !targetValue.TryGetEntity(out targetEntity))
        {
            UnityEngine.Debug.LogWarning("[RequestSkill] TargetEntity expression did not return Entity.");
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
