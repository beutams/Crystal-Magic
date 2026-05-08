using CrystalMagic.Core;
using CrystalMagic.Game.Data.Effects;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrystalMagic.Game.Skill.Effects
{
    /// <summary>
    /// 伤害效果，逻辑由战斗结算系统接入
    /// </summary>
    public sealed class DamageEffect : Effect
    {
        public new DamageEffectData Data { get; }

        public DamageEffect(DamageEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null || context == null || !context.HasTargetEntity)
                return;

            EntityManager entityManager = context.EntityManager;
            Entity target = context.TargetEntity;
            if (target == Entity.Null ||
                !entityManager.Exists(target) ||
                !entityManager.HasComponent<UnitVitalityComponent>(target))
                return;

            UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(target);
            float damage = CalculateDamage(context, entityManager, vitality);
            if (damage <= 0f)
                return;

            vitality.CurrentHealth = math.max(0f, vitality.CurrentHealth - damage);
            entityManager.SetComponentData(target, vitality);
            EventComponent.Instance.Publish(new UnitDamagedEvent(target, vitality.CurrentHealth, vitality.RealMaxHealth));
        }

        private float CalculateDamage(SkillContent context, EntityManager entityManager, UnitVitalityComponent targetVitality)
        {
            float attackPower = 0f;
            if (context.HasOriginEntity &&
                context.OriginEntity != Entity.Null &&
                entityManager.Exists(context.OriginEntity) &&
                entityManager.HasComponent<UnitAttackComponent>(context.OriginEntity))
            {
                attackPower = entityManager.GetComponentData<UnitAttackComponent>(context.OriginEntity).RealAttackPower;
            }

            float rawDamage = attackPower * Data.DamageCoefficient + Data.FlatDamageBonus;
            return math.max(0f, rawDamage - targetVitality.RealDefense);
        }
    }

    public sealed class KnockbackEffect : Effect
    {
        public new KnockbackEffectData Data { get; }

        public KnockbackEffect(KnockbackEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null ||
                context == null ||
                !context.HasTargetEntity ||
                !context.HasOriginEntity)
            {
                return;
            }

            EntityManager entityManager = context.EntityManager;
            Entity target = context.TargetEntity;
            Entity origin = context.OriginEntity;
            if (target == Entity.Null ||
                origin == Entity.Null ||
                !entityManager.Exists(target) ||
                !entityManager.Exists(origin) ||
                !entityManager.HasComponent<LocalTransform>(target) ||
                !entityManager.HasComponent<LocalTransform>(origin))
            {
                return;
            }

            LocalTransform targetTransform = entityManager.GetComponentData<LocalTransform>(target);
            float3 originPosition = entityManager.GetComponentData<LocalTransform>(origin).Position;
            float2 direction = targetTransform.Position.xy - originPosition.xy;
            if (math.lengthsq(direction) <= 0.0001f)
                direction = new float2(1f, 0f);
            else
                direction = math.normalize(direction);

            targetTransform.Position.xy += direction * Data.Force;
            entityManager.SetComponentData(target, targetTransform);
        }
    }
}
