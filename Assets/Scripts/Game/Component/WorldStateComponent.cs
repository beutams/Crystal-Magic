using CrystalMagic.Core;
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
    public static Entity Create(
        EntityManager entityManager,
        GameWorldRole role,
        GameSceneMode sceneMode)
    {
        Entity entity = entityManager.CreateEntity(
            typeof(WorldStateComponent),
            typeof(WorldVariableComponent),
            typeof(InteractionCandidateComponent),
            typeof(PlayerSkillDefinitionRegistryComponent),
            typeof(GameWorldContextComponent));
        entityManager.AddBuffer<WorldVariableElement>(entity);
        entityManager.SetComponentData(entity, new GameWorldContextComponent
        {
            Role = role,
            SceneMode = sceneMode,
        });
        entityManager.SetName(entity, "WorldEntity");
        return entity;
    }

    public static Entity GetEntity(EntityManager entityManager)
    {
        using EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<WorldStateComponent>());
        return query.GetSingletonEntity();
    }
}
