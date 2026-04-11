using CrystalMagic.Game.Data.Effects;

namespace CrystalMagic.Game.Skill.Effects
{
    /// <summary>
    /// 伤害效果，逻辑由战斗结算系统接入
    /// </summary>
    public sealed class DamageEffect : Effect
    {
        public DamageEffect(DamageEffectData data) : base(data) { }

        public override void Execute(SkillContent context) { }
    }
}
