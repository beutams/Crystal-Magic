using CrystalMagic.Game.Skill;

namespace CrystalMagic.UI
{
    public sealed class CharacterUIModel : UIModelBase
    {
        public const string DataChangedEventName = "CharacterUIModel.DataChanged";
        public override string ChangedEventName => DataChangedEventName;

        private readonly System.Collections.Generic.List<CharacterSkillDisplayData> _skillItems = new();
        private readonly CharacterInventoryDisplayData[] _inventoryItems = new CharacterInventoryDisplayData[32];
        private readonly CharacterEquipDisplayData[] _equipItems = new CharacterEquipDisplayData[5];

        public System.Collections.Generic.IReadOnlyList<CharacterSkillDisplayData> SkillItems => _skillItems;
        public CharacterInventoryDisplayData[] InventoryItems => _inventoryItems;
        public CharacterEquipDisplayData[] EquipItems => _equipItems;
        public int InventorySlotCount => 32;

        public void Refresh()
        {
            RefreshSkill();
            RefreshInventory();
            RefreshEquip();
            CrystalMagic.Core.EventComponent.Instance.Publish(new CrystalMagic.Core.CommonGameEvent(DataChangedEventName, this));
        }

        private void RefreshSkill()
        {
            _skillItems.Clear();

            CrystalMagic.Core.SkillCData skillConfig = CrystalMagic.Core.SaveDataComponent.Instance.GetSkillData();
            CrystalMagic.Core.RuntimeSkillData runtimeSkillData = CrystalMagic.Core.RuntimeDataComponent.Instance.GetSkillData();
            if (skillConfig?.Chains == null || skillConfig.Chains.Length == 0)
                return;

            int selectedIndex = UnityEngine.Mathf.Clamp(runtimeSkillData.CurrentSkillChainIndex, 0, skillConfig.Chains.Length - 1);
            CrystalMagic.Core.SkillChainData chain = skillConfig.Chains[selectedIndex];
            chain?.EnsureSlots();
            if (chain?.Slots == null)
                return;

            for (int i = 0; i < chain.Slots.Count; i++)
            {
                CrystalMagic.Core.SkillChainSlotData slot = chain.Slots[i];
                int skillStoneItemId = slot?.SkillStoneItemId ?? 0;
                CrystalMagic.Game.Data.SkillData skillData = SkillChainResolver.GetSkillDataBySkillStoneItemId(skillStoneItemId);
                CrystalMagic.Game.Data.SkillEffectData skillEffectData = slot != null && slot.SkillEffectId > 0
                    ? CrystalMagic.Core.DataComponent.Instance.Get<CrystalMagic.Game.Data.SkillEffectData>(slot.SkillEffectId)
                    : null;
                string skillIconPath = skillData != null ? skillData.IconPath : string.Empty;
                string effectIconPath = skillEffectData != null ? skillEffectData.IconPath : string.Empty;

                _skillItems.Add(new CharacterSkillDisplayData
                {
                    DisplayIndex = i + 1,
                    SkillIndex = i,
                    SkillId = skillData != null ? skillData.Id : 0,
                    SkillIconPath = skillIconPath,
                    EffectIconPath = effectIconPath,
                });
            }
        }

        private void RefreshInventory()
        {
            System.Array.Clear(_inventoryItems, 0, _inventoryItems.Length);

            System.Collections.Generic.List<CrystalMagic.Core.InventoryItemData> backpackItems = CrystalMagic.Core.SaveDataComponent.Instance.GetBackpackData()?.Items;
            if (backpackItems == null)
                return;

            int count = backpackItems.Count < _inventoryItems.Length ? backpackItems.Count : _inventoryItems.Length;
            for (int i = 0; i < count; i++)
            {
                CrystalMagic.Core.InventoryItemData inventoryItem = backpackItems[i];
                if (inventoryItem == null)
                    continue;

                CrystalMagic.Game.Data.ItemData itemData = CrystalMagic.Core.DataComponent.Instance.Get<CrystalMagic.Game.Data.ItemData>(inventoryItem.ItemId);
                _inventoryItems[i] = new CharacterInventoryDisplayData
                {
                    SlotIndex = i,
                    ItemId = inventoryItem.ItemId,
                    Count = inventoryItem.Quantity,
                    ItemType = itemData != null ? itemData.ItemType : inventoryItem.ItemType,
                    Name = itemData != null ? itemData.Name : string.Empty,
                    IconPath = itemData != null ? itemData.IconPath : string.Empty,
                };
            }
        }

        private void RefreshEquip()
        {
            for (int i = 0; i < _equipItems.Length; i++)
            {
                _equipItems[i] = null;
            }

            CrystalMagic.Core.EquipmentData equipment = CrystalMagic.Core.SaveDataComponent.Instance.GetEquipmentData();
            if (equipment == null)
                return;

            if (equipment.StaffId > 0)
            {
                CrystalMagic.Game.Data.ItemData weaponData = CrystalMagic.Core.DataComponent.Instance.Get<CrystalMagic.Game.Data.ItemData>(equipment.StaffId);
                _equipItems[0] = new CharacterEquipDisplayData
                {
                    SlotIndex = 0,
                    ItemId = equipment.StaffId,
                    ItemType = weaponData != null ? weaponData.ItemType : CrystalMagic.Game.Data.ItemType.None,
                    Name = weaponData != null ? weaponData.Name : string.Empty,
                    IconPath = weaponData != null ? weaponData.IconPath : string.Empty,
                };
            }

            if (equipment.BonusSlots == null)
                return;

            for (int i = 0; i < 4 && i < equipment.BonusSlots.Length; i++)
            {
                int bonusId = equipment.BonusSlots[i];
                if (bonusId < 0)
                    continue;

                CrystalMagic.Game.Data.ItemData itemData = CrystalMagic.Core.DataComponent.Instance.Get<CrystalMagic.Game.Data.ItemData>(bonusId);
                CrystalMagic.Game.Data.PropertyBuffData propertyBuffData = CrystalMagic.Core.DataComponent.Instance.Get<CrystalMagic.Game.Data.PropertyBuffData>(bonusId);
                CrystalMagic.Game.Data.EffectBuffData effectBuffData = CrystalMagic.Core.DataComponent.Instance.Get<CrystalMagic.Game.Data.EffectBuffData>(bonusId);
                string bonusName = itemData != null
                    ? itemData.Name
                    : propertyBuffData != null
                        ? propertyBuffData.Name
                        : effectBuffData != null
                            ? effectBuffData.Name
                            : string.Empty;

                _equipItems[i + 1] = new CharacterEquipDisplayData
                {
                    SlotIndex = i + 1,
                    ItemId = bonusId,
                    ItemType = itemData != null ? itemData.ItemType : CrystalMagic.Game.Data.ItemType.None,
                    Name = bonusName,
                    IconPath = itemData != null ? itemData.IconPath : string.Empty,
                };
            }
        }
    }

    public sealed class CharacterSkillDisplayData
    {
        public int DisplayIndex;
        public int SkillIndex;
        public int SkillId;
        public string SkillIconPath;
        public string EffectIconPath;
    }

    public sealed class CharacterInventoryDisplayData
    {
        public int SlotIndex;
        public int ItemId;
        public int Count;
        public CrystalMagic.Game.Data.ItemType ItemType;
        public string Name;
        public string IconPath;
    }

    public sealed class CharacterEquipDisplayData
    {
        public int SlotIndex;
        public int ItemId;
        public CrystalMagic.Game.Data.ItemType ItemType;
        public string Name;
        public string IconPath;
    }
}
