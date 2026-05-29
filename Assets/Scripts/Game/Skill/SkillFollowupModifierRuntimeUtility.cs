using System.Collections.Generic;
using CrystalMagic.Game.Data;
using Unity.Collections;
using UnityEngine;

namespace CrystalMagic.Game.Skill
{
    internal static class SkillFollowupModifierRuntimeUtility
    {
        public static bool TryAppendModifierSlice(ref UnitCastFollowupEffectElement followup, List<SkillModifierEntry> modifiers)
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
