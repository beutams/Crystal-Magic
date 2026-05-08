using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;

[UpdateBefore(typeof(UnitMoveSystem))]
partial class UnitBuffSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;
        Dictionary<Entity, PropertyModifierSet> modifiersByEntity = new();

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

            modifiersByEntity[entity] = modifiers;
        }

        foreach ((RefRW<UnitMoveComponent> move, Entity entity) in
            SystemAPI.Query<RefRW<UnitMoveComponent>>().WithAll<UnitBuffElement>().WithEntityAccess())
        {
            if (!modifiersByEntity.TryGetValue(entity, out PropertyModifierSet modifiers))
                continue;

            move.ValueRW.SpeedFactor = modifiers.GetFactor(PropertyModifierChannel.MoveSpeed);
            move.ValueRW.SpeedBonus = modifiers.GetBonus(PropertyModifierChannel.MoveSpeed);
        }

        foreach ((RefRW<UnitVitalityComponent> vitality, Entity entity) in
            SystemAPI.Query<RefRW<UnitVitalityComponent>>().WithAll<UnitBuffElement>().WithEntityAccess())
        {
            if (!modifiersByEntity.TryGetValue(entity, out PropertyModifierSet modifiers))
                continue;

            vitality.ValueRW.HealthFactor = modifiers.GetFactor(PropertyModifierChannel.MaxHealth);
            vitality.ValueRW.HealthBonus = modifiers.GetBonus(PropertyModifierChannel.MaxHealth);
            vitality.ValueRW.HealthRegenFactor = modifiers.GetFactor(PropertyModifierChannel.HealthRegen);
            vitality.ValueRW.HealthRegenBonus = modifiers.GetBonus(PropertyModifierChannel.HealthRegen);
            vitality.ValueRW.DefenseFactor = modifiers.GetFactor(PropertyModifierChannel.Defense);
            vitality.ValueRW.DefenseBonus = modifiers.GetBonus(PropertyModifierChannel.Defense);
        }

        foreach ((RefRW<UnitAttackComponent> attack, Entity entity) in
            SystemAPI.Query<RefRW<UnitAttackComponent>>().WithAll<UnitBuffElement>().WithEntityAccess())
        {
            if (!modifiersByEntity.TryGetValue(entity, out PropertyModifierSet modifiers))
                continue;

            attack.ValueRW.AttackFactor = modifiers.GetFactor(PropertyModifierChannel.AttackPower);
            attack.ValueRW.AttackBonus = modifiers.GetBonus(PropertyModifierChannel.AttackPower);
            attack.ValueRW.RangeFactor = modifiers.GetFactor(PropertyModifierChannel.SkillRange);
            attack.ValueRW.RangeBonus = modifiers.GetBonus(PropertyModifierChannel.SkillRange);
            attack.ValueRW.ActionSpeedFactor = modifiers.GetFactor(PropertyModifierChannel.ActionSpeed);
            attack.ValueRW.ActionSpeedBonus = modifiers.GetBonus(PropertyModifierChannel.ActionSpeed);
            attack.ValueRW.ChantSpeedFactor = modifiers.GetFactor(PropertyModifierChannel.ChantSpeed);
            attack.ValueRW.ChantSpeedBonus = modifiers.GetBonus(PropertyModifierChannel.ChantSpeed);
        }

        foreach ((RefRW<UnitElementComponent> element, RefRO<UnitElementBaseComponent> elementBase, Entity entity) in
            SystemAPI.Query<RefRW<UnitElementComponent>, RefRO<UnitElementBaseComponent>>().WithAll<UnitBuffElement>().WithEntityAccess())
        {
            if (!modifiersByEntity.TryGetValue(entity, out PropertyModifierSet modifiers))
                continue;

            element.ValueRW.WaterPower = elementBase.ValueRO.WaterPower + element.ValueRO.EquipmentWaterPower + modifiers.GetBonus(PropertyModifierChannel.WaterPower);
            element.ValueRW.FirePower = elementBase.ValueRO.FirePower + element.ValueRO.EquipmentFirePower + modifiers.GetBonus(PropertyModifierChannel.FirePower);
            element.ValueRW.LightningPower = elementBase.ValueRO.LightningPower + element.ValueRO.EquipmentLightningPower + modifiers.GetBonus(PropertyModifierChannel.LightningPower);
            element.ValueRW.WindPower = elementBase.ValueRO.WindPower + element.ValueRO.EquipmentWindPower + modifiers.GetBonus(PropertyModifierChannel.WindPower);
        }

        foreach ((RefRW<UnitManaComponent> mana, Entity entity) in
            SystemAPI.Query<RefRW<UnitManaComponent>>().WithAll<UnitBuffElement>().WithEntityAccess())
        {
            if (!modifiersByEntity.TryGetValue(entity, out PropertyModifierSet modifiers))
                continue;

            mana.ValueRW.MpFactor = modifiers.GetFactor(PropertyModifierChannel.MaxMp);
            mana.ValueRW.MpBonus = modifiers.GetBonus(PropertyModifierChannel.MaxMp);
            mana.ValueRW.MpRegenFactor = modifiers.GetFactor(PropertyModifierChannel.MpRegen);
            mana.ValueRW.MpRegenBonus = modifiers.GetBonus(PropertyModifierChannel.MpRegen);
        }
    }
}
