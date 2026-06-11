using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill.Effects;
using Unity.Entities;

namespace CrystalMagic.Game.Skill
{
    public static class SkillExecutor
    {
        private static ComparatorFactory s_comparatorFactory;

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
                if (effectData == null || !PassEffectConditions(effectData, context))
                    continue;

                EffectData runtimeEffectData = effectData;
                if (context?.RuntimeModifiers != null)
                    runtimeEffectData = effectData.CreateRuntimeCopy(context.RuntimeModifiers);

                Effect effect = CreateEffect(runtimeEffectData);
                effect?.Execute(context);
            }
        }

        private static bool PassEffectConditions(EffectData effectData, SkillContent context)
        {
            if (effectData?.Conditions == null || effectData.Conditions.Count == 0)
                return true;

            EntityManager entityManager = GetEntityManager(context);
            if (!TryGetConditionEntity(context, entityManager, out Entity conditionEntity))
                return false;

            Comparator comparator = GetComparatorFactory().BuildComparator(
                effectData.Conditions,
                conditionEntity,
                entityManager,
                context != null ? context.OriginEntity : Entity.Null,
                context != null && context.HasOriginEntity);
            return comparator.GetResult();
        }

        private static bool TryGetConditionEntity(SkillContent context, EntityManager entityManager, out Entity conditionEntity)
        {
            if (context != null &&
                context.HasTargetEntity &&
                context.TargetEntity != Entity.Null &&
                entityManager.Exists(context.TargetEntity))
            {
                conditionEntity = context.TargetEntity;
                return true;
            }

            if (context != null &&
                context.HasOriginEntity &&
                context.OriginEntity != Entity.Null &&
                entityManager.Exists(context.OriginEntity))
            {
                conditionEntity = context.OriginEntity;
                return true;
            }

            conditionEntity = Entity.Null;
            return false;
        }

        private static ComparatorFactory GetComparatorFactory()
        {
            if (s_comparatorFactory != null)
                return s_comparatorFactory;

            s_comparatorFactory = new ComparatorFactory();
            ComparatorRegistry.RegisterAll(s_comparatorFactory);
            return s_comparatorFactory;
        }

        private static EntityManager GetEntityManager(SkillContent context)
        {
            if (context != null)
                return context.EntityManager;

            return World.DefaultGameObjectInjectionWorld.EntityManager;
        }

        private static Effect CreateEffect(EffectData effectData)
        {
            return effectData switch
            {
                ApplyBuffEffectData data => new ApplyBuffEffect(data),
                AreaSearchEffectData data => new AreaSearchEffect(data),
                ChainSearchEffectData data => new ChainSearchEffect(data),
                ConeSearchEffectData data => new ConeSearchEffect(data),
                RandomAreaPointEffectData data => new RandomAreaPointEffect(data),
                ReadBuffStackEffectData data => new ReadBuffStackEffect(data),
                RemoveBuffEffectData data => new RemoveBuffEffect(data),
                CameraShakeEffectData data => new CameraShakeEffect(data),
                DamageEffectData data => new DamageEffect(data),
                FearEffectData data => new FearEffect(data),
                ForwardRectSearchEffectData data => new ForwardRectSearchEffect(data),
                HealEffectData data => new HealEffect(data),
                HealthCostEffectData data => new HealthCostEffect(data),
                KnockbackEffectData data => new KnockbackEffect(data),
                PersistentBeamEffectData data => new PersistentBeamEffect(data),
                PersistentEffectData data => new PersistentEffect(data),
                RestoreManaEffectData data => new RestoreManaEffect(data),
                SpawnProjectileEffectData data => new SpawnProjectileEffect(data),
                SpawnSoundEffectData data => new SpawnSoundEffect(data),
                SpawnUnitEffectData data => new SpawnUnitEffect(data),
                SpawnVfxEffectData data => new SpawnVfxEffect(data),
                StunEffectData data => new StunEffect(data),
                _ => null,
            };
        }
    }
}
