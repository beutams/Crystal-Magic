using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

// Mirrors the player skill configuration for State Script value queries.
public sealed class WorldSkillDataComponent : IComponentData
{
    private int _configurationSignature = int.MinValue;

    public int CurrentChainId;
    public List<WorldSkillChainData> Chains = new();
    public Dictionary<int, WorldSkillInfo> Skills = new();

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

                Skills[skillData.Id] = new WorldSkillInfo(skillData);
            }
        }

        SkillChainData[] sourceChains = skillConfig?.Chains;
        int chainCount = sourceChains?.Length ?? 0;
        for (int chainId = 0; chainId < chainCount; chainId++)
        {
            SkillChainData sourceChain = sourceChains[chainId];
            WorldSkillChainData chain = new(chainId);
            List<SkillChainSlotData> sourceSlots = sourceChain?.Slots;
            if (sourceSlots != null)
            {
                for (int slotIndex = 0; slotIndex < sourceSlots.Count; slotIndex++)
                {
                    SkillChainSlotData sourceSlot = sourceSlots[slotIndex];
                    int skillId = ResolveSkillId(dataComponent, sourceSlot?.SkillStoneItemId ?? -1);
                    chain.Slots.Add(new WorldSkillChainSlotData(skillId, sourceSlot?.SkillAdditionId ?? -1));
                }
            }

            Chains.Add(chain);
        }
    }

    public bool HasChain(int chainId)
    {
        return chainId >= 0 && chainId < Chains.Count && Chains[chainId]?.Id == chainId;
    }

    public bool TryGetChainLength(int chainId, out int length)
    {
        length = 0;
        if (!HasChain(chainId))
            return false;

        length = Chains[chainId].Slots?.Count ?? 0;
        return true;
    }

    public bool TryGetChainSlot(int chainId, int slotIndex, out WorldSkillChainSlotData slot)
    {
        slot = default;
        if (!HasChain(chainId) || slotIndex < 0)
            return false;

        List<WorldSkillChainSlotData> slots = Chains[chainId].Slots;
        if (slots == null || slotIndex >= slots.Count)
            return false;

        slot = slots[slotIndex];
        return true;
    }

    public bool TryGetSkill(int skillId, out WorldSkillInfo skill)
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

public sealed class WorldSkillChainData
{
    public WorldSkillChainData(int id)
    {
        Id = id;
    }

    public int Id;
    public List<WorldSkillChainSlotData> Slots = new();
}

public readonly struct WorldSkillChainSlotData
{
    public WorldSkillChainSlotData(int skillId, int skillAdditionId)
    {
        SkillId = skillId;
        SkillAdditionId = skillAdditionId;
    }

    public int SkillId { get; }
    public int SkillAdditionId { get; }
}

public sealed class WorldSkillInfo
{
    public WorldSkillInfo(SkillData data)
    {
        Id = data.Id;
        MpCost = data.MpCost;
        WindupDuration = data.WindupDuration;
        ChantDuration = data.ChantDuration;
        RecoveryDuration = data.RecoveryDuration;
        CanMoveWhileCasting = data.CanMoveWhileCasting;
        MoveSpeedMultiplier = data.MoveSpeedMultiplier;
        AnimationName = data.AnimationName ?? string.Empty;
        RuntimeType = data.EffectiveRuntimeType;
    }

    public int Id;
    public int MpCost;
    public float WindupDuration;
    public float ChantDuration;
    public float RecoveryDuration;
    public bool CanMoveWhileCasting;
    public float MoveSpeedMultiplier;
    public string AnimationName;
    public string RuntimeType;
}

