using Unity.Entities;

[System.Flags]
public enum GameGateMask : byte
{
    None = 0,
    Simulation = 1 << 0,
    PlayerInput = 1 << 1,
    UIInput = 1 << 2,
}

public struct WorldStateComponent : IComponentData
{
    public uint CurrentFrame;
    public GameGateMask GateMask;

    public bool IsSimulationLocked => (GateMask & GameGateMask.Simulation) != 0;
    public bool IsPlayerInputLocked => (GateMask & GameGateMask.PlayerInput) != 0;
    public bool IsUIInputLocked => (GateMask & GameGateMask.UIInput) != 0;
}

public static class WorldStateUtility
{
    public static bool TryGetEntity(EntityManager entityManager, out Entity entity)
    {
        using EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<WorldStateComponent>());
        if (query.IsEmptyIgnoreFilter)
        {
            entity = Entity.Null;
            return false;
        }

        entity = query.GetSingletonEntity();
        return true;
    }
}
