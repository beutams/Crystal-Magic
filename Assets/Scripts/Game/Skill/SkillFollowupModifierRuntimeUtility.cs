using System.Collections.Generic;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using UnityEngine;

namespace CrystalMagic.Game.Skill
{
    public sealed class SkillFollowupRuntime
    {
        public SkillFollowupRuntime(int sourceSkillId, int sourceSkillAdditionId, SkillFollowupFilter filter, SkillFollowupConsumeRule consumeRule, SkillFollowupModifierRule modifierRule)
        {
            State = new SkillFollowupRuntimeState
            {
                SourceSkillId = sourceSkillId,
                SourceSkillAdditionId = sourceSkillAdditionId,
            };
            Filter = filter;
            ConsumeRule = consumeRule;
            ModifierRule = modifierRule;
        }

        public SkillFollowupRuntimeState State { get; }

        public SkillFollowupFilter Filter { get; }

        public SkillFollowupConsumeRule ConsumeRule { get; }

        public SkillFollowupModifierRule ModifierRule { get; }

        public bool IsMatch(in SkillFollowupContext context)
        {
            return Filter != null && Filter.IsMatch(State, context);
        }

        public bool CanApply(in SkillFollowupContext context)
        {
            return ConsumeRule != null && ConsumeRule.CanApply(State, context);
        }

        public void GetModifier(ref SkillModifierSet modifiers, in SkillFollowupContext context)
        {
            ModifierRule?.GetModifier(ref modifiers, State, context);
        }

        public bool Consume(in SkillFollowupContext context)
        {
            ModifierRule?.OnConsumed(State, context);
            return ConsumeRule != null && ConsumeRule.Consume(State, context);
        }
    }

    public sealed class SkillFollowupRuntimeState
    {
        public int SourceSkillId = -1;
        public int SourceSkillAdditionId = -1;
        public int ConsumeRuleStateInt0;
        public float ConsumeRuleStateFloat0;
        public int ModifierRuleStateInt0;
        public float ModifierRuleStateFloat0;
        public int SkillId = -1;
        public string RuntimeType = string.Empty;
        public ElementType Element = ElementType.None;
        public string SkillAdditionName = string.Empty;
        public List<SkillModifierEntry> ModifierEntries = new();
        public List<SkillFollowupModifierSlice> ModifierSlices = new();
    }

    public struct SkillFollowupModifierSlice
    {
        public int StartIndex;
        public int Length;
    }

    internal static class SkillFollowupModifierRuntimeUtility
    {
        public static bool TryAppendModifierSlice(SkillFollowupRuntimeState followup, List<SkillModifierEntry> modifiers)
        {
            if (modifiers == null || modifiers.Count <= 0)
                return false;

            followup.ModifierEntries ??= new List<SkillModifierEntry>();
            followup.ModifierSlices ??= new List<SkillFollowupModifierSlice>();

            int startIndex = followup.ModifierEntries.Count;
            for (int i = 0; i < modifiers.Count; i++)
                followup.ModifierEntries.Add(modifiers[i]);

            followup.ModifierSlices.Add(new SkillFollowupModifierSlice
            {
                StartIndex = startIndex,
                Length = modifiers.Count,
            });

            return modifiers.Count > 0;
        }

        public static void ApplySliceModifiers(ref SkillModifierSet modifiers, List<SkillModifierEntry> entries, SkillFollowupModifierSlice slice)
        {
            if (entries == null || entries.Count == 0)
                return;

            int maxIndex = Mathf.Min(entries.Count, slice.StartIndex + slice.Length);
            for (int i = slice.StartIndex; i < maxIndex; i++)
                modifiers.Add(entries[i]);
        }
    }
}
