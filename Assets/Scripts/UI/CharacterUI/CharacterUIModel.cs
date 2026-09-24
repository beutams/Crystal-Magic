using CrystalMagic.Game.Skill;

namespace CrystalMagic.UI
{
    public sealed class CharacterUIModel : UIModelBase
    {
        public const string DataChangedEventName = "CharacterUIModel.DataChanged";
        public override string ChangedEventName => DataChangedEventName;

        private readonly System.Collections.Generic.List<CharacterSkillDisplayData> _skillItems = new();
        private readonly System.Collections.Generic.List<CharacterInventoryDisplayData> _inventoryItems = new();
        private readonly CharacterEquipDisplayData[] _equipItems = new CharacterEquipDisplayData[5];
        private readonly CharacterPropDisplayData[] _propItems = new CharacterPropDisplayData[3];

        public System.Collections.Generic.IReadOnlyList<CharacterSkillDisplayData> SkillItems => _skillItems;
        public System.Collections.Generic.IReadOnlyList<CharacterInventoryDisplayData> InventoryItems => _inventoryItems;
        public CharacterEquipDisplayData[] EquipItems => _equipItems;
        public CharacterPropDisplayData[] PropItems => _propItems;
        public int InventorySlotCount => _inventoryItems.Count;

        public void Refresh()
        {
            RefreshSkill();
            RefreshInventory();
            RefreshEquip();
            RefreshProps();
            CrystalMagic.Core.EventComponent.Instance.Publish(new CrystalMagic.Core.CommonGameEvent(DataChangedEventName, this));
        }

        private void RefreshSkill()
        {
            _skillItems.Clear();

            CrystalMagic.Core.SkillCData skillConfig = CrystalMagic.Core.SaveDataComponent.Instance.GetSkillData();
            if (skillConfig?.Chains == null || skillConfig.Chains.Length == 0)
                return;

            int selectedIndex = UnityEngine.Mathf.Clamp(PlayerInputUtility.GetSkillChainIndex(), 0, skillConfig.Chains.Length - 1);
            CrystalMagic.Core.SkillChainData chain = skillConfig.Chains[selectedIndex];
            chain?.EnsureSlots();
            if (chain?.Slots == null)
                return;

            for (int i = 0; i < chain.Slots.Count; i++)
            {
                CrystalMagic.Core.SkillChainSlotData slot = chain.Slots[i];
                int skillStoneItemId = slot?.SkillStoneItemId ?? -1;
                CrystalMagic.Game.Data.SkillData skillData = SkillChainResolver.GetSkillDataBySkillStoneItemId(skillStoneItemId);
                CrystalMagic.Game.Data.SkillAdditionData skillAdditionData = slot != null && slot.SkillAdditionId >= 0
                    ? CrystalMagic.Core.DataComponent.Instance.Get<CrystalMagic.Game.Data.SkillAdditionData>(slot.SkillAdditionId)
                    : null;
                string skillIconPath = skillData != null ? skillData.IconPath : string.Empty;
                string additionIconPath = skillAdditionData != null ? skillAdditionData.IconPath : string.Empty;

                _skillItems.Add(new CharacterSkillDisplayData
                {
                    DisplayIndex = i + 1,
                    SkillIndex = i,
                    SkillId = skillData != null ? skillData.Id : -1,
                    SkillIconPath = skillIconPath,
                    AdditionIconPath = additionIconPath,
                    CanSelectAddition = i > 0,
                });
            }
        }

