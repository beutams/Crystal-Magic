using System;
using UnityEngine;

namespace CrystalMagic.Game.Skill
{
    public sealed class SkillFollowupModifierRuleFactory : GeneratedFactory<string, SkillFollowupModifierRule>
    {
        public SkillFollowupModifierRuleFactory() : base(StringComparer.Ordinal)
        {
        }

        public SkillFollowupModifierRule CreateRule(string key)
        {
            SkillFollowupModifierRule rule = Create(key ?? string.Empty);
            if (rule == null)
                Debug.LogError($"[SkillFollowupModifierRuleFactory] Unregistered rule: {key}");

            return rule;
        }

        public bool TryCreateRule(string key, out SkillFollowupModifierRule rule)
        {
            return TryCreate(key ?? string.Empty, out rule);
        }
    }
}
