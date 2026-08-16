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
            if (skillStoneItemData == null || skillStoneItemData.ItemType != ItemType.SkillStone || skillStoneItemData.ExtraId < 0)
                return null;

            return dataComponent.Get<SkillData>(skillStoneItemData.ExtraId);
        }

        public static SkillData GetSkillData(SkillChainSlotData slotData)
        {
            return slotData == null ? null : GetSkillDataBySkillStoneItemId(slotData.SkillStoneItemId);
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
        public static ResolvedSkillData Resolve(SkillData skillData, SkillModifierSet modifiers, UnitAttackComponent? attackComponent = null, UnitElementComponent? elementComponent = null)
        {
            if (skillData == null)
                return null;

            modifiers ??= new SkillModifierSet();
            float actionSpeedBonus = attackComponent?.RealActionSpeedBonus ?? 0f;
            float chantSpeedBonus = attackComponent?.RealChantSpeedBonus ?? 0f;
            float actionSpeedValue = modifiers.GetActionSpeedValue(actionSpeedBonus);
            float chantSpeedValue = modifiers.GetChantSpeedValue(chantSpeedBonus);
            float actionSpeedMultiplier = UnitAttackComponent.GetDurationMultiplier(actionSpeedValue);
            float chantSpeedMultiplier = UnitAttackComponent.GetDurationMultiplier(chantSpeedValue);
            float moveSpeedMultiplier = math.min(1f, math.max(0f, skillData.MoveSpeedMultiplier) * modifiers.GetMoveSpeedMultiplier());

            return new ResolvedSkillData
            {
                Source = skillData,
                Id = skillData.Id,
                Name = skillData.DisplayName,
                RuntimeType = skillData.EffectiveRuntimeType,
                MpCost = math.max(0, (int)math.round(modifiers.Apply(SkillModifierChannel.MpCost, skillData.MpCost))),
                WindupDuration = math.max(0f, skillData.WindupDuration * actionSpeedMultiplier),
                ChantDuration = math.max(0f, skillData.ChantDuration * chantSpeedMultiplier),
                RecoveryDuration = math.max(0f, skillData.RecoveryDuration * actionSpeedMultiplier),
                CanMoveWhileCasting = skillData.CanMoveWhileCasting,
                MoveSpeedMultiplier = moveSpeedMultiplier,
                EffectChain = EffectData.CreateRuntimeCopies(skillData.EffectChain, modifiers, elementComponent),
            };
        }
    }
}
