using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;

[UpdateBefore(typeof(UnitBuffSystem))]
partial class PlayerEquipmentPropertySystem : SystemBase
{
    private struct EquipmentPropertyTotals
    {
        public float MoveSpeed;
        public float MaxHealth;
        public float Defense;
        public float AttackPower;
        public float SkillRange;
        public float MaxMp;
        public float HealthRegen;
        public float MpRegen;
        public float ActionSpeed;
        public float ChantSpeed;
        public float WaterPower;
        public float FirePower;
        public float LightningPower;
        public float WindPower;
    }

    protected override void OnCreate()
    {
        RequireForUpdate<PlayerTag>();
    }

    protected override void OnUpdate()
    {
        EquipmentPropertyTotals totals = BuildTotals(SaveDataComponent.Instance?.GetEquipmentData());

        foreach ((RefRO<PlayerTag> _, Entity entity) in SystemAPI.Query<RefRO<PlayerTag>>().WithEntityAccess())
        {
            ApplyOffsets(entity, totals);
        }
    }

    private static EquipmentPropertyTotals BuildTotals(EquipmentData equipmentData)
    {
        EquipmentPropertyTotals totals = default;
        if (equipmentData == null || DataComponent.Instance == null)
            return totals;

        AddEquipmentItem(ref totals, equipmentData.StaffId);
        if (equipmentData.BonusSlots != null)
        {
            for (int i = 0; i < equipmentData.BonusSlots.Length; i++)
                AddEquipmentItem(ref totals, equipmentData.BonusSlots[i]);
        }

        return totals;
    }

    private static void AddEquipmentItem(ref EquipmentPropertyTotals totals, int itemId)
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
            switch (entry.Channel)
            {
                case PropertyModifierChannel.MoveSpeed:
                    totals.MoveSpeed += entry.BaseBonus;
                    break;
                case PropertyModifierChannel.MaxHealth:
                    totals.MaxHealth += entry.BaseBonus;
                    break;
                case PropertyModifierChannel.Defense:
                    totals.Defense += entry.BaseBonus;
                    break;
                case PropertyModifierChannel.AttackPower:
                    totals.AttackPower += entry.BaseBonus;
                    break;
                case PropertyModifierChannel.SkillRange:
                    totals.SkillRange += entry.BaseBonus;
                    break;
                case PropertyModifierChannel.MaxMp:
                    totals.MaxMp += entry.BaseBonus;
                    break;
                case PropertyModifierChannel.HealthRegen:
                    totals.HealthRegen += entry.BaseBonus;
                    break;
                case PropertyModifierChannel.MpRegen:
                    totals.MpRegen += entry.BaseBonus;
                    break;
                case PropertyModifierChannel.ActionSpeed:
                    totals.ActionSpeed += entry.BaseBonus;
                    break;
                case PropertyModifierChannel.ChantSpeed:
                    totals.ChantSpeed += entry.BaseBonus;
                    break;
                case PropertyModifierChannel.WaterPower:
                    totals.WaterPower += entry.BaseBonus;
                    break;
                case PropertyModifierChannel.FirePower:
                    totals.FirePower += entry.BaseBonus;
                    break;
                case PropertyModifierChannel.LightningPower:
                    totals.LightningPower += entry.BaseBonus;
                    break;
                case PropertyModifierChannel.WindPower:
                    totals.WindPower += entry.BaseBonus;
                    break;
            }
        }
    }

    private void ApplyOffsets(Entity entity, EquipmentPropertyTotals totals)
    {
        if (EntityManager.HasComponent<UnitMoveComponent>(entity))
        {
            UnitMoveComponent move = EntityManager.GetComponentData<UnitMoveComponent>(entity);
            move.BaseMoveSpeedOffset = totals.MoveSpeed;
            EntityManager.SetComponentData(entity, move);
        }

        if (EntityManager.HasComponent<UnitVitalityComponent>(entity))
        {
            UnitVitalityComponent vitality = EntityManager.GetComponentData<UnitVitalityComponent>(entity);
            vitality.BaseMaxHealthOffset = totals.MaxHealth;
            vitality.BaseHealthRegenPerSecondOffset = totals.HealthRegen;
            vitality.BaseDefenseOffset = totals.Defense;
            EntityManager.SetComponentData(entity, vitality);
        }

        if (EntityManager.HasComponent<UnitManaComponent>(entity))
        {
            UnitManaComponent mana = EntityManager.GetComponentData<UnitManaComponent>(entity);
            mana.BaseMaxMpOffset = totals.MaxMp;
            mana.BaseMpRegenPerSecondOffset = totals.MpRegen;
            EntityManager.SetComponentData(entity, mana);
        }

        if (EntityManager.HasComponent<UnitAttackComponent>(entity))
        {
            UnitAttackComponent attack = EntityManager.GetComponentData<UnitAttackComponent>(entity);
            attack.BaseAttackPowerOffset = totals.AttackPower;
            attack.BaseSkillRangeOffset = totals.SkillRange;
            attack.BaseActionSpeedBonusOffset = totals.ActionSpeed;
            attack.BaseChantSpeedBonusOffset = totals.ChantSpeed;
            EntityManager.SetComponentData(entity, attack);
        }

        if (EntityManager.HasComponent<UnitElementComponent>(entity))
        {
            UnitElementComponent element = EntityManager.GetComponentData<UnitElementComponent>(entity);
            element.BaseWaterPowerBonusOffset = totals.WaterPower;
            element.BaseFirePowerBonusOffset = totals.FirePower;
            element.BaseLightningPowerBonusOffset = totals.LightningPower;
            element.BaseWindPowerBonusOffset = totals.WindPower;
            EntityManager.SetComponentData(entity, element);
        }
    }
}
