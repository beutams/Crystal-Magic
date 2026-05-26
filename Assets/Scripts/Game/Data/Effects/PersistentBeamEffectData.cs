using System.Collections.Generic;
using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Data.Effects
{
    [System.Serializable]
    public sealed class PersistentBeamEffectData : EffectData
    {
        [EditorLabel("Element")]
        public ElementType Element = ElementType.None;

        [EditorLabel("Total Duration")]
        public float TotalDuration = 1f;

        [EditorLabel("Tick Interval")]
        public float TickIntervalSeconds = 0.1f;

        [EditorLabel("Length")]
        public float Length = 10f;

        [EditorLabel("Width")]
        public float Width = 1f;

        [EditorLabel("Origin Offset")]
        public float OriginOffsetDistance;

        [EditorLabel("Target Conditions")]
        public List<ConditionConfig> TargetConditions = new();

        [EditorLabel("On Start Effects")]
        [UnityEngine.SerializeReference]
        public EffectData[] OnStartEffects = System.Array.Empty<EffectData>();

        [EditorLabel("On Hit Effects")]
        [UnityEngine.SerializeReference]
        public EffectData[] OnHitEffects = System.Array.Empty<EffectData>();

        [EditorLabel("On End Effects")]
        [UnityEngine.SerializeReference]
        public EffectData[] OnEndEffects = System.Array.Empty<EffectData>();

        public override EffectData CreateRuntimeCopy(
            SkillModifierSet modifiers,
            float elementBonus = 0f,
            System.Func<EffectData, float> elementBonusResolver = null)
        {
            SkillModifierSet runtimeModifiers = CreateCombinedModifiers(modifiers, elementBonus, AppendElementModifiers);
            PersistentBeamEffectData copy = (PersistentBeamEffectData)base.CreateRuntimeCopy(runtimeModifiers, elementBonus, elementBonusResolver);
            copy.TotalDuration = ApplyModifierNonNegative(runtimeModifiers, SkillModifierChannel.EffectDuration, TotalDuration);
            copy.TickIntervalSeconds = ApplyModifierNonNegative(runtimeModifiers, SkillModifierChannel.TickInterval, TickIntervalSeconds);
            copy.Length = ApplyModifierNonNegative(runtimeModifiers, SkillModifierChannel.ProjectileRange, Length);
            copy.Width = ApplyModifierNonNegative(runtimeModifiers, SkillModifierChannel.AreaRadius, Width);
            copy.TargetConditions = TargetConditions == null ? new List<ConditionConfig>() : new List<ConditionConfig>(TargetConditions);
            copy.OnStartEffects = CreateRuntimeCopies(OnStartEffects, modifiers, elementBonusResolver);
            copy.OnHitEffects = CreateRuntimeCopies(OnHitEffects, modifiers, elementBonusResolver);
            copy.OnEndEffects = CreateRuntimeCopies(OnEndEffects, modifiers, elementBonusResolver);
            return copy;
        }

        private void AppendElementModifiers(float elementBonus, SkillModifierSet modifiers)
        {
            if (Element == ElementType.None)
                return;

            modifiers.Add(new SkillModifierEntry
            {
                Channel = SkillModifierChannel.Damage,
                Factor = elementBonus,
            });
        }
    }
}
