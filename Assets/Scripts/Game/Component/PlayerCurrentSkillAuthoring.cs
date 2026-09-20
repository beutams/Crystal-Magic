using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

public sealed class PlayerCurrentSkillAuthoring : MonoBehaviour
{
    private sealed class Baker : Baker<PlayerCurrentSkillAuthoring>
    {
        public override void Bake(PlayerCurrentSkillAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new PlayerCurrentSkillComponent
            {
                CurrentChainId = -1,
                CurrentSlotIndex = -1,
            });
        }
    }
}

public struct PlayerCurrentSkillComponent : IComponentData
{
    public int CurrentChainId;
    public int CurrentSlotIndex;
    public SkillModifierSet PendingExtraModifiers;
}

[UnitSourceProvider(typeof(PlayerCurrentSkillComponent), typeof(PlayerCurrentSkillAuthoring))]
public static class PlayerCurrentSkillSource
{
    [UnitSourceGet(0, "player.skill.currentChainId", UnitValueCategory.Number)]
    [UnitSourceGet(1, "player.skill.currentSlotIndex", UnitValueCategory.Number)]
    public static bool TryGetStored(
        int operation,
        in PlayerCurrentSkillComponent component,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = operation switch
        {
            0 => UnitSourceValue.FromInt(component.CurrentChainId),
            1 => UnitSourceValue.FromInt(component.CurrentSlotIndex),
            _ => UnitSourceValue.None,
        };
        return result.Type != UnitValueType.None;
    }

    [UnitSourceGet(2, "player.skill.currentSkillId", UnitValueCategory.Number)]
    [UnitSourceGet(3, "player.skill.currentInputType", UnitValueCategory.Number)]
    [UnitSourceGet(4, "player.skill.currentAdditionId", UnitValueCategory.Number)]
    [UnitSourceGet(5, "player.skill.hasCurrentSkill", UnitValueCategory.Bool)]
    public static bool TryGetDerived(
        int operation,
        UnitSourceAccessContext context,
        in ComponentLookup<PlayerCurrentSkillComponent> currentSkillLookup,
        in ComponentLookup<PlayerSkillRuntimeDataComponent> runtimeLookup,
        in BufferLookup<PlayerSkillChainElement> chainLookup,
        in BufferLookup<PlayerSkillChainSlotElement> slotLookup,
        in ComponentLookup<PlayerSkillDefinitionRegistryComponent> registryLookup,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = default;
        if (!currentSkillLookup.HasComponent(context.TargetEntity))
            return false;

        if (!TryGetCurrentSlot(
                context.TargetEntity,
                in currentSkillLookup,
                in runtimeLookup,
                in chainLookup,
                in slotLookup,
                out PlayerSkillChainSlotElement slot))
        {
            if (operation == 5)
            {
                result = UnitSourceValue.FromBool(false);
                return true;
            }
            return false;
        }

        switch (operation)
        {
            case 2 when slot.SkillId >= 0:
                result = UnitSourceValue.FromInt(slot.SkillId);
                return true;
            case 3 when PlayerSkillRuntimeDataSource.TryGetSkill(
                context.GlobalEntity,
                slot.SkillId,
                in registryLookup,
                out PlayerSkillDefinitionBlob skill):
                result = UnitSourceValue.FromInt((int)skill.InputType);
                return true;
            case 4 when slot.SkillAdditionId >= 0:
                result = UnitSourceValue.FromInt(slot.SkillAdditionId);
                return true;
            case 5:
                result = UnitSourceValue.FromBool(slot.SkillId >= 0);
                return true;
            default:
                return false;
        }
    }

