using System;
using CrystalMagic.Game.Data.Effects;

namespace CrystalMagic.Game.Data
{
    [Serializable]
    public abstract class SkillFollowupFilterData
    {
        public virtual void EnsureDefaults()
        {
        }
    }

    [Serializable]
    [FactoryKey("AnySkill", 0, "Any Skill")]
    public sealed class AnySkillFollowupFilterData : SkillFollowupFilterData
    {
    }

    [Serializable]
    [FactoryKey("SkillId", 10, "Skill Id")]
    public sealed class SkillIdFollowupFilterData : SkillFollowupFilterData
    {
        [EditorLabel("Skill Id")]
        public int SkillId = -1;
    }

    [Serializable]
    [FactoryKey("RuntimeType", 20, "Runtime Type")]
    public sealed class RuntimeTypeFollowupFilterData : SkillFollowupFilterData
    {
        [EditorLabel("Runtime Type")]
        public string RuntimeType;

        public string EffectiveRuntimeType => SkillData.GetEffectiveRuntimeType(RuntimeType);
    }

    [Serializable]
    [FactoryKey("Element", 30, "Element")]
    public sealed class ElementFollowupFilterData : SkillFollowupFilterData
    {
        [EditorLabel("Element")]
        public ElementType Element = ElementType.None;
    }

    [Serializable]
    [FactoryKey("SkillAdditionName", 40, "Skill Addition Name")]
    public sealed class SkillAdditionNameFollowupFilterData : SkillFollowupFilterData
    {
        [EditorLabel("Skill Addition Name")]
        public string SkillAdditionName;
    }
}
