using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public static class SkillReleaseRequestUtility
{
    public static SkillReleaseRequest Create(
        EntityManager entityManager,
        Entity entity,
        int skillId,
        SkillModifierSet extraModifiers,
        float3 targetPosition,
        Entity targetEntity)
    {
        SkillReleaseRequest request = new()
        {
            SkillId = skillId,
            OriginEntity = entity,
            TargetPosition = targetPosition,
            HasTargetPosition = true,
            TargetEntity = targetEntity,
            HasTargetEntity = targetEntity != Entity.Null,
            ExtraModifiers = extraModifiers?.Clone() ?? new SkillModifierSet(),
        };

        if (entityManager.HasComponent<LocalTransform>(entity))
            request.OriginPosition = entityManager.GetComponentData<LocalTransform>(entity).Position;

        if (entityManager.HasComponent<UnitFacingComponent>(entity))
        {
            UnitFacingComponent facing = entityManager.GetComponentData<UnitFacingComponent>(entity);
            request.OriginFacing = math.normalizesafe(facing.Direction, new float2(1f, 0f));
        }

        return request;
    }
}
