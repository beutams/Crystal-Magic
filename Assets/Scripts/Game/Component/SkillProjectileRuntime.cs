using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct SkillProjectileSpawnRequest : IBufferElementData
{
    public FixedString128Bytes ProjectileName;
    public FixedString128Bytes VisualPrefabName;
    public float3 StartPosition;
    public float3 Direction;
    public quaternion Rotation;
    public float VisualScale;
    public float3 VisualOffset;
    public float Speed;
    public float MaxRange;
    public float HitRadius;
    public byte CanPierce;
    public byte TriggerDestroyEffectsOnMaxRange;
    public EffectRequestContext Context;
    public byte ReleaseManagedContextOnFailure;
    public ConditionDataListId CollisionTargetConditionsId;
    public EffectDataListId OnCollisionEffectListId;
    public EffectDataListId OnDestroyEffectListId;
}

public struct SkillProjectileSpawnQueueComponent : IComponentData
{
}

public static class SkillProjectileSpawnQueueUtility
{
    public static Entity GetOrCreateEntity(EntityManager entityManager)
    {
        EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<SkillProjectileSpawnQueueComponent>());
        if (!query.IsEmptyIgnoreFilter)
            return query.GetSingletonEntity();

        Entity entity = entityManager.CreateEntity();
        entityManager.AddComponent<SkillProjectileSpawnQueueComponent>(entity);
        entityManager.AddBuffer<SkillProjectileSpawnRequest>(entity);
        return entity;
    }
}

public struct SkillProjectileHitEntityElement : IBufferElementData
{
    public Entity Value;
}

public struct SkillProjectilePayloadComponent : IComponentData
{
    public EffectRequestContext Context;
    public byte OwnsManagedContext;
    public ConditionDataListId CollisionTargetConditionsId;
    public EffectDataListId OnCollisionEffectListId;
    public EffectDataListId OnDestroyEffectListId;
}

public struct SkillProjectileVisualLinkComponent : IComponentData
{
    public Entity VisualEntity;
}
