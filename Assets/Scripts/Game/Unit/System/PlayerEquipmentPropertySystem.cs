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
        PropertyModifierSet totals = BuildTotals(SaveDataComponent.Instance.GetEquipmentData());

        foreach ((RefRO<PlayerTag> _, Entity entity) in SystemAPI.Query<RefRO<PlayerTag>>().WithEntityAccess())
            UnitModifierUtility.ApplyEquipmentPropertyModifiers(EntityManager, entity, totals);
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