        private void RefreshInventory()
        {
            _inventoryItems.Clear();
            CrystalMagic.Core.BackpackData backpack = CrystalMagic.Core.SaveDataComponent.Instance.GetBackpackData();
            if (backpack == null)
                return;

            CrystalMagic.Core.InventoryUtility.EnsureBackpackSlots(backpack);
            for (int i = 0; i < backpack.Items.Count; i++)
            {
                CrystalMagic.Core.InventoryItemData inventoryItem = backpack.Items[i];
                if (inventoryItem == null || inventoryItem.IsEmpty)
                {
                    _inventoryItems.Add(null);
                    continue;
                }

                CrystalMagic.Game.Data.ItemData itemData = CrystalMagic.Core.DataComponent.Instance.Get<CrystalMagic.Game.Data.ItemData>(inventoryItem.ItemId);
                _inventoryItems.Add(new CharacterInventoryDisplayData
                {
                    SlotIndex = i,
                    ItemId = inventoryItem.ItemId,
                    Count = inventoryItem.Quantity,
                    ItemType = itemData != null ? itemData.ItemType : inventoryItem.ItemType,
                    Name = itemData != null ? itemData.Name : string.Empty,
                    IconPath = itemData != null ? itemData.IconPath : string.Empty,
                });
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

            if (equipment.MagicStoneId >= 0)
            {
                CrystalMagic.Game.Data.ItemData magicStoneData = CrystalMagic.Core.DataComponent.Instance.Get<CrystalMagic.Game.Data.ItemData>(equipment.MagicStoneId);
                _equipItems[0] = new CharacterEquipDisplayData
                {
                    SlotIndex = 0,
                    ItemId = equipment.MagicStoneId,
                    ItemType = magicStoneData != null ? magicStoneData.ItemType : CrystalMagic.Game.Data.ItemType.None,
                    Name = magicStoneData != null ? magicStoneData.Name : string.Empty,
                    IconPath = magicStoneData != null ? magicStoneData.IconPath : string.Empty,
                };
            }

            if (equipment.SpiritSlots == null)
                return;

            for (int i = 0; i < 4 && i < equipment.SpiritSlots.Length; i++)
            {
                int spiritItemId = equipment.SpiritSlots[i];
                if (spiritItemId < 0)
                    continue;

                CrystalMagic.Game.Data.ItemData itemData = CrystalMagic.Core.DataComponent.Instance.Get<CrystalMagic.Game.Data.ItemData>(spiritItemId);
                CrystalMagic.Game.Data.BuffData buffData = CrystalMagic.Core.DataComponent.Instance.Get<CrystalMagic.Game.Data.BuffData>(spiritItemId);
                string spiritName = itemData != null
                    ? itemData.Name
                    : buffData != null
                        ? buffData.Name
                            : string.Empty;

                _equipItems[i + 1] = new CharacterEquipDisplayData
                {
                    SlotIndex = i + 1,
                    ItemId = spiritItemId,
                    ItemType = itemData != null ? itemData.ItemType : CrystalMagic.Game.Data.ItemType.None,
                    Name = spiritName,
                    IconPath = itemData != null ? itemData.IconPath : string.Empty,
                };
            }
        }

        private void RefreshProps()
        {
            System.Array.Clear(_propItems, 0, _propItems.Length);
            CrystalMagic.Core.CharacterPropData propData = CrystalMagic.Core.SaveDataComponent.Instance.GetCharacterPropData();
            if (propData?.Slots == null)
                return;

            int count = UnityEngine.Mathf.Min(_propItems.Length, propData.Slots.Count);
            for (int i = 0; i < count; i++)
            {
                CrystalMagic.Core.CharacterPropSlotData slot = propData.Slots[i];
                if (slot == null || slot.IsEmpty)
                    continue;

                CrystalMagic.Game.Data.ItemData itemData = CrystalMagic.Core.DataComponent.Instance.Get<CrystalMagic.Game.Data.ItemData>(slot.ItemId);
                _propItems[i] = new CharacterPropDisplayData
                {
                    SlotIndex = i,
                    ItemId = slot.ItemId,
                    Count = slot.Quantity,
                    Name = itemData != null ? itemData.Name : string.Empty,
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
        public string AdditionIconPath;
        public bool CanSelectAddition;
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

    public sealed class CharacterPropDisplayData
    {
        public int SlotIndex;
        public int ItemId;
        public int Count;
        public string Name;
        public string IconPath;
    }
}
