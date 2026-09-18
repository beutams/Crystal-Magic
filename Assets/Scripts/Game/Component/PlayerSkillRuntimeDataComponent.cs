using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

// Mirrors the player skill configuration for State Script value queries.
public sealed class PlayerSkillRuntimeDataComponent : IComponentData
{
    public int CurrentChainId;
    public List<PlayerSkillChainData> Chains = new();
    public Dictionary<int, PlayerSkillInfo> Skills = new();

    public void Rebuild(SkillCData skillConfig, DataComponent dataComponent, int currentChainId)
    {
        CurrentChainId = currentChainId;
        Chains.Clear();
        Skills.Clear();

        if (dataComponent != null)
        {
            foreach (SkillData skillData in dataComponent.FindAll<SkillData>(_ => true))
            {
                if (skillData == null)
                    continue;

                Skills[skillData.Id] = new PlayerSkillInfo(skillData);
            }
        }

        SkillChainData[] sourceChains = skillConfig?.Chains;
        int chainCount = sourceChains?.Length ?? 0;
        for (int chainId = 0; chainId < chainCount; chainId++)
        {
            SkillChainData sourceChain = sourceChains[chainId];
            PlayerSkillChainData chain = new(chainId);
            List<SkillChainSlotData> sourceSlots = sourceChain?.Slots;
            if (sourceSlots != null)
            {
                for (int slotIndex = 0; slotIndex < sourceSlots.Count; slotIndex++)
                {
                    SkillChainSlotData sourceSlot = sourceSlots[slotIndex];
                    int skillId = ResolveSkillId(dataComponent, sourceSlot?.SkillStoneItemId ?? -1);
                    chain.Slots.Add(new PlayerSkillChainSlotData(skillId, sourceSlot?.SkillAdditionId ?? -1));
                }
            }

            Chains.Add(chain);
        }
    }

    public bool IsChainEmpty(int chainId)
    {
        return chainId < 0 || chainId >= Chains.Count || Chains[chainId].Slots.Count == 0;
    }

    public bool TryGetChainLength(int chainId, out int length)
    {
        length = 0;
        if (chainId < 0 || chainId >= Chains.Count)
            return false;

        length = Chains[chainId].Slots.Count;
        return true;
    }

    public bool TryGetChainSlot(int chainId, int slotIndex, out PlayerSkillChainSlotData slot)
    {
        slot = default;
        if (chainId < 0 || chainId >= Chains.Count || slotIndex < 0)
            return false;

        List<PlayerSkillChainSlotData> slots = Chains[chainId].Slots;
        if (slotIndex >= slots.Count)
            return false;

        slot = slots[slotIndex];
        return true;
    }

