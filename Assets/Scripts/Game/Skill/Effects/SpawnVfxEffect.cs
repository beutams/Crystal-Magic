using CrystalMagic.Game.Data.Effects;

namespace CrystalMagic.Game.Skill.Effects
{
    /// <summary>
    /// 生成特效效果，逻辑由特效系统接入
    /// </summary>
    public sealed class SpawnVfxEffect : Effect
    {
        public new SpawnVfxEffectData Data { get; }

        public SpawnVfxEffect(SpawnVfxEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context) { }
    }
}
