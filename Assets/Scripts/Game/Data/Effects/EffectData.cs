using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data;
using Unity.Mathematics;

namespace CrystalMagic.Game.Data.Effects
{
    public enum ElementType
    {
        None = 0,
        Water = 1,
        Fire = 2,
        Lightning = 3,
        Wind = 4,
    }

    public enum ElementAffectMode
    {
        None = 0,
        Magnitude = 1,
        Duration = 2,
        MagnitudeAndDuration = 3,
    }

    public interface IElementalEffectData
    {
        ElementType Element { get; }
    }

    /// <summary>
    /// 效果配置数据基类
    /// 子类只存数据字段，不含任何执行逻辑
    /// </summary>
    [System.Serializable]
    public abstract class EffectData
    {
        /// <summary>效果释放条件（所有条件通过才执行该效果）</summary>
        public List<ConditionConfig> Conditions = new();

        public virtual EffectData CreateRuntimeCopy(SkillModifierSet modifiers, float elementBonus = 0f, Func<EffectData, float> elementBonusResolver = null)
        {
            EffectData copy = (EffectData)MemberwiseClone();
            copy.Conditions = Conditions == null ? new List<ConditionConfig>() : new List<ConditionConfig>(Conditions);
            return copy;
        }

        public static EffectData[] CreateRuntimeCopies(EffectData[] effects, SkillModifierSet modifiers, Func<EffectData, float> elementBonusResolver = null)
        {
            if (effects == null || effects.Length == 0)
                return Array.Empty<EffectData>();

            EffectData[] copies = new EffectData[effects.Length];
            for (int i = 0; i < effects.Length; i++)
            {
                EffectData effect = effects[i];
                float elementBonus = elementBonusResolver?.Invoke(effect) ?? 0f;
                copies[i] = effect?.CreateRuntimeCopy(modifiers, elementBonus, elementBonusResolver);
            }

            return copies;
        }

        protected static SkillModifierSet CreateCombinedModifiers(SkillModifierSet modifiers, float elementBonus, Action<float, SkillModifierSet> appendElementModifiers)
        {
            SkillModifierSet combined = modifiers?.Clone() ?? new SkillModifierSet();
            appendElementModifiers?.Invoke(math.max(-1f, elementBonus), combined);
            return combined;
        }

        protected static bool AffectsMagnitude(ElementAffectMode affectMode)
        {
            return affectMode == ElementAffectMode.Magnitude || affectMode == ElementAffectMode.MagnitudeAndDuration;
        }

        protected static bool AffectsDuration(ElementAffectMode affectMode)
        {
            return affectMode == ElementAffectMode.Duration || affectMode == ElementAffectMode.MagnitudeAndDuration;
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

