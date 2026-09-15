using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

// Mirrors the player skill configuration for State Script value queries.
public sealed class PlayerSkillRuntimeDataComponent : IComponentData
{
    private int _configurationSignature = int.MinValue;

    public int CurrentChainId;
    public List<PlayerSkillChainData> Chains = new();
    public Dictionary<int, PlayerSkillInfo> Skills = new();

    public void Synchronize(SkillCData skillConfig, DataComponent dataComponent, int currentChainId)
    {
        CurrentChainId = currentChainId;

        int signature = CalculateConfigurationSignature(skillConfig);
        if (signature == _configurationSignature && Skills.Count > 0)
            return;

        _configurationSignature = signature;
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

    private static int CalculateConfigurationSignature(SkillCData skillConfig)
    {
        unchecked
        {
            int signature = 17;
            SkillChainData[] sourceChains = skillConfig?.Chains;
            int chainCount = sourceChains?.Length ?? 0;
            signature = signature * 31 + chainCount;
            for (int chainIndex = 0; chainIndex < chainCount; chainIndex++)
            {
                List<SkillChainSlotData> slots = sourceChains[chainIndex]?.Slots;
                int slotCount = slots?.Count ?? 0;
                signature = signature * 31 + slotCount;
                for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
                {
                    SkillChainSlotData slot = slots[slotIndex];
                    signature = signature * 31 + (slot?.SkillStoneItemId ?? -1);
                    signature = signature * 31 + (slot?.SkillAdditionId ?? -1);
                }
            }

            return signature;
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

public sealed class PlayerSkillRuntimeDataSource : UnitComponentSource
{
    private static readonly ComparatorParameterDefinition[] s_noParameters = Array.Empty<ComparatorParameterDefinition>();
    private static readonly ComparatorParameterDefinition[] s_chainIdParameter =
    {
        new ComparatorParameterDefinition("Chain ID", UnitValueCategory.Number),
    };
    private static readonly ComparatorParameterDefinition[] s_chainSlotParameters =
    {
        new ComparatorParameterDefinition("Chain ID", UnitValueCategory.Number),
        new ComparatorParameterDefinition("Slot Index", UnitValueCategory.Number),
    };
    private static readonly ComparatorParameterDefinition[] s_currentSkillSlotParameter =
    {
        new ComparatorParameterDefinition("Slot Index", UnitValueCategory.Number),
    };
    private static readonly ComparatorParameterDefinition[] s_skillIdParameter =
    {
        new ComparatorParameterDefinition("Skill ID", UnitValueCategory.Number),
    };

    public override Type ComponentType => typeof(PlayerSkillRuntimeDataComponent);
    public override bool IsGlobal => false;

    public override void Describe(UnitSourceSchemaBuilder schema)
    {
        schema.AddGet("player.skill.getCurrentChainId", ComponentType, UnitValueCategory.Number, s_noParameters);
        schema.AddGet("player.skill.isCurrentChainEmpty", ComponentType, UnitValueCategory.Bool, s_noParameters);
        schema.AddGet("player.skill.getCurrentChainLength", ComponentType, UnitValueCategory.Number, s_noParameters);
        schema.AddGet("player.skill.getCurrentSkillId", ComponentType, UnitValueCategory.Number, s_currentSkillSlotParameter);
        schema.AddGet("player.skill.getCurrentSkillAdditionId", ComponentType, UnitValueCategory.Number, s_currentSkillSlotParameter);
        schema.AddGet("player.skill.hasCurrentSkill", ComponentType, UnitValueCategory.Bool, s_currentSkillSlotParameter);
        schema.AddGet("player.skill.getCurrentSkillMpCost", ComponentType, UnitValueCategory.Number, s_currentSkillSlotParameter);
        schema.AddGet("player.skill.getCurrentSkillChantDuration", ComponentType, UnitValueCategory.Number, s_currentSkillSlotParameter);
        schema.AddGet("player.skill.getCurrentSkillRuntimeType", ComponentType, UnitValueCategory.String, s_currentSkillSlotParameter);
        schema.AddGet("player.skill.isChainEmpty", ComponentType, UnitValueCategory.Bool, s_chainIdParameter);
        schema.AddGet("player.skill.getChainLength", ComponentType, UnitValueCategory.Number, s_chainIdParameter);
        schema.AddGet("player.skill.getChainSkillId", ComponentType, UnitValueCategory.Number, s_chainSlotParameters);
        schema.AddGet("player.skill.getChainSkillAdditionId", ComponentType, UnitValueCategory.Number, s_chainSlotParameters);
        schema.AddGet("player.skill.hasSkill", ComponentType, UnitValueCategory.Bool, s_skillIdParameter);
        schema.AddGet("player.skill.getSkillMpCost", ComponentType, UnitValueCategory.Number, s_skillIdParameter);
        schema.AddGet("player.skill.getSkillChantDuration", ComponentType, UnitValueCategory.Number, s_skillIdParameter);
        schema.AddGet("player.skill.getSkillCastingMoveMultiplier", ComponentType, UnitValueCategory.Number, s_skillIdParameter);
        schema.AddGet("player.skill.getSkillRuntimeType", ComponentType, UnitValueCategory.String, s_skillIdParameter);
        schema.AddGet("player.skill.getInputType", ComponentType, UnitValueCategory.Number, s_skillIdParameter);
    }

    public override void Bind(in UnitSourceBindingContext context, UnitSourceAccessTable table)
    {
        EntityManager entityManager = context.EntityManager;
        Entity playerEntity = context.Entity;
        table.AddGet(new UnitSourceGet(
            "player.skill.getCurrentChainId",
            UnitValueCategory.Number,
            s_noParameters,
            _ => TryGetData(entityManager, playerEntity, out PlayerSkillRuntimeDataComponent data)
                ? UnitValue.FromInt(data.CurrentChainId)
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "player.skill.isCurrentChainEmpty",
            UnitValueCategory.Bool,
            s_noParameters,
            _ => TryGetData(entityManager, playerEntity, out PlayerSkillRuntimeDataComponent data)
                ? UnitValue.FromBool(data.IsChainEmpty(data.CurrentChainId))
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "player.skill.getCurrentChainLength",
            UnitValueCategory.Number,
            s_noParameters,
            _ => TryGetData(entityManager, playerEntity, out PlayerSkillRuntimeDataComponent data) &&
                 data.TryGetChainLength(data.CurrentChainId, out int length)
                ? UnitValue.FromInt(length)
                : UnitValue.None));
        AddCurrentChainSlotGet(table, entityManager, playerEntity, "player.skill.getCurrentSkillId",
            slot => UnitValue.FromInt(slot.SkillId));
        AddCurrentChainSlotGet(table, entityManager, playerEntity, "player.skill.getCurrentSkillAdditionId",
            slot => UnitValue.FromInt(slot.SkillAdditionId));
        table.AddGet(new UnitSourceGet(
            "player.skill.hasCurrentSkill",
            UnitValueCategory.Bool,
            s_currentSkillSlotParameter,
            input => TryGetCurrentSkill(entityManager, playerEntity, input, out _)
                ? UnitValue.FromBool(true)
                : UnitValue.FromBool(false)));
        AddCurrentSkillGet(table, entityManager, playerEntity, "player.skill.getCurrentSkillMpCost", UnitValueCategory.Number,
            skill => UnitValue.FromInt(skill.MpCost));
        AddCurrentSkillGet(table, entityManager, playerEntity, "player.skill.getCurrentSkillChantDuration", UnitValueCategory.Number,
            skill => UnitValue.FromFloat(skill.ChantDuration));
        AddCurrentSkillGet(table, entityManager, playerEntity, "player.skill.getCurrentSkillRuntimeType", UnitValueCategory.String,
            skill => UnitValue.FromString(skill.RuntimeType));
        table.AddGet(new UnitSourceGet(
            "player.skill.isChainEmpty",
            UnitValueCategory.Bool,
            s_chainIdParameter,
            input => TryGetData(entityManager, playerEntity, out PlayerSkillRuntimeDataComponent data) &&
                     TryGetInt(input[0], out int chainId)
                ? UnitValue.FromBool(data.IsChainEmpty(chainId))
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "player.skill.getChainLength",
            UnitValueCategory.Number,
            s_chainIdParameter,
            input => TryGetData(entityManager, playerEntity, out PlayerSkillRuntimeDataComponent data) &&
                     TryGetInt(input[0], out int chainId) &&
                     data.TryGetChainLength(chainId, out int length)
                ? UnitValue.FromInt(length)
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "player.skill.getChainSkillId",
            UnitValueCategory.Number,
            s_chainSlotParameters,
            input => TryGetChainSlot(entityManager, playerEntity, input, out PlayerSkillChainSlotData slot)
                ? UnitValue.FromInt(slot.SkillId)
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "player.skill.getChainSkillAdditionId",
            UnitValueCategory.Number,
            s_chainSlotParameters,
            input => TryGetChainSlot(entityManager, playerEntity, input, out PlayerSkillChainSlotData slot)
                ? UnitValue.FromInt(slot.SkillAdditionId)
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "player.skill.hasSkill",
            UnitValueCategory.Bool,
            s_skillIdParameter,
            input => TryGetSkill(entityManager, playerEntity, input[0], out _)
                ? UnitValue.FromBool(true)
                : UnitValue.FromBool(false)));
        AddSkillNumberGet(table, entityManager, playerEntity, "player.skill.getSkillMpCost", s_skillIdParameter,
            skill => UnitValue.FromInt(skill.MpCost));
        AddSkillNumberGet(table, entityManager, playerEntity, "player.skill.getSkillChantDuration", s_skillIdParameter,
            skill => UnitValue.FromFloat(skill.ChantDuration));
        AddSkillNumberGet(table, entityManager, playerEntity, "player.skill.getSkillCastingMoveMultiplier", s_skillIdParameter,
            skill => UnitValue.FromFloat(skill.CastingMoveMultiplier));
        table.AddGet(new UnitSourceGet(
            "player.skill.getSkillRuntimeType",
            UnitValueCategory.String,
            s_skillIdParameter,
            input => TryGetSkill(entityManager, playerEntity, input[0], out PlayerSkillInfo skill)
                ? UnitValue.FromString(skill.RuntimeType)
                : UnitValue.None));
        AddSkillNumberGet(table, entityManager, playerEntity, "player.skill.getInputType", s_skillIdParameter,
            skill => UnitValue.FromInt((int)skill.InputType));
    }

    private static void AddSkillNumberGet(
        UnitSourceAccessTable table,
        EntityManager entityManager,
        Entity playerEntity,
        string key,
        IReadOnlyList<ComparatorParameterDefinition> parameters,
        Func<PlayerSkillInfo, UnitValue> getter)
    {
        table.AddGet(new UnitSourceGet(
            key,
            UnitValueCategory.Number,
            parameters,
            input => TryGetSkill(entityManager, playerEntity, input[0], out PlayerSkillInfo skill)
                ? getter(skill)
                : UnitValue.None));
    }

    private static void AddCurrentChainSlotGet(
        UnitSourceAccessTable table,
        EntityManager entityManager,
        Entity playerEntity,
        string key,
        Func<PlayerSkillChainSlotData, UnitValue> getter)
    {
        table.AddGet(new UnitSourceGet(
            key,
            UnitValueCategory.Number,
            s_currentSkillSlotParameter,
            input => TryGetCurrentChainSlot(entityManager, playerEntity, input, out PlayerSkillChainSlotData slot)
                ? getter(slot)
                : UnitValue.None));
    }

    private static void AddCurrentSkillGet(
        UnitSourceAccessTable table,
        EntityManager entityManager,
        Entity playerEntity,
        string key,
        UnitValueCategory category,
        Func<PlayerSkillInfo, UnitValue> getter)
    {
        table.AddGet(new UnitSourceGet(
            key,
            category,
            s_currentSkillSlotParameter,
            input => TryGetCurrentSkill(entityManager, playerEntity, input, out PlayerSkillInfo skill)
                ? getter(skill)
                : UnitValue.None));
    }

    private static bool TryGetChainSlot(
        EntityManager entityManager,
        Entity playerEntity,
        UnitValue[] input,
        out PlayerSkillChainSlotData slot)
    {
        slot = default;
        return input.Length == 2 &&
               TryGetData(entityManager, playerEntity, out PlayerSkillRuntimeDataComponent data) &&
               TryGetInt(input[0], out int chainId) &&
               TryGetInt(input[1], out int slotIndex) &&
               data.TryGetChainSlot(chainId, slotIndex, out slot);
    }

    private static bool TryGetCurrentChainSlot(
        EntityManager entityManager,
        Entity playerEntity,
        UnitValue[] input,
        out PlayerSkillChainSlotData slot)
    {
        slot = default;
        return input.Length == 1 &&
               TryGetData(entityManager, playerEntity, out PlayerSkillRuntimeDataComponent data) &&
               TryGetInt(input[0], out int slotIndex) &&
               data.TryGetChainSlot(data.CurrentChainId, slotIndex, out slot);
    }

    private static bool TryGetSkill(EntityManager entityManager, Entity playerEntity, UnitValue skillIdValue, out PlayerSkillInfo skill)
    {
        skill = null;
        return TryGetData(entityManager, playerEntity, out PlayerSkillRuntimeDataComponent data) &&
               TryGetInt(skillIdValue, out int skillId) &&
               data.TryGetSkill(skillId, out skill);
    }

    private static bool TryGetCurrentSkill(
        EntityManager entityManager,
        Entity playerEntity,
        UnitValue[] input,
        out PlayerSkillInfo skill)
    {
        skill = null;
        return TryGetCurrentChainSlot(entityManager, playerEntity, input, out PlayerSkillChainSlotData slot) &&
               TryGetData(entityManager, playerEntity, out PlayerSkillRuntimeDataComponent data) &&
               data.TryGetSkill(slot.SkillId, out skill);
    }

    private static bool TryGetData(EntityManager entityManager, Entity playerEntity, out PlayerSkillRuntimeDataComponent data)
    {
        data = null;
        if (!entityManager.Exists(playerEntity) || !entityManager.HasComponent<PlayerSkillRuntimeDataComponent>(playerEntity))
            return false;

        data = entityManager.GetComponentObject<PlayerSkillRuntimeDataComponent>(playerEntity);
        return data != null;
    }

    private static bool TryGetInt(UnitValue value, out int result)
    {
        result = -1;
        if (!value.TryGetNumber(out float rawValue) || float.IsNaN(rawValue) || float.IsInfinity(rawValue))
            return false;

        float roundedValue = Mathf.Round(rawValue);
        if (roundedValue < int.MinValue || roundedValue > int.MaxValue || !Mathf.Approximately(rawValue, roundedValue))
            return false;

        result = (int)roundedValue;
        return true;
    }
}
