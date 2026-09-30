using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Data.Effects
{
    [System.Serializable]
    public sealed class PersistentEffectData : EffectData
    {
        [EditorLabel("Element")]
        public ElementType Element = ElementType.None;

        public override ElementType GetAttributeElementType()
        {
            return Element;
        }

        [EditorLabel("Total Duration")]
        public float TotalDuration;

        [EditorLabel("Tick Interval")]
        public float TickIntervalSeconds;

        [EditorLabel("Placement Count")]
        public int PlacementCount = 1;
        [EditorLabel("Placement Radius")]
        public float PlacementRadius;
        [EditorLabel("Validate Placement Position")]
        public bool ValidatePlacementPosition;
        [EditorLabel("Placement Clearance Radius")]
        public float PlacementClearanceRadius;
        [EditorLabel("Position Attempts")]
        public int PlacementValidationAttempts = 32;

        [EditorLabel("On Start Effects")]
        [UnityEngine.SerializeReference]
        public EffectData[] OnStartEffects = System.Array.Empty<EffectData>();

        [EditorLabel("On Tick Effects")]
        [UnityEngine.SerializeReference]
        public EffectData[] OnTickEffects = System.Array.Empty<EffectData>();

        [EditorLabel("On End Effects")]
        [UnityEngine.SerializeReference]
        public EffectData[] OnEndEffects = System.Array.Empty<EffectData>();

        public override EffectData CreateRuntimeCopy(
            SkillModifierSet modifiers,
            UnitElementComponent? elementComponent = null)
        {
            float attributePower = ResolveAttributePower(elementComponent);
            SkillModifierSet runtimeModifiers = CreateModifiersWithAttributePower(modifiers, attributePower);
            if (Element != ElementType.None)
            {
                runtimeModifiers.Add(new SkillModifierEntry
                {
                    Channel = SkillModifierChannel.BuffDuration,
                    Factor = GetAttributePowerValue(runtimeModifiers),
                });
            }

            PersistentEffectData copy = (PersistentEffectData)base.CreateRuntimeCopy(runtimeModifiers, elementComponent);
            copy.PlacementCount = Unity.Mathematics.math.max(1, PlacementCount);
            copy.PlacementRadius = ApplyModifierNonNegative(modifiers, SkillModifierChannel.AreaRadius, PlacementRadius);
            copy.TotalDuration = ApplyModifierNonNegative(runtimeModifiers, SkillModifierChannel.EffectDuration, TotalDuration);
            copy.TotalDuration = ApplyModifierNonNegative(runtimeModifiers, SkillModifierChannel.BuffDuration, copy.TotalDuration);
            copy.TickIntervalSeconds = ApplyModifierNonNegative(runtimeModifiers, SkillModifierChannel.TickInterval, TickIntervalSeconds);
            copy.OnStartEffects = CreateRuntimeCopies(OnStartEffects, modifiers, elementComponent);
            copy.OnTickEffects = CreateRuntimeCopies(OnTickEffects, modifiers, elementComponent);
            copy.OnEndEffects = CreateRuntimeCopies(OnEndEffects, modifiers, elementComponent);
            return copy;
        }

    }
}
