using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Skill
{
    /// <summary>
    /// 依赖上下文目标位置的技能
    /// 仅当 SkillContent.HasPosition 为 true 时可由系统视为可触发
    /// </summary>
    public class PositionSkill : Skill
    {
        public PositionSkill(SkillData data) : base(data) { }
    }
}
