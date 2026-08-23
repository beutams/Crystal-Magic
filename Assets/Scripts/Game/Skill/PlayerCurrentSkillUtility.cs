using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;

public static class PlayerCurrentSkillUtility
{
    public static bool TrySetCurrentChainSlot(
        EntityManager entityManager,
        Entity entity,
        int chainId,
        int slotIndex)
    {
        if (!TryGetComponent(entityManager, entity, out PlayerCurrentSkillComponent component) ||
            !TryGetWorldSkillData(entityManager, out WorldSkillDataComponent worldSkillData) ||
            !worldSkillData.TryGetChainSlot(chainId, slotIndex, out _))
        {
            return false;
        }

        if (component.CurrentChainId != chainId || component.CurrentSlotIndex != slotIndex)
        {
            component.CurrentChainId = chainId;
            component.CurrentSlotIndex = slotIndex;
            component.PendingExtraModifiers = new SkillModifierSet();
        }

        return true;
    }

    public static bool TryGetCurrentSlot(
        EntityManager entityManager,
        Entity entity,
        out WorldSkillChainSlotData slot)
    {
        slot = default;
        return TryGetComponent(entityManager, entity, out PlayerCurrentSkillComponent component) &&
               component.CurrentChainId >= 0 &&
               component.CurrentSlotIndex >= 0 &&
               TryGetWorldSkillData(entityManager, out WorldSkillDataComponent worldSkillData) &&
               worldSkillData.TryGetChainSlot(component.CurrentChainId, component.CurrentSlotIndex, out slot);
    }

    public static bool TryGetCurrentSkillId(EntityManager entityManager, Entity entity, out int skillId)
    {
        skillId = -1;
        if (!TryGetCurrentSlot(entityManager, entity, out WorldSkillChainSlotData slot) || slot.SkillId < 0)
            return false;

        skillId = slot.SkillId;
        return true;
    }

    public static bool TryGetCurrentAdditionId(EntityManager entityManager, Entity entity, out int additionId)
    {
        additionId = -1;
        if (!TryGetCurrentSlot(entityManager, entity, out WorldSkillChainSlotData slot) || slot.SkillAdditionId < 0)
            return false;

        additionId = slot.SkillAdditionId;
        return true;
    }

    public static bool AddPendingExtraModifier(EntityManager entityManager, Entity entity, SkillModifierEntry entry)
    {
        if (!TryGetComponent(entityManager, entity, out PlayerCurrentSkillComponent component) ||
            !TryGetCurrentSlot(entityManager, entity, out _) ||
            !System.Enum.IsDefined(typeof(SkillModifierChannel), entry.Channel) ||
            SkillModifierChannelUtility.IsInternalChannel(entry.Channel))
        {
            return false;
        }

        component.PendingExtraModifiers ??= new SkillModifierSet();
        component.PendingExtraModifiers.Add(entry);
        return true;
    }

    public static SkillModifierSet ConsumePendingExtraModifiers(EntityManager entityManager, Entity entity)
    {
        if (!TryGetComponent(entityManager, entity, out PlayerCurrentSkillComponent component))
            return new SkillModifierSet();

        SkillModifierSet result = component.PendingExtraModifiers?.Clone() ?? new SkillModifierSet();
        component.PendingExtraModifiers = new SkillModifierSet();
        return result;
    }

    public static void ClearCurrentChainSlot(EntityManager entityManager, Entity entity)
    {
        if (!TryGetComponent(entityManager, entity, out PlayerCurrentSkillComponent component))
            return;

        component.CurrentChainId = -1;
        component.CurrentSlotIndex = -1;
        component.PendingExtraModifiers = new SkillModifierSet();
    }

    private static bool TryGetComponent(EntityManager entityManager, Entity entity, out PlayerCurrentSkillComponent component)
    {
        component = null;
        if (entity == Entity.Null || !entityManager.Exists(entity) || !entityManager.HasComponent<PlayerCurrentSkillComponent>(entity))
            return false;

        component = entityManager.GetComponentObject<PlayerCurrentSkillComponent>(entity);
        return component != null;
    }

    private static bool TryGetWorldSkillData(EntityManager entityManager, out WorldSkillDataComponent data)
    {
        data = null;
        return WorldStateUtility.TryGetEntity(entityManager, out Entity worldEntity) &&
               entityManager.HasComponent<WorldSkillDataComponent>(worldEntity) &&
               (data = entityManager.GetComponentObject<WorldSkillDataComponent>(worldEntity)) != null;
    }
}
