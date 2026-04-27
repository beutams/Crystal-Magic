using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CrystalMagic.Game.Skill
{
    public static class SkillChainResolver
    {
        public static SkillData GetSkillDataBySkillStoneItemId(int skillStoneItemId)
        {
            DataComponent dataComponent = DataComponent.Instance;
            if (dataComponent == null)
                return null;

            ItemData skillStoneItemData = dataComponent.Get<ItemData>(skillStoneItemId);
            if (skillStoneItemData == null || skillStoneItemData.ItemType != ItemType.SkillStone || skillStoneItemData.ExtraId <= 0)
                return null;

            return dataComponent.Get<SkillData>(skillStoneItemData.ExtraId);
        }

        public static SkillData GetSkillData(SkillChainSlotData slotData)
        {
            return slotData == null ? null : GetSkillDataBySkillStoneItemId(slotData.SkillStoneItemId);
        }

        public static bool TryBuildSelectedChain(SkillCData skillConfig, RuntimeSkillData runtimeSkillData, List<SkillChainSlotData> slots, out int chainIndex)
        {
            slots?.Clear();
            chainIndex = -1;

            if (skillConfig?.Chains == null || skillConfig.Chains.Length == 0)
                return false;

            int selectedIndex = Mathf.Clamp(runtimeSkillData?.CurrentSkillChainIndex ?? 0, 0, skillConfig.Chains.Length - 1);
            SkillChainData chain = skillConfig.Chains[selectedIndex];
            chain?.EnsureSlots();
            if (chain?.Slots == null || chain.Slots.Count == 0)
                return false;

            foreach (SkillChainSlotData slotData in chain.Slots)
            {
                if (slotData == null || slotData.SkillStoneItemId <= 0)
                    continue;

                SkillData skillData = GetSkillData(slotData);
                if (skillData != null)
                    slots.Add(slotData);
            }

            if (slots == null || slots.Count == 0)
                return false;

            chainIndex = selectedIndex;
            return true;
        }

        public static SkillChainSlotData GetFirstSlot(SkillCData skillConfig, RuntimeSkillData runtimeSkillData)
        {
            if (skillConfig?.Chains == null || skillConfig.Chains.Length == 0)
                return null;

            int selectedIndex = Mathf.Clamp(runtimeSkillData?.CurrentSkillChainIndex ?? 0, 0, skillConfig.Chains.Length - 1);
            SkillChainData chain = skillConfig.Chains[selectedIndex];
            chain?.EnsureSlots();
            if (chain?.Slots == null || chain.Slots.Count == 0)
                return null;

            foreach (SkillChainSlotData slotData in chain.Slots)
            {
                SkillData skillData = GetSkillData(slotData);
                if (skillData != null)
                    return slotData;
            }

            return null;
        }

    }

    public static class SkillResolver
    {
        public static ResolvedSkillData Resolve(SkillData skillData, SkillModifierSet modifiers)
        {
            if (skillData == null)
                return null;

            modifiers ??= new SkillModifierSet();

            return new ResolvedSkillData
            {
                Source = skillData,
                Id = skillData.Id,
                Name = skillData.Name,
                SkillType = skillData.SkillType,
                MpCost = math.max(0, (int)math.round(modifiers.Apply(SkillModifierChannel.MpCost, skillData.MpCost))),
                WindupDuration = math.max(0f, modifiers.Apply(SkillModifierChannel.WindupDuration, skillData.WindupDuration)),
                ChantDuration = math.max(0f, modifiers.Apply(SkillModifierChannel.ChantDuration, skillData.ChantDuration)),
                RecoveryDuration = math.max(0f, modifiers.Apply(SkillModifierChannel.RecoveryDuration, skillData.RecoveryDuration)),
                CanMoveDuringWindup = skillData.CanMoveDuringWindup,
                CanMoveDuringCasting = skillData.CanMoveDuringCasting,
                CanMoveDuringRecovery = skillData.CanMoveDuringRecovery,
                MoveSpeedMultiplier = math.max(0f, modifiers.Apply(SkillModifierChannel.MoveSpeedMultiplier, skillData.MoveSpeedMultiplier)),
                EffectChain = EffectData.CreateRuntimeCopies(skillData.EffectChain, modifiers),
            };
        }

        public static SkillModifierSet CollectModifiers(EntityManager entityManager, Entity entity, SkillChainSlotData slotData = null)
        {
            SkillModifierSet modifiers = new();

            DataComponent dataComponent = DataComponent.Instance;
            if (dataComponent == null)
                return modifiers;

            if (entityManager.HasBuffer<UnitBuffElement>(entity))
            {
                DynamicBuffer<UnitBuffElement> buffs = entityManager.GetBuffer<UnitBuffElement>(entity);
                for (int i = 0; i < buffs.Length; i++)
                {
                    UnitBuffElement buffElement = buffs[i];
                    if (dataComponent.Get<BuffData>(buffElement.BuffId) is BuffData buffData)
                        modifiers.Add(buffData.SkillModifiers, math.max(1, buffElement.StackCount));
                }
            }

            if (slotData != null && slotData.SkillEffectId > 0)
            {
                if (dataComponent.Get<SkillEffectData>(slotData.SkillEffectId) is SkillEffectData skillEffectData)
                    modifiers.Add(skillEffectData.Modifiers);
            }

            return modifiers;
        }
    }
}
