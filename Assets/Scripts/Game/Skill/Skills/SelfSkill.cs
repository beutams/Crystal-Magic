using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Skill
{
    /// <summary>
    /// 以施法者自身位置作为释放点的技能。
    /// </summary>
    public class SelfSkill : Skill
    {
        public SelfSkill(SkillData data) : base(data) { }
    }
}
