using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using UnityEngine;

[UpdateBefore(typeof(UnitMoveSystem))]
partial class UnitBuffSystem : SystemBase
{
    private readonly SkillContent _buffTickContext = new();

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
                if (!TryUpdateBuffElement(entity, dt, ref element))
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

    private bool TryUpdateBuffElement(Entity entity, float deltaTime, ref UnitBuffElement element)
    {
        BuffData buffData = DataComponent.Instance?.Get<BuffData>(element.BuffId);
        if (buffData == null)
            return false;

        float effectiveDeltaTime = deltaTime;
        if (element.RemainingTime >= 0f)
        {
            effectiveDeltaTime = Mathf.Min(deltaTime, element.RemainingTime);
            element.RemainingTime -= deltaTime;
        }

        if (buffData is EffectBuffData effectBuffData)
            TickEffectBuff(entity, effectBuffData, effectiveDeltaTime, ref element);

        return element.RemainingTime < 0f || element.RemainingTime > 0f;
    }

    private void TickEffectBuff(Entity entity, EffectBuffData effectBuffData, float deltaTime, ref UnitBuffElement element)
    {
        if (effectBuffData.TickIntervalSeconds <= 0f ||
            effectBuffData.EffectChain == null ||
            effectBuffData.EffectChain.Length == 0 ||
            deltaTime <= 0f)
        {
            return;
        }

        if (element.NextTickTime <= 0f)
            element.NextTickTime = effectBuffData.TickIntervalSeconds;

        element.NextTickTime -= deltaTime;
        while (element.NextTickTime <= 0f)
        {
            ExecuteBuffTickEffects(entity, effectBuffData, element.StackCount);
            element.NextTickTime += effectBuffData.TickIntervalSeconds;
        }
    }

    private void ExecuteBuffTickEffects(Entity entity, EffectBuffData effectBuffData, int stackCount)
    {
        int executeCount = Mathf.Max(1, stackCount);
        _buffTickContext.EntityManager = EntityManager;
        _buffTickContext.HasOriginEntity = false;
        _buffTickContext.OriginEntity = Entity.Null;
        _buffTickContext.HasTargetEntity = true;
        _buffTickContext.TargetEntity = entity;
        _buffTickContext.HasTarget = false;
        _buffTickContext.Target = null;
        _buffTickContext.Origin = null;
        _buffTickContext.RuntimeModifiers = null;

        for (int i = 0; i < executeCount; i++)
            SkillExecutor.ExecuteEffects(effectBuffData.EffectChain, _buffTickContext);
    }
}
