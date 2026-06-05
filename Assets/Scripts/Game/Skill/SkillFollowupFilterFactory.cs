using System;
using UnityEngine;

namespace CrystalMagic.Game.Skill
{
    public sealed class SkillFollowupFilterFactory : GeneratedFactory<string, SkillFollowupFilter>
    {
        public SkillFollowupFilterFactory() : base(StringComparer.Ordinal)
        {
        }

        public SkillFollowupFilter CreateFilter(string key)
        {
            SkillFollowupFilter filter = Create(key ?? string.Empty);
            if (filter == null)
                Debug.LogError($"[SkillFollowupFilterFactory] Unregistered filter: {key}");

            return filter;
        }

        public bool TryCreateFilter(string key, out SkillFollowupFilter filter)
        {
            return TryCreate(key ?? string.Empty, out filter);
        }
    }
}
