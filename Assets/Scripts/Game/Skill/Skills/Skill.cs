using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Skill
{
    /// <summary>
    /// 技能基类，只包含执行行为，不持有任何配置字段
    /// 所有配置通过 Data 属性从 SkillData 读取
    /// </summary>
    public abstract class Skill
    {
        protected SkillData Data { get; }

        public Skill(SkillData data) => Data = data;
    }
}
