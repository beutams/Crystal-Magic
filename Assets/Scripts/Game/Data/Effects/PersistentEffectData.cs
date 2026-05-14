using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Data.Effects
{
    [System.Serializable]
    public sealed class PersistentEffectData : EffectData
    {
        [EditorLabel("元素类型")]
        public ElementType Element = ElementType.None;
        [EditorLabel("总时长")]
        public float TotalDuration;
        [EditorLabel("触发间隔")]
        public float TickIntervalSeconds;

        [EditorLabel("开始效果")]
        [UnityEngine.SerializeReference]
        public EffectData[] OnStartEffects = System.Array.Empty<EffectData>();

        [EditorLabel("周期效果")]
        [UnityEngine.SerializeReference]
        public EffectData[] OnTickEffects = System.Array.Empty<EffectData>();

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers, float elementBonus = 0f, System.Func<EffectData, float> elementBonusResolver = null)
        {
            SkillModifierSet runtimeModifiers = CreateCombinedModifiers(modifiers, elementBonus, AppendElementModifiers);
            PersistentEffectData copy = (PersistentEffectData)base.CreateRuntimeCopy(runtimeModifiers, elementBonus, elementBonusResolver);
            copy.TotalDuration = ApplyModifierNonNegative(runtimeModifiers, SkillModifierChannel.EffectDuration, TotalDuration);
            copy.TotalDuration = ApplyModifierNonNegative(runtimeModifiers, SkillModifierChannel.BuffDuration, copy.TotalDuration);
            copy.TickIntervalSeconds = ApplyModifierNonNegative(runtimeModifiers, SkillModifierChannel.TickInterval, TickIntervalSeconds);
            copy.OnStartEffects = CreateRuntimeCopies(OnStartEffects, modifiers, elementBonusResolver);
            copy.OnTickEffects = CreateRuntimeCopies(OnTickEffects, modifiers, elementBonusResolver);
            return copy;
        }

        private void AppendElementModifiers(float elementBonus, SkillModifierSet modifiers)
        {
            if (Element == ElementType.None)
                return;

            modifiers.Add(new SkillModifierEntry
            {
                Channel = SkillModifierChannel.BuffDuration,
                Factor = elementBonus,
            });
        }
    }
}
