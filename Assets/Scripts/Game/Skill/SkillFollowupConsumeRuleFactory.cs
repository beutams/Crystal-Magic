using System;
using UnityEngine;

namespace CrystalMagic.Game.Skill
{
    public sealed class SkillFollowupConsumeRuleFactory : GeneratedFactory<string, SkillFollowupConsumeRule>
    {
        public SkillFollowupConsumeRuleFactory() : base(StringComparer.Ordinal)
        {
        }

        public SkillFollowupConsumeRule CreateRule(string key)
        {
            SkillFollowupConsumeRule rule = Create(key ?? string.Empty);
            if (rule == null)
                Debug.LogError($"[SkillFollowupConsumeRuleFactory] Unregistered rule: {key}");

            return rule;
        }

        public bool TryCreateRule(string key, out SkillFollowupConsumeRule rule)
        {
            return TryCreate(key ?? string.Empty, out rule);
        }
    }
}
