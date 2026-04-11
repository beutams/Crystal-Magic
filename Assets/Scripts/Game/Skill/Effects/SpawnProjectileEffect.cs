using CrystalMagic.Game.Data.Effects;

namespace CrystalMagic.Game.Skill.Effects
{
    /// <summary>
    /// 创建投射物效果，逻辑由投射物系统接入
    /// </summary>
    public sealed class SpawnProjectileEffect : Effect
    {
        public new SpawnProjectileEffectData Data { get; }

        public SpawnProjectileEffect(SpawnProjectileEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context) { }
    }
}
