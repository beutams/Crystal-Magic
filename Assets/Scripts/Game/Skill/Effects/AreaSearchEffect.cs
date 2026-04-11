using CrystalMagic.Game.Data.Effects;

namespace CrystalMagic.Game.Skill.Effects
{
    /// <summary>
    /// 范围搜索效果，逻辑由目标查询系统接入
    /// </summary>
    public sealed class AreaSearchEffect : Effect
    {
        public AreaSearchEffect(AreaSearchEffectData data) : base(data) { }

        public override void Execute(SkillContent context) { }
    }
}