    [UnitSourceSet(0, "player.skill.currentChainSlot.set", UnitValueCategory.Number, UnitValueCategory.Number,
        ParameterNames = new[] { "ChainId", "SlotIndex" })]
    [UnitSourceSet(2, "player.skill.pendingExtraModifiers.add", UnitValueCategory.Number, UnitValueCategory.Number,
        UnitValueCategory.Number, ParameterNames = new[] { "Channel", "Factor", "Bonus" })]
    public static bool TrySet(
        int operation,
        UnitSourceAccessContext context,
        ref ComponentLookup<PlayerCurrentSkillComponent> currentSkillLookup,
        in ComponentLookup<PlayerSkillRuntimeDataComponent> runtimeLookup,
        in BufferLookup<PlayerSkillChainElement> chainLookup,
        in BufferLookup<PlayerSkillChainSlotElement> slotLookup,
        in ComponentLookup<PlayerSkillDefinitionRegistryComponent> registryLookup,
        in UnitSourceArguments arguments)
    {
        if (!currentSkillLookup.TryGetComponent(
                context.TargetEntity,
                out PlayerCurrentSkillComponent component))
        {
            return false;
        }

        switch (operation)
        {
            case 0 when arguments.TryGetInt(0, out int chainId) &&
                         arguments.TryGetInt(1, out int slotIndex) &&
                         TryGetRuntimeBuffers(
                             context.TargetEntity,
                             in runtimeLookup,
                             in chainLookup,
                             in slotLookup,
                             out DynamicBuffer<PlayerSkillChainElement> chains,
                             out DynamicBuffer<PlayerSkillChainSlotElement> slots) &&
                         PlayerSkillRuntimeDataSource.TryGetChainSlot(
                             chains,
                             slots,
                             chainId,
                             slotIndex,
                             out _):
            {
                if (component.CurrentChainId != chainId || component.CurrentSlotIndex != slotIndex)
                {
                    component.CurrentChainId = chainId;
                    component.CurrentSlotIndex = slotIndex;
                    component.PendingExtraModifiers = default;
                    currentSkillLookup[context.TargetEntity] = component;
                }
                return true;
            }
            case 2 when arguments.TryGetInt(0, out int rawChannel) &&
                         arguments.TryGetNumber(1, out float factor) &&
                         arguments.TryGetNumber(2, out float bonus) &&
                         TryGetCurrentSlot(
                             context.TargetEntity,
                             in currentSkillLookup,
                             in runtimeLookup,
                             in chainLookup,
                             in slotLookup,
                             out _) &&
                         registryLookup.TryGetComponent(
                             context.GlobalEntity,
                             out PlayerSkillDefinitionRegistryComponent registry) &&
                         PlayerSkillDefinitionRegistryUtility.TryGetModifierMinimumFactor(
                             in registry.Value,
                             (SkillModifierChannel)rawChannel,
                             out float minimumFactor):
            {
                SkillModifierEntry entry = new()
                {
                    Channel = (SkillModifierChannel)rawChannel,
                    Factor = factor,
                    Bonus = bonus,
                };
                component.PendingExtraModifiers.Add(in entry, 1, minimumFactor);
                currentSkillLookup[context.TargetEntity] = component;
                return true;
            }
            default:
                return false;
        }
    }

    [UnitSourceSet(1, "player.skill.currentChainSlot.clear", UnitValueCategory.Bool,
        ParameterNames = new[] { "Clear" })]
    public static bool TryClear(
        int operation,
        ref PlayerCurrentSkillComponent component,
        in UnitSourceArguments arguments)
    {
        if (operation != 1 || !arguments.TryGetBool(0, out bool clear) || !clear)
            return false;

        component.CurrentChainId = -1;
        component.CurrentSlotIndex = -1;
        component.PendingExtraModifiers = default;
        return true;
    }

    private static bool TryGetCurrentSlot(
        Entity entity,
        in ComponentLookup<PlayerCurrentSkillComponent> currentSkillLookup,
        in ComponentLookup<PlayerSkillRuntimeDataComponent> runtimeLookup,
        in BufferLookup<PlayerSkillChainElement> chainLookup,
        in BufferLookup<PlayerSkillChainSlotElement> slotLookup,
        out PlayerSkillChainSlotElement slot)
    {
        slot = default;
        return currentSkillLookup.TryGetComponent(entity, out PlayerCurrentSkillComponent current) &&
               current.CurrentChainId >= 0 &&
               current.CurrentSlotIndex >= 0 &&
               TryGetRuntimeBuffers(
                   entity,
                   in runtimeLookup,
                   in chainLookup,
                   in slotLookup,
                   out DynamicBuffer<PlayerSkillChainElement> chains,
                   out DynamicBuffer<PlayerSkillChainSlotElement> slots) &&
               PlayerSkillRuntimeDataSource.TryGetChainSlot(
                   chains,
                   slots,
                   current.CurrentChainId,
                   current.CurrentSlotIndex,
                   out slot);
    }

    private static bool TryGetRuntimeBuffers(
        Entity entity,
        in ComponentLookup<PlayerSkillRuntimeDataComponent> runtimeLookup,
        in BufferLookup<PlayerSkillChainElement> chainLookup,
        in BufferLookup<PlayerSkillChainSlotElement> slotLookup,
        out DynamicBuffer<PlayerSkillChainElement> chains,
        out DynamicBuffer<PlayerSkillChainSlotElement> slots)
    {
        chains = default;
        slots = default;
        return runtimeLookup.HasComponent(entity) &&
               chainLookup.TryGetBuffer(entity, out chains) &&
               slotLookup.TryGetBuffer(entity, out slots);
    }
}
