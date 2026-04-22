using CrystalMagic.Game.Data.Effects;
using Unity.Entities;
using Unity.Mathematics;

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
}
