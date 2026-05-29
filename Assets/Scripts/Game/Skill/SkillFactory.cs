using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data;
using UnityEngine;

namespace CrystalMagic.Game.Skill
{
    public sealed class SkillFactory : GeneratedFactory<string, ResolvedSkillData, Skill>
    {
        public SkillFactory() : base(StringComparer.Ordinal)
        {
        }

        public Skill CreateSkill(string runtimeType, ResolvedSkillData data)
        {
            Skill skill = Create(runtimeType, data);
            if (skill == null)
                Debug.LogError($"[SkillFactory] Unregistered skill runtime: {runtimeType}");

            return skill;
        }
    }
}
