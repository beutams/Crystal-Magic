using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using System.Reflection;

namespace CrystalMagic.Game.Skill
{
    public static class SkillChainResolver
    {
        public static SkillEffectData GetSkillAdditionData(int skillAdditionId)
        {
            if (skillAdditionId < 0)
                return null;

            DataComponent dataComponent = DataComponent.Instance;
            return dataComponent == null ? null : dataComponent.Get<SkillEffectData>(skillAdditionId);
        }

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
                if (slotData == null || slotData.SkillStoneItemId < 0)
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
        public static ResolvedSkillData Resolve(SkillData skillData, SkillModifierSet modifiers, SkillEffectData skillAdditionData = null, UnitAttackComponent? attackComponent = null, UnitElementComponent? elementComponent = null)
        {
            if (skillData == null)
                return null;

            modifiers ??= new SkillModifierSet();
            float actionSpeedBonus = attackComponent?.RealActionSpeedBonus ?? 0f;
            float chantSpeedBonus = attackComponent?.RealChantSpeedBonus ?? 0f;
            float actionSpeedValue = modifiers.GetActionSpeedValue(actionSpeedBonus);
            float chantSpeedValue = modifiers.GetChantSpeedValue(chantSpeedBonus);
            float actionSpeedMultiplier = actionSpeedValue >= 0f
                ? 1f / (1f + actionSpeedValue / 100f)
                : 1f - actionSpeedValue / 100f;
            float chantSpeedMultiplier = 1f - chantSpeedValue / 100f;
            float moveSpeedMultiplier = math.min(1f, math.max(0f, skillData.MoveSpeedMultiplier) * modifiers.GetMoveSpeedMultiplier());

            EffectData[] mergedEffectChain = MergeEffectChains(skillData.EffectChain, skillAdditionData?.EffectChain);

            return new ResolvedSkillData
            {
                Source = skillData,
                Id = skillData.Id,
                Name = skillData.DisplayName,
                SkillType = skillData.SkillType,
                MpCost = math.max(0, (int)math.round(modifiers.Apply(SkillModifierChannel.MpCost, skillData.MpCost))),
                WindupDuration = math.max(0f, skillData.WindupDuration * actionSpeedMultiplier),
                ChantDuration = math.max(0f, skillData.ChantDuration * chantSpeedMultiplier),
                RecoveryDuration = math.max(0f, skillData.RecoveryDuration * actionSpeedMultiplier),
                CanMoveWhileCasting = skillData.CanMoveWhileCasting,
                MoveSpeedMultiplier = moveSpeedMultiplier,
                EffectChain = EffectData.CreateRuntimeCopies(mergedEffectChain, modifiers, effectData => GetElementPowerBonus(elementComponent, effectData)),
            };
        }

        private static EffectData[] MergeEffectChains(EffectData[] baseEffects, EffectData[] additionEffects)
        {
            bool hasBaseEffects = baseEffects != null && baseEffects.Length > 0;
            bool hasAdditionEffects = additionEffects != null && additionEffects.Length > 0;

            if (!hasBaseEffects && !hasAdditionEffects)
                return System.Array.Empty<EffectData>();

            if (!hasAdditionEffects)
                return baseEffects;

            if (!hasBaseEffects)
                return additionEffects;

            EffectData[] merged = new EffectData[baseEffects.Length + additionEffects.Length];
            System.Array.Copy(baseEffects, 0, merged, 0, baseEffects.Length);
            System.Array.Copy(additionEffects, 0, merged, baseEffects.Length, additionEffects.Length);
            return merged;
        }

        private static float GetElementPowerBonus(UnitElementComponent? elementComponent, EffectData effectData)
        {
            if (!elementComponent.HasValue || effectData == null)
                return 0f;

            ElementType element = effectData switch
            {
                DamageEffectData damageEffectData => damageEffectData.Element,
                PersistentEffectData persistentEffectData => persistentEffectData.Element,
                _ => ElementType.None,
            };

            return element == ElementType.None
                ? 0f
                : elementComponent.Value.GetPowerBonus(element);
        }

