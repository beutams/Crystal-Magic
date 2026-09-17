using System.Collections.Generic;
using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public sealed class SkillProjectileSpawnRequest
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
    public SkillContent Context;
    public List<ConditionConfig> CollisionTargetConditions;
    public EffectData[] OnCollisionEffects;
    public EffectData[] OnDestroyEffects;
}

public sealed class SkillProjectileSpawnQueueComponent : IComponentData
{
    public readonly Queue<SkillProjectileSpawnRequest> Requests = new();
}

public static class SkillProjectileSpawnQueueUtility
{
    public static SkillProjectileSpawnQueueComponent GetOrCreate(EntityManager entityManager)
    {
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SkillProjectileSpawnQueueComponent>());
        if (!query.IsEmptyIgnoreFilter)
            return entityManager.GetComponentObject<SkillProjectileSpawnQueueComponent>(query.GetSingletonEntity());

        Entity entity = entityManager.CreateEntity();
        SkillProjectileSpawnQueueComponent queue = new();
        entityManager.AddComponentObject(entity, queue);
        return queue;
    }

    public static bool TryGet(EntityManager entityManager, out SkillProjectileSpawnQueueComponent queue)
    {
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SkillProjectileSpawnQueueComponent>());
        if (query.IsEmptyIgnoreFilter)
        {
            queue = null;
            return false;
        }

        queue = entityManager.GetComponentObject<SkillProjectileSpawnQueueComponent>(query.GetSingletonEntity());
        return queue != null;
    }
}

public struct SkillProjectileHitEntityElement : IBufferElementData
{
    public Entity Value;
}

public sealed class SkillProjectilePayloadComponent : IComponentData
{
    public SkillContent Context;
    public List<ConditionConfig> CollisionTargetConditions;
    public EffectData[] OnCollisionEffects;
    public EffectData[] OnDestroyEffects;
}

public struct SkillProjectileVisualLinkComponent : IComponentData
{
    public Entity VisualEntity;
}
