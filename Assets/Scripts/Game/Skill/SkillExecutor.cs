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
                Effect effect = CreateEffect(effectData);
                effect?.Execute(context);
            }
        }

        private static Effect CreateEffect(EffectData effectData)
        {
            return effectData switch
            {
                AreaSearchEffectData data => new AreaSearchEffect(data),
                CameraShakeEffectData data => new CameraShakeEffect(data),
                DamageEffectData data => new DamageEffect(data),
                PersistentEffectData data => new PersistentEffect(data),
                SpawnProjectileEffectData data => new SpawnProjectileEffect(data),
                SpawnSoundEffectData data => new SpawnSoundEffect(data),
                SpawnVfxEffectData data => new SpawnVfxEffect(data),
                _ => null,
            };
        }
    }
}
