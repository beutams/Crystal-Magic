using System.Collections.Generic;
using CrystalMagic.Game.Data.Effects;

namespace CrystalMagic.Game.Data
{
    public enum SkillFollowupFilterType
    {
        AnySkill = 0,
        SkillId = 1,
        SkillType = 2,
        Element = 3,
        SkillAdditionId = 4,
    }

    [System.Serializable]
    public class SkillFollowupEffectData
    {
        [EditorLabel("筛选类型")]
        public SkillFollowupFilterType FilterType;

        [EditorLabel("剩余次数")]
        public int Uses = 1;

        [EditorLabel("目标技能ID")]
        public int SkillId = -1;

        [EditorLabel("目标技能类型")]
        public SkillType SkillType;

        [EditorLabel("目标元素")]
        public ElementType Element = ElementType.None;

        [EditorLabel("目标技能加成ID")]
        public int SkillAdditionId = -1;

        public List<SkillModifierEntry> Modifiers = new();
    }
}
