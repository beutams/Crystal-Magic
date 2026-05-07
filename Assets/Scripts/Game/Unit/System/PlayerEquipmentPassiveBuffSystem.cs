using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;

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

        foreach ((RefRO<PlayerTag> _, Entity entity) in SystemAPI.Query<RefRO<PlayerTag>>().WithEntityAccess())
        {
            ApplyOffsets(entity, totals);
        }
    }

    private static PropertyModifierSet BuildTotals(EquipmentData equipmentData)
    {
        PropertyModifierSet totals = new();
        if (equipmentData == null || DataComponent.Instance == null)
            return totals;

        AddEquipmentItem(totals, equipmentData.StaffId);
        if (equipmentData.BonusSlots != null)
        {
            for (int i = 0; i < equipmentData.BonusSlots.Length; i++)
                AddEquipmentItem(totals, equipmentData.BonusSlots[i]);
        }

        return totals;
    }

    private static void AddEquipmentItem(PropertyModifierSet totals, int itemId)
    {
        if (itemId <= 0)
            return;

        ItemData itemData = DataComponent.Instance.Get<ItemData>(itemId);
        if (itemData == null)
            return;

        if (itemData.ItemType != ItemType.Weapon && itemData.ItemType != ItemType.Accessory)
            return;

        if (itemData.ExtraId <= 0)
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

    private void ApplyOffsets(Entity entity, PropertyModifierSet totals)
    {
        if (EntityManager.HasComponent<UnitMoveComponent>(entity))
        {
            UnitMoveComponent move = EntityManager.GetComponentData<UnitMoveComponent>(entity);
            move.BaseMoveSpeedOffset = totals.GetBonus(PropertyModifierChannel.MoveSpeed);
            EntityManager.SetComponentData(entity, move);
        }

        if (EntityManager.HasComponent<UnitVitalityComponent>(entity))
        {
            UnitVitalityComponent vitality = EntityManager.GetComponentData<UnitVitalityComponent>(entity);
            vitality.BaseMaxHealthOffset = totals.GetBonus(PropertyModifierChannel.MaxHealth);
            vitality.BaseHealthRegenPerSecondOffset = totals.GetBonus(PropertyModifierChannel.HealthRegen);
            vitality.BaseDefenseOffset = totals.GetBonus(PropertyModifierChannel.Defense);
            EntityManager.SetComponentData(entity, vitality);
        }

        if (EntityManager.HasComponent<UnitManaComponent>(entity))
        {
            UnitManaComponent mana = EntityManager.GetComponentData<UnitManaComponent>(entity);
            mana.BaseMaxMpOffset = totals.GetBonus(PropertyModifierChannel.MaxMp);
            mana.BaseMpRegenPerSecondOffset = totals.GetBonus(PropertyModifierChannel.MpRegen);
            EntityManager.SetComponentData(entity, mana);
        }

        if (EntityManager.HasComponent<UnitAttackComponent>(entity))
        {
            UnitAttackComponent attack = EntityManager.GetComponentData<UnitAttackComponent>(entity);
            attack.BaseAttackPowerOffset = totals.GetBonus(PropertyModifierChannel.AttackPower);
            attack.BaseSkillRangeOffset = totals.GetBonus(PropertyModifierChannel.SkillRange);
            attack.BaseActionSpeedBonusOffset = totals.GetBonus(PropertyModifierChannel.ActionSpeed);
            attack.BaseChantSpeedBonusOffset = totals.GetBonus(PropertyModifierChannel.ChantSpeed);
            EntityManager.SetComponentData(entity, attack);
        }

        if (EntityManager.HasComponent<UnitElementComponent>(entity))
        {
            UnitElementComponent element = EntityManager.GetComponentData<UnitElementComponent>(entity);
            UnitElementBaseComponent elementBase = EntityManager.GetComponentData<UnitElementBaseComponent>(entity);
            element.EquipmentWaterPower = totals.GetBonus(PropertyModifierChannel.WaterPower);
            element.EquipmentFirePower = totals.GetBonus(PropertyModifierChannel.FirePower);
            element.EquipmentLightningPower = totals.GetBonus(PropertyModifierChannel.LightningPower);
            element.EquipmentWindPower = totals.GetBonus(PropertyModifierChannel.WindPower);
            element.WaterPower = elementBase.WaterPower + element.EquipmentWaterPower;
            element.FirePower = elementBase.FirePower + element.EquipmentFirePower;
            element.LightningPower = elementBase.LightningPower + element.EquipmentLightningPower;
            element.WindPower = elementBase.WindPower + element.EquipmentWindPower;
            EntityManager.SetComponentData(entity, element);
        }
    }
}
