using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using UnityEngine;

namespace CrystalMagic.Game.Skill
{
    public static class SkillChainResolver
    {
        public static bool TryBuildSelectedChain(CharacterData character, List<SkillData> skills, out int chainIndex)
        {
            skills?.Clear();
            chainIndex = -1;

            SkillCData skillConfig = character?.Skills;
            if (character == null || skillConfig?.Chains == null || skillConfig.Chains.Length == 0)
                return false;

            int selectedIndex = Mathf.Clamp(skillConfig.SelectedSkillChainIndex, 0, skillConfig.Chains.Length - 1);
            SkillChainData chain = skillConfig.Chains[selectedIndex];
            if (chain?.SkillStoneIds == null || chain.SkillStoneIds.Count == 0)
                return false;

            DataComponent dataComponent = DataComponent.Instance;
            if (dataComponent == null)
                return false;

            foreach (int skillId in chain.SkillStoneIds)
            {
                SkillData skillData = dataComponent.Get<SkillData>(skillId);
                if (skillData != null)
                    skills.Add(skillData);
            }

            if (skills == null || skills.Count == 0)
                return false;

            chainIndex = selectedIndex;
            return true;
        }

        public static SkillData GetFirstSkill(CharacterData character)
        {
            SkillCData skillConfig = character?.Skills;
            if (character == null || skillConfig?.Chains == null || skillConfig.Chains.Length == 0)
                return null;

            int selectedIndex = Mathf.Clamp(skillConfig.SelectedSkillChainIndex, 0, skillConfig.Chains.Length - 1);
            SkillChainData chain = skillConfig.Chains[selectedIndex];
            if (chain?.SkillStoneIds == null || chain.SkillStoneIds.Count == 0)
                return null;

            DataComponent dataComponent = DataComponent.Instance;
            if (dataComponent == null)
                return null;

            foreach (int skillId in chain.SkillStoneIds)
            {
                SkillData skillData = dataComponent.Get<SkillData>(skillId);
                if (skillData != null)
                    return skillData;
            }

            return null;
        }
    }
}
