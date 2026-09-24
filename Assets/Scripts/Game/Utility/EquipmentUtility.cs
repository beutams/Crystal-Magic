using System;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;

public static class EquipmentUtility
{
    public const int MagicStoneSlotIndex = 0;
    public const int SpiritSlotCount = 4;

    public static bool EnsureValid(EquipmentData equipmentData)
    {
        if (equipmentData == null)
            return false;

        if (equipmentData.SpiritSlots != null && equipmentData.SpiritSlots.Length == SpiritSlotCount)
            return false;

        int[] previousSlots = equipmentData.SpiritSlots;
        equipmentData.SpiritSlots = new int[SpiritSlotCount];
        for (int index = 0; index < equipmentData.SpiritSlots.Length; index++)
            equipmentData.SpiritSlots[index] = -1;
        if (previousSlots != null)
            Array.Copy(previousSlots, equipmentData.SpiritSlots, Math.Min(previousSlots.Length, SpiritSlotCount));

        return true;
    }

    public static int GetEquippedItemId(EquipmentData equipmentData, int equipSlotIndex)
    {
        if (equipmentData == null)
            return -1;

        if (equipSlotIndex == MagicStoneSlotIndex)
            return equipmentData.MagicStoneId;

        int spiritIndex = equipSlotIndex - 1;
        return equipmentData.SpiritSlots != null &&
               spiritIndex >= 0 &&
               spiritIndex < equipmentData.SpiritSlots.Length
            ? equipmentData.SpiritSlots[spiritIndex]
            : -1;
    }

    public static bool SetEquippedItemId(EquipmentData equipmentData, int equipSlotIndex, int itemId)
    {
        if (equipmentData == null || equipSlotIndex < MagicStoneSlotIndex || equipSlotIndex > SpiritSlotCount)
            return false;

        EnsureValid(equipmentData);
        if (GetEquippedItemId(equipmentData, equipSlotIndex) == itemId)
            return false;

        if (equipSlotIndex == MagicStoneSlotIndex)
            equipmentData.MagicStoneId = itemId;
        else
            equipmentData.SpiritSlots[equipSlotIndex - 1] = itemId;

        RebuildProperties(equipmentData);
        return true;
    }

    public static bool SwapSpiritSlots(EquipmentData equipmentData, int sourceSpiritIndex, int targetSpiritIndex)
    {
        if (equipmentData == null ||
            sourceSpiritIndex < 0 || sourceSpiritIndex >= SpiritSlotCount ||
            targetSpiritIndex < 0 || targetSpiritIndex >= SpiritSlotCount ||
            sourceSpiritIndex == targetSpiritIndex)
        {
            return false;
        }

        EnsureValid(equipmentData);
        int sourceItemId = equipmentData.SpiritSlots[sourceSpiritIndex];
        int targetItemId = equipmentData.SpiritSlots[targetSpiritIndex];
        if (sourceItemId == targetItemId)
            return false;

        equipmentData.SpiritSlots[sourceSpiritIndex] = targetItemId;
        equipmentData.SpiritSlots[targetSpiritIndex] = sourceItemId;
        return true;
    }

    public static void RebuildProperties(EquipmentData equipmentData)
    {
        if (equipmentData == null)
            return;

        EnsureValid(equipmentData);
        EquipmentPropertyData properties = default;
        AddEquipmentItem(ref properties, equipmentData.MagicStoneId);
        for (int index = 0; index < equipmentData.SpiritSlots.Length; index++)
            AddEquipmentItem(ref properties, equipmentData.SpiritSlots[index]);

        equipmentData.Properties = properties;
    }

    public static void ApplyToUnit(EntityManager entityManager, Entity entity, EquipmentData equipmentData)
    {
        if (equipmentData == null || entity == Entity.Null || !entityManager.Exists(entity))
            return;

        UnitModifierUtility.ApplyEquipmentProperties(entityManager, entity, in equipmentData.Properties);
    }

    private static void AddEquipmentItem(ref EquipmentPropertyData properties, int itemId)
    {
        if (itemId < 0)
            return;

        ItemData itemData = DataComponent.Instance.Get<ItemData>(itemId);
        if (itemData == null ||
            (itemData.ItemType != ItemType.MagicStone && itemData.ItemType != ItemType.Spirit) ||
            itemData.ExtraId < 0)
        {
            return;
        }

        EquipData equipData = DataComponent.Instance.Get<EquipData>(itemData.ExtraId);
        if (equipData?.Properties == null)
            return;

        for (int index = 0; index < equipData.Properties.Count; index++)
        {
            EquipPropertyEntry entry = equipData.Properties[index];
            AddBonus(ref properties, entry.Channel, entry.BaseBonus);
        }
    }

    private static void AddBonus(ref EquipmentPropertyData properties, PropertyModifierChannel channel, float bonus)
    {
        switch (channel)
        {
            case PropertyModifierChannel.MoveSpeed:
                properties.MoveSpeed += bonus;
                break;
            case PropertyModifierChannel.MaxHealth:
                properties.MaxHealth += bonus;
                break;
            case PropertyModifierChannel.Defense:
                properties.Defense += bonus;
                break;
            case PropertyModifierChannel.AttackPower:
                properties.AttackPower += bonus;
                break;
            case PropertyModifierChannel.SkillRange:
                properties.SkillRange += bonus;
                break;
            case PropertyModifierChannel.MaxMp:
                properties.MaxMp += bonus;
                break;
            case PropertyModifierChannel.HealthRegen:
                properties.HealthRegen += bonus;
                break;
            case PropertyModifierChannel.MpRegen:
                properties.MpRegen += bonus;
                break;
            case PropertyModifierChannel.ChantSpeed:
                properties.ChantSpeed += bonus;
                break;
            case PropertyModifierChannel.WaterPower:
                properties.WaterPower += bonus;
                break;
            case PropertyModifierChannel.FirePower:
                properties.FirePower += bonus;
                break;
            case PropertyModifierChannel.LightningPower:
                properties.LightningPower += bonus;
                break;
            case PropertyModifierChannel.WindPower:
                properties.WindPower += bonus;
                break;
        }
    }
}
