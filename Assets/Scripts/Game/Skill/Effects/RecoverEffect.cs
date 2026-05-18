using CrystalMagic.Core;
using CrystalMagic.Game.Data.Effects;
using Unity.Entities;
using Unity.Mathematics;

namespace CrystalMagic.Game.Skill.Effects
{
    public sealed class HealEffect : Effect
    {
        public new HealEffectData Data { get; }

        public HealEffect(HealEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null || context == null || !context.HasTargetEntity)
                return;

            EntityManager entityManager = context.EntityManager;
            Entity target = context.TargetEntity;
            if (target == Entity.Null ||
                !entityManager.Exists(target) ||
                !entityManager.HasComponent<UnitVitalityComponent>(target))
            {
                return;
            }

            UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(target);
            float healAmount = CalculateHealAmount(context, entityManager);
            if (healAmount <= 0f)
                return;

            vitality.CurrentHealth = math.min(vitality.RealMaxHealth, vitality.CurrentHealth + healAmount);
            entityManager.SetComponentData(target, vitality);
            EventComponent.Instance.Publish(new UnitDamagedEvent(target, vitality.CurrentHealth, vitality.RealMaxHealth));
        }

        private float CalculateHealAmount(SkillContent context, EntityManager entityManager)
        {
            float attackPower = 0f;
            if (context.HasOriginEntity &&
                context.OriginEntity != Entity.Null &&
                entityManager.Exists(context.OriginEntity) &&
                entityManager.HasComponent<UnitAttackComponent>(context.OriginEntity))
            {
                attackPower = entityManager.GetComponentData<UnitAttackComponent>(context.OriginEntity).RealAttackPower;
            }

            return math.max(0f, attackPower * Data.HealCoefficient + Data.FlatHealBonus);
        }
    }

    public sealed class RestoreManaEffect : Effect
    {
        public new RestoreManaEffectData Data { get; }

        public RestoreManaEffect(RestoreManaEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null || context == null || !context.HasTargetEntity)
                return;

            EntityManager entityManager = context.EntityManager;
            Entity target = context.TargetEntity;
            if (target == Entity.Null ||
                !entityManager.Exists(target) ||
                !entityManager.HasComponent<UnitManaComponent>(target))
            {
                return;
            }

            UnitManaComponent mana = entityManager.GetComponentData<UnitManaComponent>(target);
            float manaRestoreAmount = CalculateManaRestoreAmount(context, entityManager);
            if (manaRestoreAmount <= 0f)
                return;

            mana.CurrentMana = math.min(mana.RealMaxMp, mana.CurrentMana + manaRestoreAmount);
            entityManager.SetComponentData(target, mana);
        }

        private float CalculateManaRestoreAmount(SkillContent context, EntityManager entityManager)
        {
            float attackPower = 0f;
            if (context.HasOriginEntity &&
                context.OriginEntity != Entity.Null &&
                entityManager.Exists(context.OriginEntity) &&
                entityManager.HasComponent<UnitAttackComponent>(context.OriginEntity))
            {
                attackPower = entityManager.GetComponentData<UnitAttackComponent>(context.OriginEntity).RealAttackPower;
            }

            return math.max(0f, attackPower * Data.ManaRestoreCoefficient + Data.FlatManaRestoreBonus);
        }
    }

    public sealed class HealthCostEffect : Effect
    {
        public new HealthCostEffectData Data { get; }

        public HealthCostEffect(HealthCostEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null || context == null || !context.HasOriginEntity)
                return;

            EntityManager entityManager = context.EntityManager;
            Entity origin = context.OriginEntity;
            if (origin == Entity.Null ||
                !entityManager.Exists(origin) ||
                !entityManager.HasComponent<UnitVitalityComponent>(origin))
            {
                return;
            }

            UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(origin);
            if (vitality.CurrentHealth <= 0f)
                return;

            float healthCost = math.max(0f, vitality.RealMaxHealth * Data.MaxHealthCoefficient + Data.FlatHealthCost);
            if (healthCost <= 0f)
                return;

            vitality.CurrentHealth = math.max(1f, vitality.CurrentHealth - healthCost);
            entityManager.SetComponentData(origin, vitality);
            EventComponent.Instance.Publish(new UnitDamagedEvent(origin, vitality.CurrentHealth, vitality.RealMaxHealth));
        }
    }
}