        public static SkillModifierSet CollectModifiers(EntityManager entityManager, Entity entity, SkillData skillData = null, SkillChainSlotData slotData = null)
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
                    if (dataComponent.Get<BuffData>(buffElement.BuffId) is SkillModifierBuffData buffData)
                        modifiers.Add(buffData.SkillModifiers, math.max(1, buffElement.StackCount));
                }
            }

            if (slotData != null && slotData.SkillAdditionId >= 0)
            {
                if (SkillChainResolver.GetSkillAdditionData(slotData.SkillAdditionId) is SkillEffectData skillEffectData)
                    modifiers.Add(skillEffectData.Modifiers);
            }

            if (skillData != null && entityManager.HasBuffer<UnitCastFollowupEffectElement>(entity))
            {
                SkillFollowupContext context = new(entityManager, entity, skillData, null, slotData);
                DynamicBuffer<UnitCastFollowupEffectElement> followupEffects = entityManager.GetBuffer<UnitCastFollowupEffectElement>(entity);
                for (int i = 0; i < followupEffects.Length; i++)
                {
                    UnitCastFollowupEffectElement followupEffect = followupEffects[i];
                    if (!MatchesFollowupEffect(followupEffect, skillData, slotData))
                        continue;

                    ApplyFollowupModifiers(ref modifiers, followupEffect, context);
                }
            }

            return modifiers;
        }

        public static bool MatchesFollowupEffect(UnitCastFollowupEffectElement followupEffect, SkillData skillData, SkillChainSlotData slotData)
        {
            if (skillData == null)
                return false;

            return followupEffect.FilterType switch
            {
                SkillFollowupFilterType.AnySkill => true,
                SkillFollowupFilterType.SkillId => followupEffect.SkillId >= 0 && skillData.Id == followupEffect.SkillId,
                SkillFollowupFilterType.SkillType => skillData.SkillType == followupEffect.SkillType,
                SkillFollowupFilterType.Element => followupEffect.Element != ElementType.None && SkillUsesElement(skillData.EffectChain, followupEffect.Element),
                SkillFollowupFilterType.SkillAdditionId => slotData != null && slotData.SkillAdditionId >= 0 && slotData.SkillAdditionId == followupEffect.SkillAdditionId,
                _ => false,
            };
        }

        public static void ApplyFollowupModifiers(ref SkillModifierSet modifiers, UnitCastFollowupEffectElement followupEffect, in SkillFollowupContext context)
        {
            if (!SkillFollowupConsumeRuleRegistry.TryGetRule(followupEffect.ConsumeRuleType, out SkillFollowupConsumeRule rule))
                return;

            if (!rule.CanApply(followupEffect, context))
                return;

            if (!SkillFollowupModifierRuleRegistry.TryGetRule(followupEffect.ModifierRuleType, out SkillFollowupModifierRule modifierRule))
                return;

            modifierRule.ApplyModifiers(ref modifiers, followupEffect, context);
        }

        private static bool SkillUsesElement(EffectData[] effectChain, ElementType element)
        {
            if (effectChain == null || effectChain.Length == 0 || element == ElementType.None)
                return false;

            for (int i = 0; i < effectChain.Length; i++)
            {
                if (EffectUsesElement(effectChain[i], element))
                    return true;
            }

            return false;
        }

        private static bool EffectUsesElement(EffectData effectData, ElementType element)
        {
            if (effectData == null)
                return false;

            if (effectData is DamageEffectData damageEffectData && damageEffectData.Element == element)
                return true;

            if (effectData is PersistentEffectData persistentEffectData && persistentEffectData.Element == element)
                return true;

            FieldInfo[] fields = effectData.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.FieldType != typeof(EffectData[]) && !(field.FieldType.IsArray && typeof(EffectData).IsAssignableFrom(field.FieldType.GetElementType())))
                    continue;

                if (field.GetValue(effectData) is EffectData[] nestedEffects && SkillUsesElement(nestedEffects, element))
                    return true;
            }

            return false;
        }
    }
}
