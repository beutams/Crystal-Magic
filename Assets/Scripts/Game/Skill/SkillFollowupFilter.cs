using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;

namespace CrystalMagic.Game.Skill
{
    public abstract class SkillFollowupFilter
    {
        public abstract bool TryInitializeRuntime(SkillFollowupFilterData filterData, SkillFollowupRuntimeState followup);

        public abstract bool IsMatch(SkillFollowupRuntimeState followup, in SkillFollowupContext context);

        protected static bool SkillUsesElement(EffectData[] effectChain, ElementType element)
        {
            if (effectChain == null || effectChain.Length == 0 || element == ElementType.None)
                return false;

            for (int i = 0; i < effectChain.Length; i++)
            {
                if (EffectUsesElement(effectChain[i], element))
                    return true;
            }

            return false;
        }

        private static bool EffectUsesElement(EffectData effectData, ElementType element)
        {
            if (effectData == null)
                return false;

            if (effectData is DamageEffectData damageEffectData && damageEffectData.Element == element)
                return true;

            if (effectData is PersistentEffectData persistentEffectData && persistentEffectData.Element == element)
                return true;

            System.Reflection.FieldInfo[] fields = effectData.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                System.Reflection.FieldInfo field = fields[i];
                if (field.FieldType != typeof(EffectData[]) && !(field.FieldType.IsArray && typeof(EffectData).IsAssignableFrom(field.FieldType.GetElementType())))
                    continue;

                if (field.GetValue(effectData) is EffectData[] nestedEffects && SkillUsesElement(nestedEffects, element))
                    return true;
            }

            return false;
        }
    }
}
