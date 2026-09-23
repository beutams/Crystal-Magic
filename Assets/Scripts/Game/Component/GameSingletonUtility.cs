using CrystalMagic.Core;
using Unity.Entities;

public static class GameSingletonUtility
{
    public static void Create(
        EntityManager entityManager,
        GameWorldRole role,
        GameSceneMode sceneMode)
    {
        Entity contextEntity = entityManager.CreateEntity(typeof(GameWorldContextComponent));
        entityManager.SetComponentData(contextEntity, new GameWorldContextComponent
        {
            Role = role,
            SceneMode = sceneMode,
        });
        entityManager.SetName(contextEntity, nameof(GameWorldContextComponent));

        Entity variableEntity = entityManager.CreateEntity(typeof(WorldVariableComponent));
        entityManager.AddBuffer<WorldVariableElement>(variableEntity);
        entityManager.SetName(variableEntity, nameof(WorldVariableComponent));

        Entity interactionEntity = entityManager.CreateEntity(typeof(GameInteractionComponent));
        entityManager.AddBuffer<InteractionTransactionElement>(interactionEntity);
        entityManager.SetName(interactionEntity, nameof(GameInteractionComponent));

        Entity skillRegistryEntity = entityManager.CreateEntity(typeof(PlayerSkillDefinitionRegistryComponent));
        entityManager.SetName(skillRegistryEntity, nameof(PlayerSkillDefinitionRegistryComponent));

        if (role != GameWorldRole.Server)
        {
            Entity gameGateEntity = entityManager.CreateEntity(typeof(GameGateStateComponent));
            entityManager.SetName(gameGateEntity, nameof(GameGateStateComponent));
        }
    }

    public static Entity GetEntity<T>(EntityManager entityManager)
        where T : unmanaged, IComponentData
    {
        using EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
        return query.GetSingletonEntity();
    }

    public static T Get<T>(EntityManager entityManager)
        where T : unmanaged, IComponentData
    {
        using EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
        return query.GetSingleton<T>();
    }

    public static void Set<T>(EntityManager entityManager, T value)
        where T : unmanaged, IComponentData
    {
        entityManager.SetComponentData(GetEntity<T>(entityManager), value);
    }
}
