using UnityEngine;

namespace CrystalMagic.Core
{
    public sealed class RuntimeDataComponent : SingletonNonMono<RuntimeDataComponent>
    {
        public const string SkillRuntimeDataChangedEventName = "Runtime.Skill.Changed";
        public const string PropRuntimeDataChangedEventName = "Runtime.Prop.Changed";

        private readonly RuntimeSkillData _skillData = new();
        private readonly RuntimePropData _propData = new();

        public RuntimeSkillData GetSkillData()
        {
            return _skillData;
        }

        public RuntimePropData GetPropData()
        {
            return _propData;
        }

        public void Reset()
        {
            _skillData.CurrentSkillChainIndex = 0;
            _propData.SharedCooldownRemaining = 0f;
        }

        public void InitializeForGameRun()
        {
            Reset();
            NotifySkillDataChanged();
            NotifyPropDataChanged();
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

        public void TickPropSharedCooldown(float deltaTime)
        {
            if (_propData.SharedCooldownRemaining <= 0f)
                return;

            float nextValue = Mathf.Max(0f, _propData.SharedCooldownRemaining - Mathf.Max(0f, deltaTime));
            if (Mathf.Approximately(nextValue, _propData.SharedCooldownRemaining))
                return;

            _propData.SharedCooldownRemaining = nextValue;
            NotifyPropDataChanged();
        }

        public void StartPropSharedCooldown(float cooldownSeconds)
        {
            float nextValue = Mathf.Max(0f, cooldownSeconds);
            if (Mathf.Approximately(_propData.SharedCooldownRemaining, nextValue))
                return;

            _propData.SharedCooldownRemaining = nextValue;
            NotifyPropDataChanged();
        }

        public void NotifySkillDataChanged()
        {
            EventComponent.Instance.Publish(new CommonGameEvent(SkillRuntimeDataChangedEventName, _skillData));
        }

        public void NotifyPropDataChanged()
        {
            EventComponent.Instance.Publish(new CommonGameEvent(PropRuntimeDataChangedEventName, _propData));
        }
    }

    public sealed class RuntimeSkillData
    {
        public int CurrentSkillChainIndex;
    }

    public sealed class RuntimePropData
    {
        public float SharedCooldownRemaining;
    }
}