    public bool TryGetSkill(int skillId, out PlayerSkillInfo skill)
    {
        return Skills.TryGetValue(skillId, out skill);
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

public static class PlayerSkillRuntimeDataUtility
{
    public static void Initialize(EntityManager entityManager, Entity player, CharacterData characterData)
    {
        if (!entityManager.Exists(player) || characterData == null)
            return;

        if (!entityManager.HasComponent<PlayerSkillSelectionComponent>(player))
            entityManager.AddComponentData(player, new PlayerSkillSelectionComponent());

        if (!entityManager.HasComponent<PlayerPropCooldownComponent>(player))
            entityManager.AddComponentData(player, new PlayerPropCooldownComponent());

        Rebuild(entityManager, player, characterData);
    }

    public static void Rebuild(EntityManager entityManager, Entity player)
    {
        if (!GameRuntimeStateUtility.TryGetPlayerCharacterData(entityManager, player, out CharacterData characterData))
            return;

        if (!entityManager.HasComponent<PlayerSkillSelectionComponent>(player) ||
            !entityManager.HasComponent<PlayerPropCooldownComponent>(player))
        {
            Initialize(entityManager, player, characterData);
            return;
        }

        Rebuild(entityManager, player, characterData);
    }

    public static void SetCurrentChain(EntityManager entityManager, Entity player, int currentChainId)
    {
        if (!entityManager.Exists(player) ||
            !entityManager.HasComponent<PlayerSkillRuntimeDataComponent>(player))
        {
            return;
        }

        PlayerSkillRuntimeDataComponent runtimeData =
            entityManager.GetComponentObject<PlayerSkillRuntimeDataComponent>(player);
        if (runtimeData != null)
            runtimeData.CurrentChainId = currentChainId;
    }

    private static void Rebuild(EntityManager entityManager, Entity player, CharacterData characterData)
    {
        PlayerSkillSelectionComponent selection =
            entityManager.GetComponentData<PlayerSkillSelectionComponent>(player);
        int chainCount = characterData.Skills?.Chains?.Length ?? 0;
        int maxIndex = chainCount > 0 ? chainCount - 1 : 0;
        if (selection.CurrentChainIndex < 0 || selection.CurrentChainIndex > maxIndex)
        {
            selection.CurrentChainIndex = 0;
            selection.NetworkDirty = 1;
            entityManager.SetComponentData(player, selection);
        }

        PlayerSkillRuntimeDataComponent runtimeData;
        if (entityManager.HasComponent<PlayerSkillRuntimeDataComponent>(player))
        {
            runtimeData = entityManager.GetComponentObject<PlayerSkillRuntimeDataComponent>(player);
            if (runtimeData == null)
            {
                entityManager.RemoveComponent<PlayerSkillRuntimeDataComponent>(player);
                runtimeData = new PlayerSkillRuntimeDataComponent();
                entityManager.AddComponentObject(player, runtimeData);
            }
        }
        else
        {
            runtimeData = new PlayerSkillRuntimeDataComponent();
            entityManager.AddComponentObject(player, runtimeData);
        }

        runtimeData.Rebuild(characterData.Skills, DataComponent.Instance, selection.CurrentChainIndex);
    }
}

public sealed class PlayerSkillChainData
{
    public PlayerSkillChainData(int id)
    {
        Id = id;
    }

    public int Id;
    public List<PlayerSkillChainSlotData> Slots = new();
}

public readonly struct PlayerSkillChainSlotData
{
    public PlayerSkillChainSlotData(int skillId, int skillAdditionId)
    {
        SkillId = skillId;
        SkillAdditionId = skillAdditionId;
    }

    public int SkillId { get; }
    public int SkillAdditionId { get; }
}

public sealed class PlayerSkillInfo
{
    public PlayerSkillInfo(SkillData data)
    {
        Id = data.Id;
        MpCost = data.MpCost;
        ChantDuration = data.ChantDuration;
        CastingMoveMultiplier = Mathf.Max(0f, data.CastingMoveMultiplier);
        RuntimeType = data.EffectiveRuntimeType;
        InputType = data.InputType;
    }

    public int Id;
    public int MpCost;
    public float ChantDuration;
    public float CastingMoveMultiplier;
    public string RuntimeType;
    public SkillInputType InputType;
}

[UnitSourceProvider(typeof(PlayerSkillRuntimeDataComponent), typeof(PlayerCurrentSkillAuthoring))]
public static class PlayerSkillRuntimeDataSource
{
    [UnitSourceGet(0, "player.skill.getCurrentChainId", UnitValueCategory.Number)]
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
        EntityManager entityManager,
        Entity entity,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = default;
        if (!TryGetData(entityManager, entity, out PlayerSkillRuntimeDataComponent data))
            return false;

        switch (operation)
        {
            case 0:
                result = UnitSourceValue.FromInt(data.CurrentChainId);
                return true;
            case 1:
                result = UnitSourceValue.FromBool(data.IsChainEmpty(data.CurrentChainId));
                return true;
            case 2:
                if (!data.TryGetChainLength(data.CurrentChainId, out int currentLength))
                    return false;
                result = UnitSourceValue.FromInt(currentLength);
                return true;
            case 3:
                if (!TryGetCurrentChainSlot(data, in arguments, out PlayerSkillChainSlotData currentIdSlot))
                    return false;
                result = UnitSourceValue.FromInt(currentIdSlot.SkillId);
                return true;
            case 4:
                if (!TryGetCurrentChainSlot(data, in arguments, out PlayerSkillChainSlotData currentAdditionSlot))
                    return false;
                result = UnitSourceValue.FromInt(currentAdditionSlot.SkillAdditionId);
                return true;
            case 5:
                result = UnitSourceValue.FromBool(
                    TryGetCurrentChainSlot(data, in arguments, out PlayerSkillChainSlotData currentSlot) &&
                    data.TryGetSkill(currentSlot.SkillId, out _));
                return true;
            case 6:
                if (!TryGetCurrentSkill(data, in arguments, out PlayerSkillInfo currentMpSkill))
                    return false;
                result = UnitSourceValue.FromInt(currentMpSkill.MpCost);
                return true;
            case 7:
                if (!TryGetCurrentSkill(data, in arguments, out PlayerSkillInfo currentChantSkill))
                    return false;
                result = UnitSourceValue.FromFloat(currentChantSkill.ChantDuration);
                return true;
            case 8:
                if (!TryGetCurrentSkill(data, in arguments, out PlayerSkillInfo currentRuntimeSkill))
                    return false;
                result = UnitSourceValue.FromString(currentRuntimeSkill.RuntimeType);
                return result.Type != UnitValueType.None;
            case 9:
                if (!arguments.TryGetInt(0, out int emptyChainId))
                    return false;
                result = UnitSourceValue.FromBool(data.IsChainEmpty(emptyChainId));
                return true;
            case 10:
                if (!arguments.TryGetInt(0, out int lengthChainId) ||
                    !data.TryGetChainLength(lengthChainId, out int chainLength))
                    return false;
                result = UnitSourceValue.FromInt(chainLength);
                return true;
            case 11:
                if (!TryGetChainSlot(data, in arguments, out PlayerSkillChainSlotData skillSlot))
                    return false;
                result = UnitSourceValue.FromInt(skillSlot.SkillId);
                return true;
            case 12:
                if (!TryGetChainSlot(data, in arguments, out PlayerSkillChainSlotData additionSlot))
                    return false;
                result = UnitSourceValue.FromInt(additionSlot.SkillAdditionId);
                return true;
            case 13:
                if (!arguments.TryGetInt(0, out int hasSkillId))
                    return false;
                result = UnitSourceValue.FromBool(data.TryGetSkill(hasSkillId, out _));
                return true;
            case 14:
                if (!TryGetSkill(data, in arguments, out PlayerSkillInfo mpSkill))
                    return false;
                result = UnitSourceValue.FromInt(mpSkill.MpCost);
                return true;
            case 15:
                if (!TryGetSkill(data, in arguments, out PlayerSkillInfo chantSkill))
                    return false;
                result = UnitSourceValue.FromFloat(chantSkill.ChantDuration);
                return true;
            case 16:
                if (!TryGetSkill(data, in arguments, out PlayerSkillInfo moveSkill))
                    return false;
                result = UnitSourceValue.FromFloat(moveSkill.CastingMoveMultiplier);
                return true;
            case 17:
                if (!TryGetSkill(data, in arguments, out PlayerSkillInfo runtimeSkill))
                    return false;
                result = UnitSourceValue.FromString(runtimeSkill.RuntimeType);
                return result.Type != UnitValueType.None;
            case 18:
                if (!TryGetSkill(data, in arguments, out PlayerSkillInfo inputSkill))
                    return false;
                result = UnitSourceValue.FromInt((int)inputSkill.InputType);
                return true;
            default:
                return false;
        }
    }

    private static bool TryGetData(
        EntityManager entityManager,
        Entity entity,
        out PlayerSkillRuntimeDataComponent data)
    {
        data = null;
        if (!entityManager.Exists(entity) ||
            !entityManager.HasComponent<PlayerSkillRuntimeDataComponent>(entity))
        {
            return false;
        }

        data = entityManager.GetComponentObject<PlayerSkillRuntimeDataComponent>(entity);
        return data != null;
    }

    private static bool TryGetCurrentChainSlot(
        PlayerSkillRuntimeDataComponent data,
        in UnitSourceArguments arguments,
        out PlayerSkillChainSlotData slot)
    {
        slot = default;
        return arguments.TryGetInt(0, out int slotIndex) &&
               data.TryGetChainSlot(data.CurrentChainId, slotIndex, out slot);
    }

    private static bool TryGetCurrentSkill(
        PlayerSkillRuntimeDataComponent data,
        in UnitSourceArguments arguments,
        out PlayerSkillInfo skill)
    {
        skill = null;
        return TryGetCurrentChainSlot(data, in arguments, out PlayerSkillChainSlotData slot) &&
               data.TryGetSkill(slot.SkillId, out skill);
    }

    private static bool TryGetChainSlot(
        PlayerSkillRuntimeDataComponent data,
        in UnitSourceArguments arguments,
        out PlayerSkillChainSlotData slot)
    {
        slot = default;
        return arguments.TryGetInt(0, out int chainId) &&
               arguments.TryGetInt(1, out int slotIndex) &&
               data.TryGetChainSlot(chainId, slotIndex, out slot);
    }

    private static bool TryGetSkill(
        PlayerSkillRuntimeDataComponent data,
        in UnitSourceArguments arguments,
        out PlayerSkillInfo skill)
    {
        skill = null;
        return arguments.TryGetInt(0, out int skillId) &&
               data.TryGetSkill(skillId, out skill);
    }
}
