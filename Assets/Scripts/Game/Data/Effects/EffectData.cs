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

    /// <summary>
    /// 效果配置数据基类
    /// 子类只存数据字段，不含任何执行逻辑
    /// </summary>
    [System.Serializable]
    public abstract class EffectData
    {
        /// <summary>效果释放条件（所有条件通过才执行该效果）</summary>
        [EditorLabel("生效条件")]
        public List<ConditionConfig> Conditions = new();

        public virtual ElementType GetAttributeElementType()
        {
            return ElementType.None;
        }

        public virtual EffectData CreateRuntimeCopy(SkillModifierSet modifiers, UnitElementComponent? elementComponent = null)
        {
            EffectData copy = (EffectData)MemberwiseClone();
            copy.Conditions = Conditions == null ? new List<ConditionConfig>() : new List<ConditionConfig>(Conditions);
            return copy;
        }

        public static EffectData[] CreateRuntimeCopies(EffectData[] effects, SkillModifierSet modifiers, UnitElementComponent? elementComponent = null)
        {
            if (effects == null || effects.Length == 0)
                return Array.Empty<EffectData>();

            EffectData[] copies = new EffectData[effects.Length];
            for (int i = 0; i < effects.Length; i++)
            {
                EffectData effect = effects[i];
                copies[i] = effect?.CreateRuntimeCopy(modifiers, elementComponent);
            }

            return copies;
        }

        protected float ResolveAttributePower(UnitElementComponent? elementComponent)
        {
            if (!elementComponent.HasValue)
                return 0f;

            ElementType element = GetAttributeElementType();
            return element == ElementType.None
                ? 0f
                : elementComponent.Value.GetPowerBonus(element);
        }

        protected static SkillModifierSet CreateCombinedModifiers(SkillModifierSet modifiers, float elementBonus, Action<float, SkillModifierSet> appendElementModifiers)
        {
            SkillModifierSet combined = modifiers?.Clone() ?? new SkillModifierSet();
            appendElementModifiers?.Invoke(math.max(-1f, elementBonus), combined);
            return combined;
        }

        protected static SkillModifierSet CreateModifiersWithAttributePower(SkillModifierSet modifiers, float attributePower)
        {
            SkillModifierSet combined = modifiers?.Clone() ?? new SkillModifierSet();
            if (math.abs(attributePower) > 0.0001f)
            {
                combined.Add(new SkillModifierEntry
                {
                    Channel = SkillModifierChannel.AttributePower,
                    Bonus = attributePower,
                });
            }

            return combined;
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

        protected static float GetAttributePowerValue(SkillModifierSet modifiers)
        {
            return modifiers?.GetAttributePowerValue() ?? 0f;
        }
    }
}

