using Unity.Entities;

public static class GameInteractionTargetUtility
{
    public static bool IsAvailable(EntityManager entityManager, Entity target, in UnitInteractableComponent interactable)
    {
        if (target == Entity.Null || !entityManager.Exists(target) || interactable.IsEnabled == 0 || !interactable.Data.IsValid)
            return false;

        if (entityManager.HasComponent<DestroyEntityFlag>(target) &&
            entityManager.IsComponentEnabled<DestroyEntityFlag>(target))
        {
            return false;
        }

        if (interactable.Data.Kind == InteractionKind.Npc && interactable.Data.DataId < 0)
            return false;

        return true;
    }

}
