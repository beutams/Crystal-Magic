using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using UnityEngine;

public static class PendingEffectExecutionQueueUtility
{
    public static PendingEffectExecutionQueueComponent GetOrCreate(EntityManager entityManager)
    {
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PendingEffectExecutionQueueComponent>());
        if (!query.IsEmptyIgnoreFilter)
        {
            Entity singletonEntity = query.GetSingletonEntity();
            return entityManager.GetComponentObject<PendingEffectExecutionQueueComponent>(singletonEntity);
        }

        Entity entity = entityManager.CreateEntity();
        PendingEffectExecutionQueueComponent queue = new();
        entityManager.AddComponentObject(entity, queue);
        return queue;
    }
}

public static class PersistentEffectUtility
{
    public static void AddEffect(PersistentEffectData data, SkillContent sourceContext, Vector3 releasePosition)
    {
        if (data == null || sourceContext == null)
            return;

        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
            return;

        PersistentEffectQueueComponent queue = GetOrCreate(world.EntityManager);
        queue.Enqueue(new PersistentEffectRequest
        {
            Data = data,
            SourceContext = sourceContext,
            ReleasePosition = releasePosition,
        });
    }

    public static PersistentEffectQueueComponent GetOrCreate(EntityManager entityManager)
    {
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PersistentEffectQueueComponent>());
        if (!query.IsEmptyIgnoreFilter)
        {
            Entity singletonEntity = query.GetSingletonEntity();
            return entityManager.GetComponentObject<PersistentEffectQueueComponent>(singletonEntity);
        }

        Entity entity = entityManager.CreateEntity();
        PersistentEffectQueueComponent queue = new();
        entityManager.AddComponentObject(entity, queue);
        return queue;
    }
}
