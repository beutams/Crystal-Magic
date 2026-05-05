using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;

[UpdateBefore(typeof(UnitMoveSystem))]
partial class UnitBuffSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;

        foreach ((DynamicBuffer<UnitBuffElement> queryBuffer, Entity entity) in
            SystemAPI.Query<DynamicBuffer<UnitBuffElement>>().WithEntityAccess())
        {
            DynamicBuffer<UnitBuffElement> buffBuffer = queryBuffer;
            for (int i = buffBuffer.Length - 1; i >= 0; i--)
            {
                UnitBuffElement element = buffBuffer[i];
                element.RemainingTime -= dt;
                if (element.RemainingTime <= 0f)
                {
                    buffBuffer.RemoveAt(i);
                    continue;
                }

                buffBuffer[i] = element;
            }

            PropertyModifierSet modifiers = new();
            for (int i = 0; i < buffBuffer.Length; i++)
            {
                UnitBuffElement buffElement = buffBuffer[i];
                if (DataComponent.Instance.Get<BuffData>(buffElement.BuffId) is not PropertyBuffData propertyBuff)
                    continue;

                modifiers.Add(propertyBuff.PropertyModifiers, buffElement.StackCount > 0 ? buffElement.StackCount : 1);
            }

            if (EntityManager.HasComponent<UnitMoveComponent>(entity))
            {
                UnitMoveComponent move = EntityManager.GetComponentData<UnitMoveComponent>(entity);
                move.SpeedFactor = modifiers.GetFactor(PropertyModifierChannel.MoveSpeed);
                move.SpeedBonus = modifiers.GetBonus(PropertyModifierChannel.MoveSpeed);
                EntityManager.SetComponentData(entity, move);
            }

            if (EntityManager.HasComponent<UnitVitalityComponent>(entity))
            {
                UnitVitalityComponent vitality = EntityManager.GetComponentData<UnitVitalityComponent>(entity);
                vitality.HealthFactor = modifiers.GetFactor(PropertyModifierChannel.MaxHealth);
                vitality.HealthBonus = modifiers.GetBonus(PropertyModifierChannel.MaxHealth);
                vitality.HealthRegenFactor = modifiers.GetFactor(PropertyModifierChannel.HealthRegen);
                vitality.HealthRegenBonus = modifiers.GetBonus(PropertyModifierChannel.HealthRegen);
                vitality.DefenseFactor = modifiers.GetFactor(PropertyModifierChannel.Defense);
                vitality.DefenseBonus = modifiers.GetBonus(PropertyModifierChannel.Defense);
                EntityManager.SetComponentData(entity, vitality);
            }

            if (EntityManager.HasComponent<UnitAttackComponent>(entity))
            {
                UnitAttackComponent attack = EntityManager.GetComponentData<UnitAttackComponent>(entity);
                attack.AttackFactor = modifiers.GetFactor(PropertyModifierChannel.AttackPower);
                attack.AttackBonus = modifiers.GetBonus(PropertyModifierChannel.AttackPower);
                attack.RangeFactor = modifiers.GetFactor(PropertyModifierChannel.SkillRange);
                attack.RangeBonus = modifiers.GetBonus(PropertyModifierChannel.SkillRange);
                attack.ActionSpeedFactor = modifiers.GetFactor(PropertyModifierChannel.ActionSpeed);
                attack.ActionSpeedBonus = modifiers.GetBonus(PropertyModifierChannel.ActionSpeed);
                attack.ChantSpeedFactor = modifiers.GetFactor(PropertyModifierChannel.ChantSpeed);
                attack.ChantSpeedBonus = modifiers.GetBonus(PropertyModifierChannel.ChantSpeed);
                EntityManager.SetComponentData(entity, attack);
            }

            if (EntityManager.HasComponent<UnitElementComponent>(entity))
            {
                UnitElementComponent element = EntityManager.GetComponentData<UnitElementComponent>(entity);
                element.WaterPowerFactor = modifiers.GetFactor(PropertyModifierChannel.WaterPower);
                element.WaterPowerBonus = modifiers.GetBonus(PropertyModifierChannel.WaterPower);
                element.FirePowerFactor = modifiers.GetFactor(PropertyModifierChannel.FirePower);
                element.FirePowerBonus = modifiers.GetBonus(PropertyModifierChannel.FirePower);
                element.LightningPowerFactor = modifiers.GetFactor(PropertyModifierChannel.LightningPower);
                element.LightningPowerBonus = modifiers.GetBonus(PropertyModifierChannel.LightningPower);
                element.WindPowerFactor = modifiers.GetFactor(PropertyModifierChannel.WindPower);
                element.WindPowerBonus = modifiers.GetBonus(PropertyModifierChannel.WindPower);
                EntityManager.SetComponentData(entity, element);
            }

            if (EntityManager.HasComponent<UnitManaComponent>(entity))
            {
                UnitManaComponent mana = EntityManager.GetComponentData<UnitManaComponent>(entity);
                mana.MpFactor = modifiers.GetFactor(PropertyModifierChannel.MaxMp);
                mana.MpBonus = modifiers.GetBonus(PropertyModifierChannel.MaxMp);
                mana.MpRegenFactor = modifiers.GetFactor(PropertyModifierChannel.MpRegen);
                mana.MpRegenBonus = modifiers.GetBonus(PropertyModifierChannel.MpRegen);
                EntityManager.SetComponentData(entity, mana);
            }
        }
    }
}
