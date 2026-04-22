using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Data.Effects
{
    /// <summary>
    /// 效果配置数据基类
    /// 子类只存数据字段，不含任何执行逻辑
    /// </summary>
    [System.Serializable]
    public abstract class EffectData
    {
        /// <summary>效果释放条件（所有条件通过才执行该效果）</summary>
        public List<ConditionConfig> Conditions = new();

        public virtual EffectData CreateRuntimeCopy(SkillModifierSet modifiers)
        {
            EffectData copy = (EffectData)MemberwiseClone();
            copy.Conditions = Conditions == null ? new List<ConditionConfig>() : new List<ConditionConfig>(Conditions);
            return copy;
        }

        public static EffectData[] CreateRuntimeCopies(EffectData[] effects, SkillModifierSet modifiers)
        {
            if (effects == null || effects.Length == 0)
                return Array.Empty<EffectData>();

            EffectData[] copies = new EffectData[effects.Length];
            for (int i = 0; i < effects.Length; i++)
                copies[i] = effects[i]?.CreateRuntimeCopy(modifiers);

            return copies;
        }

        protected static float ApplyModifier(SkillModifierSet modifiers, SkillModifierChannel channel, float value)
        {
            return modifiers == null ? value : modifiers.Apply(channel, value);
        }

        protected static float ApplyModifierNonNegative(SkillModifierSet modifiers, SkillModifierChannel channel, float value)
        {
            float modified = ApplyModifier(modifiers, channel, value);
            return modified < 0f ? 0f : modified;
        }
    }
}

