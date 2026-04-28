using CrystalMagic.Game.Data.Effects;

namespace CrystalMagic.Game.Skill
{
    /// <summary>
    /// 技能效果基类
    /// </summary>
    public abstract class Effect
    {
        protected EffectData Data { get; }
        protected Effect(EffectData data) => Data = data;
        public abstract void Execute(SkillContent context);
    }
}