public sealed class WorldSkillSource : UnitComponentSource
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
    private static readonly ComparatorParameterDefinition[] s_skillIdParameter =
    {
        new ComparatorParameterDefinition("Skill ID", UnitValueCategory.Number),
    };

    public override Type ComponentType => typeof(WorldSkillDataComponent);
    public override bool IsGlobal => true;

    public override void Describe(UnitSourceSchemaBuilder schema)
    {
        schema.AddGet("world.skill.getCurrentChainId", ComponentType, UnitValueCategory.Number, s_noParameters);
        schema.AddGet("world.skill.getChainCount", ComponentType, UnitValueCategory.Number, s_noParameters);
        schema.AddGet("world.skill.hasChain", ComponentType, UnitValueCategory.Bool, s_chainIdParameter);
        schema.AddGet("world.skill.getChainLength", ComponentType, UnitValueCategory.Number, s_chainIdParameter);
        schema.AddGet("world.skill.getChainSkillId", ComponentType, UnitValueCategory.Number, s_chainSlotParameters);
        schema.AddGet("world.skill.getChainSkillAdditionId", ComponentType, UnitValueCategory.Number, s_chainSlotParameters);
        schema.AddGet("world.skill.hasSkill", ComponentType, UnitValueCategory.Bool, s_skillIdParameter);
        schema.AddGet("world.skill.getSkillMpCost", ComponentType, UnitValueCategory.Number, s_skillIdParameter);
        schema.AddGet("world.skill.getSkillWindupDuration", ComponentType, UnitValueCategory.Number, s_skillIdParameter);
        schema.AddGet("world.skill.getSkillChantDuration", ComponentType, UnitValueCategory.Number, s_skillIdParameter);
        schema.AddGet("world.skill.getSkillRecoveryDuration", ComponentType, UnitValueCategory.Number, s_skillIdParameter);
        schema.AddGet("world.skill.getSkillCanMoveWhileCasting", ComponentType, UnitValueCategory.Bool, s_skillIdParameter);
        schema.AddGet("world.skill.getSkillMoveSpeedMultiplier", ComponentType, UnitValueCategory.Number, s_skillIdParameter);
        schema.AddGet("world.skill.getSkillAnimationName", ComponentType, UnitValueCategory.String, s_skillIdParameter);
        schema.AddGet("world.skill.getSkillRuntimeType", ComponentType, UnitValueCategory.String, s_skillIdParameter);
    }

    public override void Bind(in UnitSourceBindingContext context, UnitSourceAccessTable table)
    {
        if (!WorldStateUtility.TryGetEntity(context.EntityManager, out Entity worldEntity))
            throw new InvalidOperationException("World state entity must exist before unit sources are initialized.");

        EntityManager entityManager = context.EntityManager;
        table.AddGet(new UnitSourceGet(
            "world.skill.getCurrentChainId",
            UnitValueCategory.Number,
            s_noParameters,
            _ => TryGetData(entityManager, worldEntity, out WorldSkillDataComponent data)
                ? UnitValue.FromInt(data.CurrentChainId)
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "world.skill.getChainCount",
            UnitValueCategory.Number,
            s_noParameters,
            _ => TryGetData(entityManager, worldEntity, out WorldSkillDataComponent data)
                ? UnitValue.FromInt(data.Chains?.Count ?? 0)
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "world.skill.hasChain",
            UnitValueCategory.Bool,
            s_chainIdParameter,
            input => TryGetData(entityManager, worldEntity, out WorldSkillDataComponent data) &&
                     TryGetInt(input[0], out int chainId)
                ? UnitValue.FromBool(data.HasChain(chainId))
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "world.skill.getChainLength",
            UnitValueCategory.Number,
            s_chainIdParameter,
            input => TryGetData(entityManager, worldEntity, out WorldSkillDataComponent data) &&
                     TryGetInt(input[0], out int chainId) &&
                     data.TryGetChainLength(chainId, out int length)
                ? UnitValue.FromInt(length)
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "world.skill.getChainSkillId",
            UnitValueCategory.Number,
            s_chainSlotParameters,
            input => TryGetChainSlot(entityManager, worldEntity, input, out WorldSkillChainSlotData slot)
                ? UnitValue.FromInt(slot.SkillId)
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "world.skill.getChainSkillAdditionId",
            UnitValueCategory.Number,
            s_chainSlotParameters,
            input => TryGetChainSlot(entityManager, worldEntity, input, out WorldSkillChainSlotData slot)
                ? UnitValue.FromInt(slot.SkillAdditionId)
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "world.skill.hasSkill",
            UnitValueCategory.Bool,
            s_skillIdParameter,
            input => TryGetSkill(entityManager, worldEntity, input[0], out _)
                ? UnitValue.FromBool(true)
                : UnitValue.FromBool(false)));
        AddSkillNumberGet(table, entityManager, worldEntity, "world.skill.getSkillMpCost", s_skillIdParameter,
            skill => UnitValue.FromInt(skill.MpCost));
        AddSkillNumberGet(table, entityManager, worldEntity, "world.skill.getSkillWindupDuration", s_skillIdParameter,
            skill => UnitValue.FromFloat(skill.WindupDuration));
        AddSkillNumberGet(table, entityManager, worldEntity, "world.skill.getSkillChantDuration", s_skillIdParameter,
            skill => UnitValue.FromFloat(skill.ChantDuration));
        AddSkillNumberGet(table, entityManager, worldEntity, "world.skill.getSkillRecoveryDuration", s_skillIdParameter,
            skill => UnitValue.FromFloat(skill.RecoveryDuration));
        table.AddGet(new UnitSourceGet(
            "world.skill.getSkillCanMoveWhileCasting",
            UnitValueCategory.Bool,
            s_skillIdParameter,
            input => TryGetSkill(entityManager, worldEntity, input[0], out WorldSkillInfo skill)
                ? UnitValue.FromBool(skill.CanMoveWhileCasting)
                : UnitValue.None));
        AddSkillNumberGet(table, entityManager, worldEntity, "world.skill.getSkillMoveSpeedMultiplier", s_skillIdParameter,
            skill => UnitValue.FromFloat(skill.MoveSpeedMultiplier));
        table.AddGet(new UnitSourceGet(
            "world.skill.getSkillAnimationName",
            UnitValueCategory.String,
            s_skillIdParameter,
            input => TryGetSkill(entityManager, worldEntity, input[0], out WorldSkillInfo skill)
                ? UnitValue.FromString(skill.AnimationName)
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "world.skill.getSkillRuntimeType",
            UnitValueCategory.String,
            s_skillIdParameter,
            input => TryGetSkill(entityManager, worldEntity, input[0], out WorldSkillInfo skill)
                ? UnitValue.FromString(skill.RuntimeType)
                : UnitValue.None));
    }

    private static void AddSkillNumberGet(
        UnitSourceAccessTable table,
        EntityManager entityManager,
        Entity worldEntity,
        string key,
        IReadOnlyList<ComparatorParameterDefinition> parameters,
        Func<WorldSkillInfo, UnitValue> getter)
    {
        table.AddGet(new UnitSourceGet(
            key,
            UnitValueCategory.Number,
            parameters,
            input => TryGetSkill(entityManager, worldEntity, input[0], out WorldSkillInfo skill)
                ? getter(skill)
                : UnitValue.None));
    }

    private static bool TryGetChainSlot(
        EntityManager entityManager,
        Entity worldEntity,
        UnitValue[] input,
        out WorldSkillChainSlotData slot)
    {
        slot = default;
        return input.Length == 2 &&
               TryGetData(entityManager, worldEntity, out WorldSkillDataComponent data) &&
               TryGetInt(input[0], out int chainId) &&
               TryGetInt(input[1], out int slotIndex) &&
               data.TryGetChainSlot(chainId, slotIndex, out slot);
    }

    private static bool TryGetSkill(EntityManager entityManager, Entity worldEntity, UnitValue skillIdValue, out WorldSkillInfo skill)
    {
        skill = null;
        return TryGetData(entityManager, worldEntity, out WorldSkillDataComponent data) &&
               TryGetInt(skillIdValue, out int skillId) &&
               data.TryGetSkill(skillId, out skill);
    }

    private static bool TryGetData(EntityManager entityManager, Entity worldEntity, out WorldSkillDataComponent data)
    {
        data = null;
        if (!entityManager.Exists(worldEntity) || !entityManager.HasComponent<WorldSkillDataComponent>(worldEntity))
            return false;

        data = entityManager.GetComponentObject<WorldSkillDataComponent>(worldEntity);
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
