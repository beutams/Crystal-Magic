using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

[UpdateBefore(typeof(UnitBuffSystem))]
partial class PlayerEquipmentPassiveBuffSystem : SystemBase
{
    private readonly Dictionary<int, int> _passiveBuffStacks = new();
    private readonly List<UnitPassiveBuffElement> _resolvedPassiveBuffs = new();

    protected override void OnCreate()
    {
        RequireForUpdate<PlayerTag>();
    }

    protected override void OnUpdate()
    {
        if (SaveDataComponent.Instance == null || DataComponent.Instance == null)
            return;

        BuildPassiveBuffSnapshot(SaveDataComponent.Instance.GetEquipmentData());

        foreach ((RefRO<PlayerTag> _, DynamicBuffer<UnitPassiveBuffElement> passiveBuffs) in
            SystemAPI.Query<RefRO<PlayerTag>, DynamicBuffer<UnitPassiveBuffElement>>())
        {
            if (AreEqual(passiveBuffs, _resolvedPassiveBuffs))
                continue;

            passiveBuffs.Clear();
            for (int i = 0; i < _resolvedPassiveBuffs.Count; i++)
                passiveBuffs.Add(_resolvedPassiveBuffs[i]);
        }
    }

    private void BuildPassiveBuffSnapshot(EquipmentData equipmentData)
    {
        _passiveBuffStacks.Clear();
        _resolvedPassiveBuffs.Clear();

        if (equipmentData == null)
            return;

        AddEquipmentBuff(equipmentData.StaffId);
        if (equipmentData.BonusSlots != null)
        {
            for (int i = 0; i < equipmentData.BonusSlots.Length; i++)
                AddEquipmentBuff(equipmentData.BonusSlots[i]);
        }

        foreach (KeyValuePair<int, int> pair in _passiveBuffStacks)
        {
            _resolvedPassiveBuffs.Add(new UnitPassiveBuffElement
            {
                BuffId = pair.Key,
                StackCount = pair.Value,
            });
        }
    }

    private void AddEquipmentBuff(int itemId)
    {
        if (itemId <= 0)
            return;

        ItemData itemData = DataComponent.Instance.Get<ItemData>(itemId);
        if (itemData == null)
            return;

        if (itemData.ItemType != ItemType.Weapon && itemData.ItemType != ItemType.Accessory)
            return;

        int buffId = itemData.ExtraId;
        if (buffId <= 0)
            return;

        BuffData buffData = DataComponent.Instance.Get<BuffData>(buffId);
        if (buffData == null)
            return;

        _passiveBuffStacks.TryGetValue(buffId, out int currentStacks);
        int nextStacks = currentStacks + 1;

        if (!buffData.CanStack)
            nextStacks = currentStacks > 0 ? currentStacks : 1;
        else
            nextStacks = Mathf.Clamp(nextStacks, 1, Mathf.Max(1, buffData.MaxStacks));

        _passiveBuffStacks[buffId] = nextStacks;
    }

    private static bool AreEqual(DynamicBuffer<UnitPassiveBuffElement> current, List<UnitPassiveBuffElement> target)
    {
        if (current.Length != target.Count)
            return false;

        for (int i = 0; i < current.Length; i++)
        {
            UnitPassiveBuffElement a = current[i];
            UnitPassiveBuffElement b = target[i];
            if (a.BuffId != b.BuffId || a.StackCount != b.StackCount)
                return false;
        }

        return true;
    }
}
