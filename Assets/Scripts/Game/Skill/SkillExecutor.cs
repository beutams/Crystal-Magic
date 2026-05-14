using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill.Effects;

namespace CrystalMagic.Game.Skill
{
    public static class SkillExecutor
    {
        public static void ExecuteSkill(SkillData skillData, SkillContent context)
        {
            if (skillData == null || skillData.EffectChain == null)
                return;

            ExecuteEffects(skillData.EffectChain, context);
        }

        public static void ExecuteSkill(ResolvedSkillData skillData, SkillContent context)
        {
            if (skillData == null || skillData.EffectChain == null)
                return;

            ExecuteEffects(skillData.EffectChain, context);
        }

        public static void ExecuteEffects(EffectData[] effects, SkillContent context)
        {
            if (effects == null)
                return;

            foreach (EffectData effectData in effects)
            {
                EffectData runtimeEffectData = effectData;
                if (effectData != null && context?.RuntimeModifiers != null)
                    runtimeEffectData = effectData.CreateRuntimeCopy(context.RuntimeModifiers);

                Effect effect = CreateEffect(runtimeEffectData);
                effect?.Execute(context);
            }
        }

        private static Effect CreateEffect(EffectData effectData)
        {
            return effectData switch
            {
                ApplyBuffEffectData data => new ApplyBuffEffect(data),
                AreaSearchEffectData data => new AreaSearchEffect(data),
                ReadBuffStackEffectData data => new ReadBuffStackEffect(data),
                RemoveBuffEffectData data => new RemoveBuffEffect(data),
                CameraShakeEffectData data => new CameraShakeEffect(data),
                DamageEffectData data => new DamageEffect(data),
                ForwardRectSearchEffectData data => new ForwardRectSearchEffect(data),
                HealEffectData data => new HealEffect(data),
                KnockbackEffectData data => new KnockbackEffect(data),
                PersistentEffectData data => new PersistentEffect(data),
                RestoreManaEffectData data => new RestoreManaEffect(data),
                SpawnProjectileEffectData data => new SpawnProjectileEffect(data),
                SpawnSoundEffectData data => new SpawnSoundEffect(data),
                SpawnVfxEffectData data => new SpawnVfxEffect(data),
                _ => null,
            };
        }
    }
}
