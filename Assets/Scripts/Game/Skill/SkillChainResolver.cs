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

        public static bool TryBuildSelectedChain(SkillCData skillConfig, RuntimeSkillData runtimeSkillData, List<SkillData> skills, out int chainIndex)
        {
            skills?.Clear();
            chainIndex = -1;

            if (skillConfig?.Chains == null || skillConfig.Chains.Length == 0)
                return false;

            int selectedIndex = Mathf.Clamp(runtimeSkillData?.CurrentSkillChainIndex ?? 0, 0, skillConfig.Chains.Length - 1);
            SkillChainData chain = skillConfig.Chains[selectedIndex];
            if (chain?.SkillStoneIds == null || chain.SkillStoneIds.Count == 0)
                return false;

            foreach (int skillStoneItemId in chain.SkillStoneIds)
            {
                SkillData skillData = GetSkillDataBySkillStoneItemId(skillStoneItemId);
                if (skillData != null)
                    skills.Add(skillData);
            }

            if (skills == null || skills.Count == 0)
                return false;

            chainIndex = selectedIndex;
            return true;
        }

        public static SkillData GetFirstSkill(SkillCData skillConfig, RuntimeSkillData runtimeSkillData)
        {
            if (skillConfig?.Chains == null || skillConfig.Chains.Length == 0)
                return null;

            int selectedIndex = Mathf.Clamp(runtimeSkillData?.CurrentSkillChainIndex ?? 0, 0, skillConfig.Chains.Length - 1);
            SkillChainData chain = skillConfig.Chains[selectedIndex];
            if (chain?.SkillStoneIds == null || chain.SkillStoneIds.Count == 0)
                return null;

            foreach (int skillStoneItemId in chain.SkillStoneIds)
            {
                SkillData skillData = GetSkillDataBySkillStoneItemId(skillStoneItemId);
                if (skillData != null)
                    return skillData;
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

        public static SkillModifierSet CollectModifiers(EntityManager entityManager, Entity entity)
        {
            SkillModifierSet modifiers = new();
            if (!entityManager.HasBuffer<UnitBuffElement>(entity))
                return modifiers;

            DataComponent dataComponent = DataComponent.Instance;
            if (dataComponent == null)
                return modifiers;

            DynamicBuffer<UnitBuffElement> buffs = entityManager.GetBuffer<UnitBuffElement>(entity);
            for (int i = 0; i < buffs.Length; i++)
            {
                UnitBuffElement buffElement = buffs[i];
                if (dataComponent.Get<BuffData>(buffElement.BuffId) is BuffData buffData)
                    modifiers.Add(buffData.SkillModifiers, math.max(1, buffElement.StackCount));
            }

            return modifiers;
        }
    }
}
