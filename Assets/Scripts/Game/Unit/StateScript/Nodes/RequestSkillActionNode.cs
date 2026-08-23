using System;
using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;

[FactoryKey("RequestSkill", 11, "Request Skill")]
public sealed class RequestSkillActionNode : StateScriptActionNode
{
    private static readonly ComparatorFactory s_expressionFactory = CreateExpressionFactory();

    private readonly RequestSkillActionNodeData _data;
    private readonly StateScriptOutputPort _output;
    private Func<UnitValue> _skillIdGetter;

    public RequestSkillActionNode(RequestSkillActionNodeData data, StateScriptRuntime runtime)
        : base(data, runtime)
    {
        _data = data;
        AddInput("In", RequestSkill);
        _output = AddOutput("Out");
    }

    protected override bool OnBind(out string error)
    {
        _skillIdGetter = null;
        _data.SkillId ??= RequestSkillActionNodeData.CreateDefaultSkillIdExpression();
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
            error = $"RequestSkill SkillId requires Number, but received {category}.";
            return false;
        }

        if (!Runtime.EntityManager.HasComponent<UnitSkillReleaseComponent>(Runtime.Entity))
        {
            error = "RequestSkill requires UnitSkillReleaseComponent.";
            return false;
        }

        _skillIdGetter = skillIdGetter;
        error = string.Empty;
        return true;
    }

    private void RequestSkill()
    {
        if (!TryGetSkillId(out int skillId))
            return;

        EntityManager entityManager = Runtime.EntityManager;
        Entity entity = Runtime.Entity;
        if (!entityManager.HasComponent<UnitSkillReleaseComponent>(entity))
            return;

        UnitSkillReleaseComponent releaseComponent = entityManager.GetComponentObject<UnitSkillReleaseComponent>(entity);
        if (releaseComponent == null)
            return;

        SkillReleaseRequest request = SkillReleaseRequestUtility.Create(entityManager, entity, skillId, new SkillModifierSet());
        releaseComponent.PendingRequests.Add(request);
        _output.Pulse();
    }

    private bool TryGetSkillId(out int skillId)
    {
        skillId = -1;
        if (_skillIdGetter == null || !_skillIdGetter().TryGetNumber(out float rawSkillId) || !math.isfinite(rawSkillId))
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

    private static ComparatorFactory CreateExpressionFactory()
    {
        ComparatorFactory factory = new();
        ComparatorRegistry.RegisterAll(factory);
        return factory;
    }
}
