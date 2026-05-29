using System;
using System.Collections.Generic;

namespace CrystalMagic.Game.Data
{
    [Serializable]
    public abstract class SkillFollowupModifierRuleData
    {
        public virtual void EnsureDefaults()
        {
        }
    }

    [Serializable]
    [FactoryKey("Static", 0, "Static")]
    public sealed class StaticSkillFollowupModifierRuleData : SkillFollowupModifierRuleData
    {
        [EditorLabel("Modifiers")]
        public List<SkillModifierEntry> Modifiers = new();

        public override void EnsureDefaults()
        {
            Modifiers ??= new List<SkillModifierEntry>();
        }
    }

    [Serializable]
    [FactoryKey("Sequence", 10, "Sequence")]
    public sealed class SequenceSkillFollowupModifierRuleData : SkillFollowupModifierRuleData
    {
        [EditorLabel("Modifier Sets")]
        public List<SkillFollowupModifierSetData> ModifierSets = new();

        public override void EnsureDefaults()
        {
            ModifierSets ??= new List<SkillFollowupModifierSetData>();
            for (int i = 0; i < ModifierSets.Count; i++)
            {
                ModifierSets[i] ??= new SkillFollowupModifierSetData();
                ModifierSets[i].EnsureDefaults();
            }
        }
    }

    [Serializable]
    public sealed class SkillFollowupModifierSetData
    {
        [EditorLabel("Modifiers")]
        public List<SkillModifierEntry> Modifiers = new();

        public void EnsureDefaults()
        {
            Modifiers ??= new List<SkillModifierEntry>();
        }
    }
}
