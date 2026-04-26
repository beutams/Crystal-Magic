using UnityEngine;

namespace CrystalMagic.Core
{
    public sealed class RuntimeDataComponent : SingletonNonMono<RuntimeDataComponent>
    {
        public const string SkillRuntimeDataChangedEventName = "Runtime.Skill.Changed";

        private readonly RuntimeSkillData _skillData = new();

        public RuntimeSkillData GetSkillData()
        {
            return _skillData;
        }

        public void Reset()
        {
            _skillData.CurrentSkillChainIndex = 0;
        }

        public void InitializeFromSave(SaveData saveData)
        {
            CharacterData characterData = saveData?.Town?.Character;
            _skillData.CurrentSkillChainIndex = characterData != null
                ? characterData.ConsumeLegacySelectedSkillChainIndex()
                : 0;

            NotifySkillDataChanged();
        }

        public void SetCurrentSkillChainIndex(int index, SkillCData skillConfig = null)
        {
            int maxIndex = skillConfig?.Chains != null && skillConfig.Chains.Length > 0
                ? skillConfig.Chains.Length - 1
                : 0;
            int clampedIndex = Mathf.Clamp(index, 0, maxIndex);
            if (_skillData.CurrentSkillChainIndex == clampedIndex)
                return;

            _skillData.CurrentSkillChainIndex = clampedIndex;
            NotifySkillDataChanged();
        }

        public void SelectNextSkillChain(SkillCData skillConfig = null)
        {
            int skillChainCount = GetSkillChainCount(skillConfig);
            if (skillChainCount <= 0)
                return;

            int nextIndex = (_skillData.CurrentSkillChainIndex + 1) % skillChainCount;
            SetCurrentSkillChainIndex(nextIndex, skillConfig);
        }

        public int GetSkillChainCount(SkillCData skillConfig = null)
        {
            skillConfig ??= SaveDataComponent.Instance?.GetSkillData();
            return skillConfig?.Chains != null ? skillConfig.Chains.Length : 0;
        }

        public void NotifySkillDataChanged()
        {
            EventComponent.Instance.Publish(new CommonGameEvent(SkillRuntimeDataChangedEventName, _skillData));
        }
    }

    public sealed class RuntimeSkillData
    {
        public int CurrentSkillChainIndex;
    }
}
