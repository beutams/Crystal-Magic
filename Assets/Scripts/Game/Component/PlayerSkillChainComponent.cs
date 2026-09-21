using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;

[InternalBufferCapacity(4)]
public struct PlayerSkillChainElement : IBufferElementData
{
    public int SlotStart;
    public int SlotCount;
}

[InternalBufferCapacity(8)]
public struct PlayerSkillChainSlotElement : IBufferElementData
{
    public int SkillId;
    public int SkillAdditionId;
}

public static class PlayerSkillChainUtility
{
    public static void Initialize(EntityManager entityManager, Entity player, CharacterData characterData)
    {
        if (!entityManager.Exists(player) || characterData == null)
            return;

        characterData.Skills ??= new SkillCData();
        characterData.Skills.EnsureValid();

        if (!entityManager.HasComponent<PlayerInputComponent>(player))
            entityManager.AddComponentData(player, new PlayerInputComponent());
        if (!entityManager.HasComponent<PlayerPropCooldownComponent>(player))
            entityManager.AddComponentData(player, new PlayerPropCooldownComponent());

        Rebuild(entityManager, player, characterData);
    }

    public static void Rebuild(EntityManager entityManager, Entity player)
    {
        if (!GameRuntimeStateUtility.TryGetPlayerCharacterData(entityManager, player, out CharacterData characterData))
            return;

        if (!entityManager.HasComponent<PlayerInputComponent>(player) ||
            !entityManager.HasComponent<PlayerPropCooldownComponent>(player))
        {
            Initialize(entityManager, player, characterData);
            return;
        }

        Rebuild(entityManager, player, characterData);
    }

    public static void Clear(EntityManager entityManager, Entity player)
    {
        if (!entityManager.Exists(player))
            return;

        if (entityManager.HasBuffer<PlayerSkillChainElement>(player))
            entityManager.RemoveComponent<PlayerSkillChainElement>(player);
        if (entityManager.HasBuffer<PlayerSkillChainSlotElement>(player))
            entityManager.RemoveComponent<PlayerSkillChainSlotElement>(player);
    }

    private static void Rebuild(EntityManager entityManager, Entity player, CharacterData characterData)
    {
        PlayerInputComponent input = entityManager.GetComponentData<PlayerInputComponent>(player);
        int chainCount = characterData.Skills?.Chains?.Length ?? 0;
        int maxIndex = chainCount > 0 ? chainCount - 1 : 0;
        if (input.SkillChainIndex < 0 || input.SkillChainIndex > maxIndex)
        {
            input.SkillChainIndex = 0;
            input.NetworkDirty = 1;
            entityManager.SetComponentData(player, input);
        }

        // Complete every structural change before acquiring buffer handles. Adding the
        // second buffer would otherwise invalidate a handle acquired for the first one.
        if (!entityManager.HasBuffer<PlayerSkillChainElement>(player))
            entityManager.AddBuffer<PlayerSkillChainElement>(player);
        if (!entityManager.HasBuffer<PlayerSkillChainSlotElement>(player))
            entityManager.AddBuffer<PlayerSkillChainSlotElement>(player);

        DynamicBuffer<PlayerSkillChainElement> chains =
            entityManager.GetBuffer<PlayerSkillChainElement>(player);
        DynamicBuffer<PlayerSkillChainSlotElement> slots =
            entityManager.GetBuffer<PlayerSkillChainSlotElement>(player);
        chains.Clear();
        slots.Clear();

        SkillChainData[] sourceChains = characterData.Skills?.Chains;
        for (int chainId = 0; chainId < chainCount; chainId++)
        {
            SkillChainData sourceChain = sourceChains[chainId];
            int slotStart = slots.Length;
            int slotCount = sourceChain?.Slots?.Count ?? 0;
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                SkillChainSlotData sourceSlot = sourceChain.Slots[slotIndex];
                slots.Add(new PlayerSkillChainSlotElement
                {
                    SkillId = ResolveSkillId(DataComponent.Instance, sourceSlot?.SkillStoneItemId ?? -1),
                    SkillAdditionId = sourceSlot?.SkillAdditionId ?? -1,
                });
            }

            chains.Add(new PlayerSkillChainElement
            {
                SlotStart = slotStart,
                SlotCount = slotCount,
            });
        }
    }

    private static int ResolveSkillId(DataComponent dataComponent, int skillStoneItemId)
    {
        if (dataComponent == null || skillStoneItemId < 0)
            return -1;

        ItemData itemData = dataComponent.Get<ItemData>(skillStoneItemId);
        if (itemData == null || itemData.ItemType != ItemType.SkillStone || itemData.ExtraId < 0)
            return -1;

        return dataComponent.Get<SkillData>(itemData.ExtraId) != null ? itemData.ExtraId : -1;
    }
}

