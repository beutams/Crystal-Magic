using CrystalMagic.Game.Data.Effects;

namespace CrystalMagic.Game.Skill.Effects
{
    /// <summary>
    /// 生成音效效果，逻辑由音频系统接入
    /// </summary>
    public sealed class SpawnSoundEffect : Effect
    {
        public new SpawnSoundEffectData Data { get; }

        public SpawnSoundEffect(SpawnSoundEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context) { }
    }
}
