using Unity.Entities;

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
