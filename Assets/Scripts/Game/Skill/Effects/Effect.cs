using CrystalMagic.Game.Data.Effects;

namespace CrystalMagic.Game.Skill
{
    /// <summary>
    /// 技能效果基类，只包含执行行为，不持有任何配置字段
    /// 具体参数由子类持有的 EffectData 子类提供
    /// </summary>
    public abstract class Effect
    {
        protected EffectData Data { get; }
        protected Effect(EffectData data) => Data = data;
        public abstract void Execute(SkillContent context);
    }
}
