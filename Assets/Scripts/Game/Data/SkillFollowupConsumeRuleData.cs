using System;

namespace CrystalMagic.Game.Data
{
    [Serializable]
    public abstract class SkillFollowupConsumeRuleData
    {
        public virtual void EnsureDefaults()
        {
        }
    }

    [Serializable]
    [FactoryKey("UseCount", 0, "Use Count")]
    public sealed class UseCountSkillFollowupConsumeRuleData : SkillFollowupConsumeRuleData
    {
        [EditorLabel("Use Count")]
        public int Uses = 1;

        public override void EnsureDefaults()
        {
            if (Uses < 1)
                Uses = 1;
        }
    }
}
