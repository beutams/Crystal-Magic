using CrystalMagic.Game.Data;
using Unity.Entities;

public static class PlayerCurrentSkillUtility
{
    public static bool TrySetCurrentChainSlot(
        EntityManager entityManager,
        Entity entity,
        int chainId,
        int slotIndex)
    {
        if (!TryGetComponent(entityManager, entity, out PlayerCurrentSkillComponent component) ||
            !TryGetChainSlot(entityManager, entity, chainId, slotIndex, out _))
        {
            return false;
        }

        if (component.CurrentChainId != chainId || component.CurrentSlotIndex != slotIndex)
        {
            component.CurrentChainId = chainId;
            component.CurrentSlotIndex = slotIndex;
            component.PendingExtraModifiers = default;
            entityManager.SetComponentData(entity, component);
        }

        return true;
    }

    public static bool TryGetCurrentSlot(
        EntityManager entityManager,
        Entity entity,
        out PlayerSkillChainSlotElement slot)
    {
        slot = default;
        return TryGetComponent(entityManager, entity, out PlayerCurrentSkillComponent component) &&
               component.CurrentChainId >= 0 &&
               component.CurrentSlotIndex >= 0 &&
               TryGetChainSlot(
                   entityManager,
                   entity,
                   component.CurrentChainId,
                   component.CurrentSlotIndex,
                   out slot);
    }

    public static bool TryGetCurrentSkillId(EntityManager entityManager, Entity entity, out int skillId)
    {
        skillId = -1;
        if (!TryGetCurrentSlot(entityManager, entity, out PlayerSkillChainSlotElement slot) || slot.SkillId < 0)
            return false;

        skillId = slot.SkillId;
        return true;
    }

    public static bool TryGetCurrentAdditionId(EntityManager entityManager, Entity entity, out int additionId)
    {
        additionId = -1;
        if (!TryGetCurrentSlot(entityManager, entity, out PlayerSkillChainSlotElement slot) || slot.SkillAdditionId < 0)
            return false;

        additionId = slot.SkillAdditionId;
        return true;
    }

    public static bool TryGetCurrentInputType(
        EntityManager entityManager,
        Entity entity,
        out SkillInputType inputType)
    {
        inputType = SkillInputType.None;
        if (!TryGetCurrentSkillId(entityManager, entity, out int skillId) ||
            !PlayerSkillDefinitionRegistryUtility.TryGet(
                entityManager,
                out BlobAssetReference<PlayerSkillDefinitionRegistryBlob> registry) ||
            !PlayerSkillDefinitionRegistryUtility.TryGetSkill(
                in registry,
                skillId,
                out PlayerSkillDefinitionBlob skill))
        {
            return false;
        }

        inputType = skill.InputType;
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

        component.PendingExtraModifiers.Add(entry);
        entityManager.SetComponentData(entity, component);
        return true;
    }

    public static SkillModifierSet ConsumePendingExtraModifiers(EntityManager entityManager, Entity entity)
    {
        if (!TryGetComponent(entityManager, entity, out PlayerCurrentSkillComponent component))
            return default;

        SkillModifierSet result = component.PendingExtraModifiers;
        component.PendingExtraModifiers = default;
        entityManager.SetComponentData(entity, component);
        return result;
    }

    public static void ClearCurrentChainSlot(EntityManager entityManager, Entity entity)
    {
        if (!TryGetComponent(entityManager, entity, out PlayerCurrentSkillComponent component))
            return;

        component.CurrentChainId = -1;
        component.CurrentSlotIndex = -1;
        component.PendingExtraModifiers = default;
        entityManager.SetComponentData(entity, component);
    }

    private static bool TryGetComponent(
        EntityManager entityManager,
        Entity entity,
        out PlayerCurrentSkillComponent component)
    {
        component = default;
        if (entity == Entity.Null ||
            !entityManager.Exists(entity) ||
            !entityManager.HasComponent<PlayerCurrentSkillComponent>(entity))
        {
            return false;
        }

        component = entityManager.GetComponentData<PlayerCurrentSkillComponent>(entity);
        return true;
    }

    private static bool TryGetChainSlot(
        EntityManager entityManager,
        Entity entity,
        int chainId,
        int slotIndex,
        out PlayerSkillChainSlotElement slot)
    {
        slot = default;
        if (entity == Entity.Null ||
            !entityManager.Exists(entity) ||
            !entityManager.HasBuffer<PlayerSkillChainElement>(entity) ||
            !entityManager.HasBuffer<PlayerSkillChainSlotElement>(entity))
        {
            return false;
        }

        DynamicBuffer<PlayerSkillChainElement> chains = entityManager.GetBuffer<PlayerSkillChainElement>(entity);
        DynamicBuffer<PlayerSkillChainSlotElement> slots = entityManager.GetBuffer<PlayerSkillChainSlotElement>(entity);
        return PlayerSkillChainSource.TryGetChainSlot(chains, slots, chainId, slotIndex, out slot);
    }
}
