using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;

[UpdateInGroup(typeof(UnitInitializationSystemGroup))]
[UpdateBefore(typeof(UnitBuffSystem))]
partial class PlayerEquipmentPropertySystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<PlayerTag>();
    }

    protected override void OnUpdate()
    {
        PropertyModifierSet totals = BuildTotals(SaveDataComponent.Instance?.GetEquipmentData());

        foreach ((RefRO<PlayerTag> _, RefRW<UnitMoveComponent> move) in SystemAPI.Query<RefRO<PlayerTag>, RefRW<UnitMoveComponent>>())
        {
            move.ValueRW.BaseMoveSpeedOffset = totals.GetBonus(PropertyModifierChannel.MoveSpeed);
        }

        foreach ((RefRO<PlayerTag> _, RefRW<UnitVitalityComponent> vitality) in SystemAPI.Query<RefRO<PlayerTag>, RefRW<UnitVitalityComponent>>())
        {
            vitality.ValueRW.BaseMaxHealthOffset = totals.GetBonus(PropertyModifierChannel.MaxHealth);
            vitality.ValueRW.BaseHealthRegenPerSecondOffset = totals.GetBonus(PropertyModifierChannel.HealthRegen);
            vitality.ValueRW.BaseDefenseOffset = totals.GetBonus(PropertyModifierChannel.Defense);
        }

        foreach ((RefRO<PlayerTag> _, RefRW<UnitManaComponent> mana) in SystemAPI.Query<RefRO<PlayerTag>, RefRW<UnitManaComponent>>())
        {
            mana.ValueRW.BaseMaxMpOffset = totals.GetBonus(PropertyModifierChannel.MaxMp);
            mana.ValueRW.BaseMpRegenPerSecondOffset = totals.GetBonus(PropertyModifierChannel.MpRegen);
        }

        foreach ((RefRO<PlayerTag> _, RefRW<UnitAttackComponent> attack) in SystemAPI.Query<RefRO<PlayerTag>, RefRW<UnitAttackComponent>>())
        {
            attack.ValueRW.BaseAttackPowerOffset = totals.GetBonus(PropertyModifierChannel.AttackPower);
            attack.ValueRW.BaseSkillRangeOffset = totals.GetBonus(PropertyModifierChannel.SkillRange);
            attack.ValueRW.BaseActionSpeedBonusOffset = totals.GetBonus(PropertyModifierChannel.ActionSpeed);
            attack.ValueRW.BaseChantSpeedBonusOffset = totals.GetBonus(PropertyModifierChannel.ChantSpeed);
        }

        foreach ((RefRO<PlayerTag> _, RefRW<UnitElementComponent> element, RefRO<UnitElementBaseComponent> elementBase) in
                 SystemAPI.Query<RefRO<PlayerTag>, RefRW<UnitElementComponent>, RefRO<UnitElementBaseComponent>>())
        {
            element.ValueRW.EquipmentWaterPower = totals.GetBonus(PropertyModifierChannel.WaterPower);
            element.ValueRW.EquipmentFirePower = totals.GetBonus(PropertyModifierChannel.FirePower);
            element.ValueRW.EquipmentLightningPower = totals.GetBonus(PropertyModifierChannel.LightningPower);
            element.ValueRW.EquipmentWindPower = totals.GetBonus(PropertyModifierChannel.WindPower);
            element.ValueRW.WaterPower = elementBase.ValueRO.WaterPower + element.ValueRO.EquipmentWaterPower;
            element.ValueRW.FirePower = elementBase.ValueRO.FirePower + element.ValueRO.EquipmentFirePower;
            element.ValueRW.LightningPower = elementBase.ValueRO.LightningPower + element.ValueRO.EquipmentLightningPower;
            element.ValueRW.WindPower = elementBase.ValueRO.WindPower + element.ValueRO.EquipmentWindPower;
        }
    }

    private static PropertyModifierSet BuildTotals(EquipmentData equipmentData)
    {
        PropertyModifierSet totals = new();
        if (equipmentData == null || DataComponent.Instance == null)
            return totals;

        AddEquipmentItem(totals, equipmentData.MagicStoneId);
        if (equipmentData.SpiritSlots != null)
        {
            for (int i = 0; i < equipmentData.SpiritSlots.Length; i++)
                AddEquipmentItem(totals, equipmentData.SpiritSlots[i]);
        }

        return totals;
    }

    private static void AddEquipmentItem(PropertyModifierSet totals, int itemId)
    {
        if (itemId < 0)
            return;

        ItemData itemData = DataComponent.Instance.Get<ItemData>(itemId);
        if (itemData == null)
            return;

        if (itemData.ItemType != ItemType.MagicStone && itemData.ItemType != ItemType.Spirit)
            return;

        if (itemData.ExtraId < 0)
            return;

        EquipData equipData = DataComponent.Instance.Get<EquipData>(itemData.ExtraId);
        if (equipData?.Properties == null)
            return;

        for (int i = 0; i < equipData.Properties.Count; i++)
        {
            EquipPropertyEntry entry = equipData.Properties[i];
            totals.Add(new PropertyModifierEntry
            {
                Channel = entry.Channel,
                Bonus = entry.BaseBonus,
            });
        }
    }
}
