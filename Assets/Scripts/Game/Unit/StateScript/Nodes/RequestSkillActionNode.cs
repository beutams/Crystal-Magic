using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[FactoryKey("RequestSkill", 11, "Request Skill")]
public sealed class RequestSkillActionNode : StateScriptActionNode
{
    private readonly RequestSkillActionNodeData _data;
    private readonly StateScriptOutputPort _output;

    public RequestSkillActionNode(RequestSkillActionNodeData data, StateScriptRuntime runtime)
        : base(data, runtime)
    {
        _data = data;
        AddInput("In", RequestSkill);
        _output = AddOutput("Out");
    }

    protected override bool OnBind(out string error)
    {
        if (_data.SkillId < 0)
        {
            error = "RequestSkill requires a valid SkillId.";
            return false;
        }

        if (!Runtime.EntityManager.HasComponent<UnitSkillReleaseComponent>(Runtime.Entity))
        {
            error = "RequestSkill requires UnitSkillReleaseComponent.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void RequestSkill()
    {
        EntityManager entityManager = Runtime.EntityManager;
        Entity entity = Runtime.Entity;
        if (!entityManager.HasComponent<UnitSkillReleaseComponent>(entity))
            return;

        UnitSkillReleaseComponent releaseComponent = entityManager.GetComponentObject<UnitSkillReleaseComponent>(entity);
        if (releaseComponent == null)
            return;

        SkillReleaseRequest request = CreateRequest(entityManager, entity);
        releaseComponent.PendingRequests.Add(request);
        _output.Pulse();
    }

    private SkillReleaseRequest CreateRequest(EntityManager entityManager, Entity entity)
    {
        SkillReleaseRequest request = new()
        {
            SkillId = _data.SkillId,
            SkillAdditionId = _data.SkillAdditionId,
            OriginEntity = entity,
        };

        if (entityManager.HasComponent<LocalTransform>(entity))
            request.OriginPosition = entityManager.GetComponentData<LocalTransform>(entity).Position;

        if (entityManager.HasComponent<UnitFacingComponent>(entity))
        {
            UnitFacingComponent facing = entityManager.GetComponentData<UnitFacingComponent>(entity);
            request.OriginFacing = math.normalizesafe(facing.Direction, new float2(1f, 0f));
        }

        if (entityManager.HasComponent<UnitAttackComponent>(entity))
        {
            request.HasAttackSnapshot = true;
            request.AttackSnapshot = entityManager.GetComponentData<UnitAttackComponent>(entity);
        }

        if (entityManager.HasComponent<UnitElementComponent>(entity))
        {
            request.HasElementSnapshot = true;
            request.ElementSnapshot = entityManager.GetComponentData<UnitElementComponent>(entity);
        }

        if (entityManager.HasComponent<UnitSkillModifierRuntimeComponent>(entity))
            request.ModifierSnapshot = UnitSkillModifierUtility.CreateSnapshot(entityManager, entity);

        CaptureTarget(entityManager, entity, request);
        return request;
    }

    private void CaptureTarget(EntityManager entityManager, Entity entity, SkillReleaseRequest request)
    {
        switch (_data.TargetMode)
        {
            case SkillReleaseTargetMode.Self:
                request.HasTargetEntity = true;
                request.TargetEntity = entity;
                request.HasTargetPosition = true;
                request.TargetPosition = request.OriginPosition;
                break;

            case SkillReleaseTargetMode.Variables:
                CaptureVariableTarget(entityManager, entity, request);
                break;

            case SkillReleaseTargetMode.PerceptionTarget:
                if (entityManager.HasComponent<UnitPerceptionComponent>(entity))
                {
                    UnitPerceptionComponent perception = entityManager.GetComponentData<UnitPerceptionComponent>(entity);
                    if (perception.HasTarget)
                    {
                        request.HasTargetEntity = true;
                        request.TargetEntity = perception.TargetEntity;
                        request.HasTargetPosition = true;
                        request.TargetPosition = new float3(perception.TargetPosition.x, perception.TargetPosition.y, 0f);
                    }
                }
                break;
        }
    }

    private void CaptureVariableTarget(EntityManager entityManager, Entity entity, SkillReleaseRequest request)
    {
        if (!entityManager.HasComponent<UnitVariableComponent>(entity))
            return;

        UnitVariableComponent variables = entityManager.GetComponentObject<UnitVariableComponent>(entity);
        if (variables?.Values == null)
            return;

        if (variables.Values.TryGetValue(_data.TargetPositionVariableKey ?? string.Empty, out UnitValue position))
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

        if (variables.Values.TryGetValue(_data.TargetEntityVariableKey ?? string.Empty, out UnitValue target) &&
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
}
