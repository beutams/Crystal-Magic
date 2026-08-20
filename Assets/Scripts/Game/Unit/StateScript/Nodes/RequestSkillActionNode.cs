using System;
using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[FactoryKey("RequestSkill", 11, "Request Skill")]
public sealed class RequestSkillActionNode : StateScriptActionNode
{
    private const string TargetPositionVariableKey = "skill.targetPosition";
    private const string TargetEntityVariableKey = "skill.targetEntity";

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

        SkillReleaseRequest request = CreateRequest(entityManager, entity, skillId);
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

    private SkillReleaseRequest CreateRequest(EntityManager entityManager, Entity entity, int skillId)
    {
        SkillReleaseRequest request = new()
        {
            SkillId = skillId,
            OriginEntity = entity,
        };

        if (entityManager.HasComponent<LocalTransform>(entity))
            request.OriginPosition = entityManager.GetComponentData<LocalTransform>(entity).Position;

        if (entityManager.HasComponent<UnitFacingComponent>(entity))
        {
            UnitFacingComponent facing = entityManager.GetComponentData<UnitFacingComponent>(entity);
            request.OriginFacing = math.normalizesafe(facing.Direction, new float2(1f, 0f));
        }

        if (entityManager.HasComponent<UnitElementComponent>(entity))
        {
            request.HasElementSnapshot = true;
            request.ElementSnapshot = entityManager.GetComponentData<UnitElementComponent>(entity);
        }

        if (entityManager.HasComponent<UnitSkillModifierRuntimeComponent>(entity))
            request.ModifierSnapshot = UnitSkillModifierUtility.CreateSnapshot(entityManager, entity);

        CaptureVariableTarget(entityManager, entity, request);
        return request;
    }

    private void CaptureVariableTarget(EntityManager entityManager, Entity entity, SkillReleaseRequest request)
    {
        if (!entityManager.HasComponent<UnitVariableComponent>(entity))
            return;

        UnitVariableComponent variables = entityManager.GetComponentObject<UnitVariableComponent>(entity);
        if (variables?.Values == null)
            return;

        if (variables.Values.TryGetValue(TargetPositionVariableKey, out UnitValue position))
        {
            switch (position.Type)
            {
                case UnitValueType.Float2:
                    request.HasTargetPosition = true;
                    request.TargetPosition = new float3(position.Float2.x, position.Float2.y, 0f);
                    break;

                case UnitValueType.Float3:
                    request.HasTargetPosition = true;
                    request.TargetPosition = position.Float3;
                    break;
            }
        }

        if (variables.Values.TryGetValue(TargetEntityVariableKey, out UnitValue target) &&
            target.Type == UnitValueType.Entity &&
            target.Entity != Entity.Null)
        {
            request.HasTargetEntity = true;
            request.TargetEntity = target.Entity;

            if (!request.HasTargetPosition &&
                entityManager.Exists(target.Entity) &&
                entityManager.HasComponent<LocalTransform>(target.Entity))
            {
                request.HasTargetPosition = true;
                request.TargetPosition = entityManager.GetComponentData<LocalTransform>(target.Entity).Position;
            }
        }
    }

    private static ComparatorFactory CreateExpressionFactory()
    {
        ComparatorFactory factory = new();
        ComparatorRegistry.RegisterAll(factory);
        return factory;
    }
}
