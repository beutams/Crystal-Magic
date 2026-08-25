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

        if (interactable.Data.Kind == InteractionKind.Treasure &&
            (!entityManager.HasComponent<TreasureComponent>(target) ||
             entityManager.GetComponentData<TreasureComponent>(target).IsOpened != 0))
        {
            return false;
        }

        if (interactable.Data.Kind == InteractionKind.Npc && interactable.Data.DataId < 0)
            return false;

        if (entityManager.HasComponent<DungeonExitComponent>(target) &&
            entityManager.GetComponentData<DungeonExitComponent>(target).IsOpen == 0)
        {
            return false;
        }

        return true;
    }

    public static bool IsSameData(in UnitInteractionData left, in UnitInteractionData right)
    {
        return left.Kind == right.Kind &&
               left.DataId == right.DataId &&
               left.Amount == right.Amount &&
               left.Variant == right.Variant;
    }

    public static int GetPriority(InteractionKind kind)
    {
        return kind switch
        {
            InteractionKind.Drop => 0,
            InteractionKind.Treasure => 1,
            InteractionKind.Npc => 2,
            _ => int.MaxValue,
        };
    }
}
