using Unity.Entities;

public struct WorldStateComponent : IComponentData
{
}

public static class WorldStateUtility
{
    public static bool TryGetEntity(EntityManager entityManager, out Entity entity)
    {
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<WorldStateComponent>());
        if (query.IsEmptyIgnoreFilter)
        {
            entity = Entity.Null;
            return false;
        }

        entity = query.GetSingletonEntity();
        return true;
    }
}