[UnitSourceProvider(typeof(PlayerInputComponent), typeof(PlayerCurrentSkillAuthoring))]
public static class PlayerSkillChainSource
{
    [UnitSourceGet(1, "player.skill.isCurrentChainEmpty", UnitValueCategory.Bool)]
    [UnitSourceGet(2, "player.skill.getCurrentChainLength", UnitValueCategory.Number)]
    [UnitSourceGet(3, "player.skill.getCurrentSkillId", UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "Slot Index" })]
    [UnitSourceGet(4, "player.skill.getCurrentSkillAdditionId", UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "Slot Index" })]
    [UnitSourceGet(5, "player.skill.hasCurrentSkillAt", UnitValueCategory.Bool, UnitValueCategory.Number, ParameterNames = new[] { "Slot Index" })]
    [UnitSourceGet(6, "player.skill.getCurrentSkillMpCost", UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "Slot Index" })]
    [UnitSourceGet(7, "player.skill.getCurrentSkillChantDuration", UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "Slot Index" })]
    [UnitSourceGet(8, "player.skill.getCurrentSkillRuntimeType", UnitValueCategory.String, UnitValueCategory.Number, ParameterNames = new[] { "Slot Index" })]
    [UnitSourceGet(9, "player.skill.isChainEmpty", UnitValueCategory.Bool, UnitValueCategory.Number, ParameterNames = new[] { "Chain ID" })]
    [UnitSourceGet(10, "player.skill.getChainLength", UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "Chain ID" })]
    [UnitSourceGet(11, "player.skill.getChainSkillId", UnitValueCategory.Number, UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "Chain ID", "Slot Index" })]
    [UnitSourceGet(12, "player.skill.getChainSkillAdditionId", UnitValueCategory.Number, UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "Chain ID", "Slot Index" })]
    [UnitSourceGet(13, "player.skill.hasSkill", UnitValueCategory.Bool, UnitValueCategory.Number, ParameterNames = new[] { "Skill ID" })]
    [UnitSourceGet(14, "player.skill.getSkillMpCost", UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "Skill ID" })]
    [UnitSourceGet(15, "player.skill.getSkillChantDuration", UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "Skill ID" })]
    [UnitSourceGet(16, "player.skill.getSkillCastingMoveMultiplier", UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "Skill ID" })]
    [UnitSourceGet(17, "player.skill.getSkillRuntimeType", UnitValueCategory.String, UnitValueCategory.Number, ParameterNames = new[] { "Skill ID" })]
    [UnitSourceGet(18, "player.skill.getInputType", UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "Skill ID" })]
    public static bool TryGet(
        int operation,
        UnitSourceAccessContext context,
        in ComponentLookup<PlayerInputComponent> inputLookup,
        in BufferLookup<PlayerSkillChainElement> chainLookup,
        in BufferLookup<PlayerSkillChainSlotElement> slotLookup,
        in ComponentLookup<PlayerSkillDefinitionRegistryComponent> registryLookup,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = default;
        if (!TryGetRuntimeData(
                context.TargetEntity,
                in inputLookup,
                in chainLookup,
                in slotLookup,
                out PlayerInputComponent input,
                out DynamicBuffer<PlayerSkillChainElement> chains,
                out DynamicBuffer<PlayerSkillChainSlotElement> slots))
        {
            return false;
        }

        switch (operation)
        {
            case 1:
                result = UnitSourceValue.FromBool(IsChainEmpty(chains, input.SkillChainIndex));
                return true;
            case 2:
                if (!TryGetChainLength(chains, input.SkillChainIndex, out int currentLength))
                    return false;
                result = UnitSourceValue.FromInt(currentLength);
                return true;
            case 3:
                if (!TryGetCurrentChainSlot(input, chains, slots, in arguments, out PlayerSkillChainSlotElement currentIdSlot))
                    return false;
                result = UnitSourceValue.FromInt(currentIdSlot.SkillId);
                return true;
            case 4:
                if (!TryGetCurrentChainSlot(input, chains, slots, in arguments, out PlayerSkillChainSlotElement currentAdditionSlot))
                    return false;
                result = UnitSourceValue.FromInt(currentAdditionSlot.SkillAdditionId);
                return true;
            case 5:
                result = UnitSourceValue.FromBool(
                    TryGetCurrentChainSlot(input, chains, slots, in arguments, out PlayerSkillChainSlotElement currentSlot) &&
                    TryGetSkill(context.SingletonEntity, currentSlot.SkillId, in registryLookup, out _));
                return true;
            case 6:
                if (!TryGetCurrentSkill(context.SingletonEntity, input, chains, slots, in registryLookup, in arguments, out PlayerSkillDefinitionBlob currentMpSkill))
                    return false;
                result = UnitSourceValue.FromInt(currentMpSkill.MpCost);
                return true;
            case 7:
                if (!TryGetCurrentSkill(context.SingletonEntity, input, chains, slots, in registryLookup, in arguments, out PlayerSkillDefinitionBlob currentChantSkill))
                    return false;
                result = UnitSourceValue.FromFloat(currentChantSkill.ChantDuration);
                return true;
            case 8:
                if (!TryGetCurrentSkill(context.SingletonEntity, input, chains, slots, in registryLookup, in arguments, out PlayerSkillDefinitionBlob currentRuntimeSkill) ||
                    currentRuntimeSkill.RuntimeTypeValid == 0)
                {
                    return false;
                }
                result = UnitSourceValue.FromString(in currentRuntimeSkill.RuntimeType);
                return true;
            case 9:
                if (!arguments.TryGetInt(0, out int emptyChainId))
                    return false;
                result = UnitSourceValue.FromBool(IsChainEmpty(chains, emptyChainId));
                return true;
            case 10:
                if (!arguments.TryGetInt(0, out int lengthChainId) ||
                    !TryGetChainLength(chains, lengthChainId, out int chainLength))
                {
                    return false;
                }
                result = UnitSourceValue.FromInt(chainLength);
                return true;
            case 11:
                if (!TryGetChainSlot(chains, slots, in arguments, out PlayerSkillChainSlotElement skillSlot))
                    return false;
                result = UnitSourceValue.FromInt(skillSlot.SkillId);
                return true;
            case 12:
                if (!TryGetChainSlot(chains, slots, in arguments, out PlayerSkillChainSlotElement additionSlot))
                    return false;
                result = UnitSourceValue.FromInt(additionSlot.SkillAdditionId);
                return true;
            case 13:
                if (!arguments.TryGetInt(0, out int hasSkillId))
                    return false;
                result = UnitSourceValue.FromBool(TryGetSkill(context.SingletonEntity, hasSkillId, in registryLookup, out _));
                return true;
            case 14:
                if (!TryGetSkill(context.SingletonEntity, in registryLookup, in arguments, out PlayerSkillDefinitionBlob mpSkill))
                    return false;
                result = UnitSourceValue.FromInt(mpSkill.MpCost);
                return true;
            case 15:
                if (!TryGetSkill(context.SingletonEntity, in registryLookup, in arguments, out PlayerSkillDefinitionBlob chantSkill))
                    return false;
                result = UnitSourceValue.FromFloat(chantSkill.ChantDuration);
                return true;
            case 16:
                if (!TryGetSkill(context.SingletonEntity, in registryLookup, in arguments, out PlayerSkillDefinitionBlob moveSkill))
                    return false;
                result = UnitSourceValue.FromFloat(moveSkill.CastingMoveMultiplier);
                return true;
            case 17:
                if (!TryGetSkill(context.SingletonEntity, in registryLookup, in arguments, out PlayerSkillDefinitionBlob runtimeSkill) ||
                    runtimeSkill.RuntimeTypeValid == 0)
                {
                    return false;
                }
                result = UnitSourceValue.FromString(in runtimeSkill.RuntimeType);
                return true;
            case 18:
                if (!TryGetSkill(context.SingletonEntity, in registryLookup, in arguments, out PlayerSkillDefinitionBlob inputSkill))
                    return false;
                result = UnitSourceValue.FromInt((int)inputSkill.InputType);
                return true;
            default:
                return false;
        }
    }

    public static bool TryGetRuntimeData(
        Entity entity,
        in ComponentLookup<PlayerInputComponent> inputLookup,
        in BufferLookup<PlayerSkillChainElement> chainLookup,
        in BufferLookup<PlayerSkillChainSlotElement> slotLookup,
        out PlayerInputComponent input,
        out DynamicBuffer<PlayerSkillChainElement> chains,
        out DynamicBuffer<PlayerSkillChainSlotElement> slots)
    {
        input = default;
        chains = default;
        slots = default;
        return inputLookup.TryGetComponent(entity, out input) &&
               chainLookup.TryGetBuffer(entity, out chains) &&
               slotLookup.TryGetBuffer(entity, out slots);
    }

    public static bool TryGetChainSlot(
        in DynamicBuffer<PlayerSkillChainElement> chains,
        in DynamicBuffer<PlayerSkillChainSlotElement> slots,
        int chainId,
        int slotIndex,
        out PlayerSkillChainSlotElement slot)
    {
        slot = default;
        if (chainId < 0 || chainId >= chains.Length || slotIndex < 0)
            return false;

        PlayerSkillChainElement chain = chains[chainId];
        if (slotIndex >= chain.SlotCount)
            return false;

        int absoluteIndex = chain.SlotStart + slotIndex;
        if (absoluteIndex < 0 || absoluteIndex >= slots.Length)
            return false;

        slot = slots[absoluteIndex];
        return true;
    }

    public static bool TryGetSkill(
        Entity registryEntity,
        int skillId,
        in ComponentLookup<PlayerSkillDefinitionRegistryComponent> registryLookup,
        out PlayerSkillDefinitionBlob skill)
    {
        skill = default;
        return registryLookup.TryGetComponent(registryEntity, out PlayerSkillDefinitionRegistryComponent component) &&
               PlayerSkillDefinitionRegistryUtility.TryGetSkill(in component.Value, skillId, out skill);
    }

    private static bool IsChainEmpty(in DynamicBuffer<PlayerSkillChainElement> chains, int chainId)
    {
        return chainId < 0 || chainId >= chains.Length || chains[chainId].SlotCount == 0;
    }

    private static bool TryGetChainLength(
        in DynamicBuffer<PlayerSkillChainElement> chains,
        int chainId,
        out int length)
    {
        length = 0;
        if (chainId < 0 || chainId >= chains.Length)
            return false;

        length = chains[chainId].SlotCount;
        return true;
    }

    private static bool TryGetCurrentChainSlot(
        in PlayerInputComponent input,
        in DynamicBuffer<PlayerSkillChainElement> chains,
        in DynamicBuffer<PlayerSkillChainSlotElement> slots,
        in UnitSourceArguments arguments,
        out PlayerSkillChainSlotElement slot)
    {
        slot = default;
        return arguments.TryGetInt(0, out int slotIndex) &&
               TryGetChainSlot(chains, slots, input.SkillChainIndex, slotIndex, out slot);
    }

    private static bool TryGetCurrentSkill(
        Entity registryEntity,
        in PlayerInputComponent input,
        in DynamicBuffer<PlayerSkillChainElement> chains,
        in DynamicBuffer<PlayerSkillChainSlotElement> slots,
        in ComponentLookup<PlayerSkillDefinitionRegistryComponent> registryLookup,
        in UnitSourceArguments arguments,
        out PlayerSkillDefinitionBlob skill)
    {
        skill = default;
        return TryGetCurrentChainSlot(input, chains, slots, in arguments, out PlayerSkillChainSlotElement slot) &&
               TryGetSkill(registryEntity, slot.SkillId, in registryLookup, out skill);
    }

    private static bool TryGetChainSlot(
        in DynamicBuffer<PlayerSkillChainElement> chains,
        in DynamicBuffer<PlayerSkillChainSlotElement> slots,
        in UnitSourceArguments arguments,
        out PlayerSkillChainSlotElement slot)
    {
        slot = default;
        return arguments.TryGetInt(0, out int chainId) &&
               arguments.TryGetInt(1, out int slotIndex) &&
               TryGetChainSlot(chains, slots, chainId, slotIndex, out slot);
    }

    private static bool TryGetSkill(
        Entity registryEntity,
        in ComponentLookup<PlayerSkillDefinitionRegistryComponent> registryLookup,
        in UnitSourceArguments arguments,
        out PlayerSkillDefinitionBlob skill)
    {
        skill = default;
        return arguments.TryGetInt(0, out int skillId) &&
               TryGetSkill(registryEntity, skillId, in registryLookup, out skill);
    }
}
