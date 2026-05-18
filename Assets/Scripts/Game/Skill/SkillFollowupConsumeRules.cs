using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace CrystalMagic.Game.Skill
{
    public readonly struct SkillFollowupContext
    {
        public SkillFollowupContext(
            EntityManager entityManager,
            Entity entity,
            SkillData skillData,
            ResolvedSkillData resolvedSkillData,
            SkillChainSlotData slotData)
        {
            EntityManager = entityManager;
            Entity = entity;
            SkillData = skillData;
            ResolvedSkillData = resolvedSkillData;
            SlotData = slotData;
        }

        public EntityManager EntityManager { get; }
        public Entity Entity { get; }
        public SkillData SkillData { get; }
        public ResolvedSkillData ResolvedSkillData { get; }
        public SkillChainSlotData SlotData { get; }
    }

    public abstract class SkillFollowupConsumeRule
    {
        public abstract SkillFollowupConsumeRuleType RuleType { get; }

        public abstract bool TryInitializeRuntime(SkillFollowupConsumeRuleData ruleData, ref UnitCastFollowupEffectElement followup);

        public abstract bool CanApply(in UnitCastFollowupEffectElement followup, in SkillFollowupContext context);

        public abstract bool Consume(ref UnitCastFollowupEffectElement followup, in SkillFollowupContext context);
    }

    public abstract class SkillFollowupModifierRule
    {
        public abstract SkillFollowupModifierRuleType RuleType { get; }

        public abstract bool TryInitializeRuntime(SkillFollowupModifierRuleData ruleData, ref UnitCastFollowupEffectElement followup);

        public abstract void ApplyModifiers(ref SkillModifierSet modifiers, in UnitCastFollowupEffectElement followup, in SkillFollowupContext context);

        public abstract void OnConsumed(ref UnitCastFollowupEffectElement followup, in SkillFollowupContext context);
    }

    internal sealed class UseCountSkillFollowupConsumeRule : SkillFollowupConsumeRule
    {
        public override SkillFollowupConsumeRuleType RuleType => SkillFollowupConsumeRuleType.UseCount;

        public override bool TryInitializeRuntime(SkillFollowupConsumeRuleData ruleData, ref UnitCastFollowupEffectElement followup)
        {
            if (ruleData is not UseCountSkillFollowupConsumeRuleData useCountRuleData)
                return false;

            followup.ConsumeRuleStateInt0 = Mathf.Max(1, useCountRuleData.Uses);
            followup.ConsumeRuleStateFloat0 = 0f;
            return true;
        }

        public override bool CanApply(in UnitCastFollowupEffectElement followup, in SkillFollowupContext context)
        {
            return followup.ConsumeRuleStateInt0 > 0;
        }

        public override bool Consume(ref UnitCastFollowupEffectElement followup, in SkillFollowupContext context)
        {
            followup.ConsumeRuleStateInt0 -= 1;
            return followup.ConsumeRuleStateInt0 > 0;
        }
    }

    internal sealed class StaticSkillFollowupModifierRule : SkillFollowupModifierRule
    {
        public override SkillFollowupModifierRuleType RuleType => SkillFollowupModifierRuleType.Static;

        public override bool TryInitializeRuntime(SkillFollowupModifierRuleData ruleData, ref UnitCastFollowupEffectElement followup)
        {
            if (ruleData is not StaticSkillFollowupModifierRuleData staticRuleData)
                return false;

            followup.ModifierEntries = default;
            followup.ModifierSlices = default;
            followup.ModifierRuleStateInt0 = 0;
            followup.ModifierRuleStateFloat0 = 0f;

            return SkillFollowupModifierRuntimeUtility.TryAppendModifierSlice(ref followup, staticRuleData.Modifiers);
        }

        public override void ApplyModifiers(ref SkillModifierSet modifiers, in UnitCastFollowupEffectElement followup, in SkillFollowupContext context)
        {
            if (followup.ModifierSlices.Length <= 0)
                return;

            SkillFollowupModifierRuntimeUtility.ApplySliceModifiers(ref modifiers, followup.ModifierEntries, followup.ModifierSlices[0]);
        }

        public override void OnConsumed(ref UnitCastFollowupEffectElement followup, in SkillFollowupContext context)
        {
        }
    }

    internal sealed class SequenceSkillFollowupModifierRule : SkillFollowupModifierRule
    {
        public override SkillFollowupModifierRuleType RuleType => SkillFollowupModifierRuleType.Sequence;

        public override bool TryInitializeRuntime(SkillFollowupModifierRuleData ruleData, ref UnitCastFollowupEffectElement followup)
        {
            if (ruleData is not SequenceSkillFollowupModifierRuleData sequenceRuleData)
                return false;

            followup.ModifierEntries = default;
            followup.ModifierSlices = default;
            followup.ModifierRuleStateInt0 = 0;
            followup.ModifierRuleStateFloat0 = 0f;

            if (sequenceRuleData.ModifierSets == null || sequenceRuleData.ModifierSets.Count <= 0)
                return false;

            bool hasSlice = false;
            for (int i = 0; i < sequenceRuleData.ModifierSets.Count; i++)
            {
                SkillFollowupModifierSetData modifierSet = sequenceRuleData.ModifierSets[i];
                if (modifierSet?.Modifiers == null || modifierSet.Modifiers.Count <= 0)
                    continue;

                if (!SkillFollowupModifierRuntimeUtility.TryAppendModifierSlice(ref followup, modifierSet.Modifiers))
                    return false;

                hasSlice = true;
            }

            return hasSlice;
        }

        public override void ApplyModifiers(ref SkillModifierSet modifiers, in UnitCastFollowupEffectElement followup, in SkillFollowupContext context)
        {
            if (followup.ModifierSlices.Length <= 0)
                return;

            int sliceIndex = Mathf.Clamp(followup.ModifierRuleStateInt0, 0, followup.ModifierSlices.Length - 1);
            SkillFollowupModifierRuntimeUtility.ApplySliceModifiers(ref modifiers, followup.ModifierEntries, followup.ModifierSlices[sliceIndex]);
        }

        public override void OnConsumed(ref UnitCastFollowupEffectElement followup, in SkillFollowupContext context)
        {
            if (followup.ModifierSlices.Length <= 1)
                return;

            if (followup.ModifierRuleStateInt0 < followup.ModifierSlices.Length - 1)
                followup.ModifierRuleStateInt0 += 1;
        }
    }

    public static class SkillFollowupConsumeRuleRegistry
    {
        private static readonly UseCountSkillFollowupConsumeRule UseCountRule = new();

        public static bool TryGetRule(SkillFollowupConsumeRuleType ruleType, out SkillFollowupConsumeRule rule)
        {
            switch (ruleType)
            {
                case SkillFollowupConsumeRuleType.UseCount:
                    rule = UseCountRule;
                    return true;
                default:
                    rule = null;
                    return false;
            }
        }
    }

    public static class SkillFollowupModifierRuleRegistry
    {
        private static readonly StaticSkillFollowupModifierRule StaticRule = new();
        private static readonly SequenceSkillFollowupModifierRule SequenceRule = new();

        public static bool TryGetRule(SkillFollowupModifierRuleType ruleType, out SkillFollowupModifierRule rule)
        {
            switch (ruleType)
            {
                case SkillFollowupModifierRuleType.Static:
                    rule = StaticRule;
                    return true;
                case SkillFollowupModifierRuleType.Sequence:
                    rule = SequenceRule;
                    return true;
                default:
                    rule = null;
                    return false;
            }
        }
    }

    internal static class SkillFollowupModifierRuntimeUtility
    {
        public static bool TryAppendModifierSlice(ref UnitCastFollowupEffectElement followup, System.Collections.Generic.List<SkillModifierEntry> modifiers)
        {
            if (modifiers == null || modifiers.Count <= 0)
                return false;

            if (followup.ModifierSlices.Length >= followup.ModifierSlices.Capacity)
                return false;

            int startIndex = followup.ModifierEntries.Length;
            int addedCount = 0;
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (followup.ModifierEntries.Length >= followup.ModifierEntries.Capacity)
                    break;

                followup.ModifierEntries.Add(modifiers[i]);
                addedCount++;
            }

            if (addedCount <= 0)
                return false;

            followup.ModifierSlices.Add(new SkillFollowupModifierSlice
            {
                StartIndex = startIndex,
                Length = addedCount,
            });

            return true;
        }

        public static void ApplySliceModifiers(ref SkillModifierSet modifiers, FixedList4096Bytes<SkillModifierEntry> entries, SkillFollowupModifierSlice slice)
        {
            int maxIndex = Mathf.Min(entries.Length, slice.StartIndex + slice.Length);
            for (int i = slice.StartIndex; i < maxIndex; i++)
                modifiers.Add(entries[i]);
        }
    }
}
