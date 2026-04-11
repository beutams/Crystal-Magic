using CrystalMagic.Game.Data.Effects;

namespace CrystalMagic.Game.Skill.Effects
{
    /// <summary>
    /// 持续性效果（Buff / 场地效果），逻辑由持久化系统接入
    /// </summary>
    public sealed class PersistentEffect : Effect
    {
        public PersistentEffect(PersistentEffectData data) : base(data) { }

        public override void Execute(SkillContent context) { }
    }
}
