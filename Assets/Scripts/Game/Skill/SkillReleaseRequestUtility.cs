using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public static class SkillReleaseRequestUtility
{
    private const string TargetPositionVariableKey = "skill.targetPosition";
    private const string TargetEntityVariableKey = "skill.targetEntity";

    public static SkillReleaseRequest Create(
        EntityManager entityManager,
        Entity entity,
        int skillId,
        SkillModifierSet extraModifiers)
    {
        SkillReleaseRequest request = new()
        {
            SkillId = skillId,
            OriginEntity = entity,
            ExtraModifiers = extraModifiers?.Clone() ?? new SkillModifierSet(),
        };

        if (entityManager.HasComponent<LocalTransform>(entity))
            request.OriginPosition = entityManager.GetComponentData<LocalTransform>(entity).Position;

        if (entityManager.HasComponent<UnitFacingComponent>(entity))
        {
            UnitFacingComponent facing = entityManager.GetComponentData<UnitFacingComponent>(entity);
            request.OriginFacing = math.normalizesafe(facing.Direction, new float2(1f, 0f));
        }

        CaptureVariableTarget(entityManager, entity, request);
        return request;
    }

    private static void CaptureVariableTarget(EntityManager entityManager, Entity entity, SkillReleaseRequest request)
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
}
