using CrystalMagic.Core;
using CrystalMagic.Game.Data.Effects;
using UnityEngine;
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
            DamageBreakdown breakdown = CalculateDamage(context, entityManager, vitality);
            float damage = breakdown.FinalDamage;
            damage = UnitBuffUtility.ApplyDamageTakenRuntimeBuffs(entityManager, target, damage);
            if (damage <= 0f)
                return;

            float previousHealth = vitality.CurrentHealth;
            vitality.CurrentHealth = math.max(0f, vitality.CurrentHealth - damage);
            entityManager.SetComponentData(target, vitality);

            Debug.Log(
                $"[DamageEffect] Damage={damage:0.##} | Formula=max(0, AttackPower*Coeff+Flat-Defense) | " +
                $"AttackPower={breakdown.AttackPower:0.##} Coeff={Data.DamageCoefficient:0.##} Flat={Data.FlatDamageBonus:0.##} " +
                $"Raw={breakdown.RawDamage:0.##} Defense={breakdown.Defense:0.##} Final={breakdown.FinalDamage:0.##} " +
                $"Target={target.Index}:{target.Version} HP={previousHealth:0.##}->{vitality.CurrentHealth:0.##}");

            if (vitality.CurrentHealth <= 0f)
            {
                if (!entityManager.HasComponent<DestroyEntityFlag>(target))
                    entityManager.AddComponent<DestroyEntityFlag>(target);

                entityManager.SetComponentEnabled<DestroyEntityFlag>(target, true);
            }

            EventComponent.Instance.Publish(new UnitDamagedEvent(target, vitality.CurrentHealth, vitality.RealMaxHealth));
        }

        private DamageBreakdown CalculateDamage(SkillContent context, EntityManager entityManager, UnitVitalityComponent targetVitality)
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
            return new DamageBreakdown
            {
                AttackPower = attackPower,
                RawDamage = rawDamage,
                Defense = targetVitality.RealDefense,
                FinalDamage = math.max(0f, rawDamage - targetVitality.RealDefense),
            };
        }

        private struct DamageBreakdown
        {
            public float AttackPower;
            public float RawDamage;
            public float Defense;
            public float FinalDamage;
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
            UnitControlUtility.ApplyKnockback(entityManager, target, origin, direction, Data.Force, Data.DurationSeconds);
        }
    }

    public sealed class StunEffect : Effect
    {
        public new StunEffectData Data { get; }

        public StunEffect(StunEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null || context == null || !context.HasTargetEntity)
                return;

            EntityManager entityManager = context.EntityManager;
            Entity target = context.TargetEntity;
            Entity source = context.HasOriginEntity ? context.OriginEntity : Entity.Null;
            UnitControlUtility.ApplyStun(entityManager, target, source, Data.DurationSeconds);
        }
    }

    public sealed class FearEffect : Effect
    {
        public new FearEffectData Data { get; }

        public FearEffect(FearEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null || context == null || !context.HasTargetEntity || !context.HasOriginEntity)
                return;

            EntityManager entityManager = context.EntityManager;
            Entity target = context.TargetEntity;
            Entity source = context.OriginEntity;
            UnitControlUtility.ApplyFear(entityManager, target, source, Data.DurationSeconds);
        }
    }
}
