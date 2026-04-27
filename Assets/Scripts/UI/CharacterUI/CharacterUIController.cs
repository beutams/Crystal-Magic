using CrystalMagic.Core;
using CrystalMagic.Game.Data;

namespace CrystalMagic.UI
{
    public sealed class CharacterUIController : UIControllerBase<CharacterUI, CharacterUIModel>
    {
        private readonly System.Action<CrystalMagic.Core.CommonGameEvent> _refreshHandler;

        public CharacterUIController(CharacterUI view, CharacterUIModel model)
            : base(view, model)
        {
            _refreshHandler = _ => Model.Refresh();
        }

        protected override void OnOpen()
        {
            View.BindModel(Model);
            View.ChangeSkillRequested += OnChangeSkillRequested;
            View.InventorySkillStoneDropped += OnInventorySkillStoneDropped;
            View.InventoryEquipDropped += OnInventoryEquipDropped;
            View.EquipReturnedToInventory += OnEquipReturnedToInventory;
            View.BonusEquipSwapped += OnBonusEquipSwapped;
            View.SkillReordered += OnSkillReordered;
            View.SkillReturnedToInventory += OnSkillReturnedToInventory;
            EventComponent.Instance.Subscribe(new CommonGameEvent(RuntimeDataComponent.SkillRuntimeDataChangedEventName), _refreshHandler);
            EventComponent.Instance.Subscribe(new CommonGameEvent(SaveDataComponent.SkillDataChangedEventName), _refreshHandler);
            EventComponent.Instance.Subscribe(new CommonGameEvent(SaveDataComponent.BackpackDataChangedEventName), _refreshHandler);
            EventComponent.Instance.Subscribe(new CommonGameEvent(SaveDataComponent.EquipmentDataChangedEventName), _refreshHandler);
            Model.Refresh();
        }

        protected override void OnClose()
        {
            View.ChangeSkillRequested -= OnChangeSkillRequested;
            View.InventorySkillStoneDropped -= OnInventorySkillStoneDropped;
            View.InventoryEquipDropped -= OnInventoryEquipDropped;
            View.EquipReturnedToInventory -= OnEquipReturnedToInventory;
            View.BonusEquipSwapped -= OnBonusEquipSwapped;
            View.SkillReordered -= OnSkillReordered;
            View.SkillReturnedToInventory -= OnSkillReturnedToInventory;
            EventComponent.Instance.Unsubscribe(new CommonGameEvent(RuntimeDataComponent.SkillRuntimeDataChangedEventName), _refreshHandler);
            EventComponent.Instance.Unsubscribe(new CommonGameEvent(SaveDataComponent.SkillDataChangedEventName), _refreshHandler);
            EventComponent.Instance.Unsubscribe(new CommonGameEvent(SaveDataComponent.BackpackDataChangedEventName), _refreshHandler);
            EventComponent.Instance.Unsubscribe(new CommonGameEvent(SaveDataComponent.EquipmentDataChangedEventName), _refreshHandler);
        }

        private void OnChangeSkillRequested()
        {
            RuntimeDataComponent.Instance.SelectNextSkillChain(SaveDataComponent.Instance.GetSkillData());
        }

        private void OnInventorySkillStoneDropped(CharacterInventoryDisplayData data, int insertIndex)
        {
            if (data == null || data.ItemType != ItemType.SkillStone)
                return;

            BackpackData backpackData = SaveDataComponent.Instance.GetBackpackData();
            SkillCData skillData = SaveDataComponent.Instance.GetSkillData();
            RuntimeSkillData runtimeSkillData = RuntimeDataComponent.Instance.GetSkillData();
            if (backpackData?.Items == null || skillData?.Chains == null || runtimeSkillData == null)
                return;

            if (!TryConsumeBackpackItem(backpackData, data.SlotIndex, data.ItemId, 1))
                return;

            int skillChainIndex = UnityEngine.Mathf.Clamp(runtimeSkillData.CurrentSkillChainIndex, 0, skillData.Chains.Length - 1);
            SkillChainData chain = skillData.Chains[skillChainIndex] ??= new SkillChainData { Index = skillChainIndex };
            chain.EnsureSlots();
            int clampedInsertIndex = UnityEngine.Mathf.Clamp(insertIndex, 0, chain.Slots.Count);
            chain.Slots.Insert(clampedInsertIndex, new SkillChainSlotData
            {
                SkillStoneItemId = data.ItemId,
            });

            SaveDataComponent.Instance.NotifyBackpackDataChanged();
            SaveDataComponent.Instance.NotifySkillDataChanged();
        }

        private void OnInventoryEquipDropped(CharacterInventoryDisplayData data, int equipSlotIndex)
        {
            if (data == null || !IsEquippableItem(data.ItemType))
                return;

            BackpackData backpackData = SaveDataComponent.Instance.GetBackpackData();
            EquipmentData equipmentData = SaveDataComponent.Instance.GetEquipmentData();
            if (backpackData?.Items == null || equipmentData == null)
                return;

            if (equipSlotIndex < 0 || equipSlotIndex >= 5)
                return;

            ItemData itemData = DataComponent.Instance.Get<ItemData>(data.ItemId);
            if (!CanEquipToSlot(itemData, equipSlotIndex))
                return;

            int oldItemId = GetEquippedItemId(equipmentData, equipSlotIndex);
            if (!TryConsumeBackpackItem(backpackData, data.SlotIndex, data.ItemId, 1))
                return;

            if (oldItemId > 0)
                AddItemToBackpack(backpackData, oldItemId, 1);

            SetEquippedItemId(equipmentData, equipSlotIndex, data.ItemId);
            SaveDataComponent.Instance.NotifyBackpackDataChanged();
            SaveDataComponent.Instance.NotifyEquipmentDataChanged();
        }

        private void OnEquipReturnedToInventory(int equipSlotIndex)
        {
            EquipmentData equipmentData = SaveDataComponent.Instance.GetEquipmentData();
            BackpackData backpackData = SaveDataComponent.Instance.GetBackpackData();
            if (equipmentData == null || backpackData?.Items == null)
                return;

            int itemId = GetEquippedItemId(equipmentData, equipSlotIndex);
            if (itemId <= 0)
                return;

            ItemData itemData = DataComponent.Instance.Get<ItemData>(itemId);
            if (itemData == null || !IsEquippableItem(itemData.ItemType))
                return;

            SetEquippedItemId(equipmentData, equipSlotIndex, equipSlotIndex == 0 ? 0 : -1);
            AddItemToBackpack(backpackData, itemId, 1);
            SaveDataComponent.Instance.NotifyBackpackDataChanged();
            SaveDataComponent.Instance.NotifyEquipmentDataChanged();
        }

        private void OnBonusEquipSwapped(int sourceSlotIndex, int targetSlotIndex)
        {
            if (sourceSlotIndex < 1 || sourceSlotIndex > 4 || targetSlotIndex < 1 || targetSlotIndex > 4 || sourceSlotIndex == targetSlotIndex)
                return;

            EquipmentData equipmentData = SaveDataComponent.Instance.GetEquipmentData();
            if (equipmentData?.BonusSlots == null || equipmentData.BonusSlots.Length < 4)
                return;

            int sourceBonusIndex = sourceSlotIndex - 1;
            int targetBonusIndex = targetSlotIndex - 1;
            int temp = equipmentData.BonusSlots[sourceBonusIndex];
            equipmentData.BonusSlots[sourceBonusIndex] = equipmentData.BonusSlots[targetBonusIndex];
            equipmentData.BonusSlots[targetBonusIndex] = temp;
            SaveDataComponent.Instance.NotifyEquipmentDataChanged();
        }

        private void OnSkillReordered(CharacterSkillDisplayData data, int insertIndex)
        {
            if (data == null)
                return;

            SkillCData skillData = SaveDataComponent.Instance.GetSkillData();
            RuntimeSkillData runtimeSkillData = RuntimeDataComponent.Instance.GetSkillData();
            if (skillData?.Chains == null || runtimeSkillData == null)
                return;

            int skillChainIndex = UnityEngine.Mathf.Clamp(runtimeSkillData.CurrentSkillChainIndex, 0, skillData.Chains.Length - 1);
            SkillChainData chain = skillData.Chains[skillChainIndex];
            chain?.EnsureSlots();
            if (chain?.Slots == null || data.SkillIndex < 0 || data.SkillIndex >= chain.Slots.Count)
                return;

            int sourceIndex = data.SkillIndex;
            int targetIndex = UnityEngine.Mathf.Clamp(insertIndex, 0, chain.Slots.Count);
            if (sourceIndex < targetIndex)
                targetIndex--;

            if (targetIndex == sourceIndex)
                return;

            SkillChainSlotData slotData = chain.Slots[sourceIndex];
            chain.Slots.RemoveAt(sourceIndex);
            chain.Slots.Insert(targetIndex, slotData);
            SaveDataComponent.Instance.NotifySkillDataChanged();
        }

        private void OnSkillReturnedToInventory(CharacterSkillDisplayData data)
        {
            if (data == null)
                return;

            SkillCData skillData = SaveDataComponent.Instance.GetSkillData();
            RuntimeSkillData runtimeSkillData = RuntimeDataComponent.Instance.GetSkillData();
            BackpackData backpackData = SaveDataComponent.Instance.GetBackpackData();
            if (skillData?.Chains == null || runtimeSkillData == null || backpackData?.Items == null)
                return;

            int skillChainIndex = UnityEngine.Mathf.Clamp(runtimeSkillData.CurrentSkillChainIndex, 0, skillData.Chains.Length - 1);
            SkillChainData chain = skillData.Chains[skillChainIndex];
            chain?.EnsureSlots();
            if (chain?.Slots == null || data.SkillIndex < 0 || data.SkillIndex >= chain.Slots.Count)
                return;

            int skillId = chain.Slots[data.SkillIndex].SkillStoneItemId;
            chain.Slots.RemoveAt(data.SkillIndex);
            AddItemToBackpack(backpackData, skillId, 1);
            SaveDataComponent.Instance.NotifyBackpackDataChanged();
            SaveDataComponent.Instance.NotifySkillDataChanged();
        }

        private bool IsEquippableItem(ItemType itemType)
        {
            return itemType == ItemType.Weapon || itemType == ItemType.Accessory;
        }

        private bool CanEquipToSlot(ItemData itemData, int equipSlotIndex)
        {
            if (itemData == null || !IsEquippableItem(itemData.ItemType))
                return false;

            if (equipSlotIndex == 0)
                return itemData.ItemType == ItemType.Weapon;

            if (equipSlotIndex >= 1 && equipSlotIndex <= 4)
                return itemData.ItemType == ItemType.Accessory;

            return false;
        }

        private int GetEquippedItemId(EquipmentData equipmentData, int equipSlotIndex)
        {
            if (equipmentData == null)
                return 0;

            if (equipSlotIndex == 0)
                return equipmentData.StaffId;

            int bonusIndex = equipSlotIndex - 1;
            if (equipmentData.BonusSlots == null || bonusIndex < 0 || bonusIndex >= equipmentData.BonusSlots.Length)
                return 0;

            return equipmentData.BonusSlots[bonusIndex];
        }

        private void SetEquippedItemId(EquipmentData equipmentData, int equipSlotIndex, int itemId)
        {
            if (equipmentData == null)
                return;

            if (equipSlotIndex == 0)
            {
                equipmentData.StaffId = itemId;
                return;
            }

            int bonusIndex = equipSlotIndex - 1;
            if (equipmentData.BonusSlots == null || bonusIndex < 0 || bonusIndex >= equipmentData.BonusSlots.Length)
                return;

            equipmentData.BonusSlots[bonusIndex] = itemId;
        }

        private bool TryConsumeBackpackItem(BackpackData backpackData, int slotIndex, int itemId, int count)
        {
            if (backpackData?.Items == null || count <= 0 || slotIndex < 0 || slotIndex >= backpackData.Items.Count)
                return false;

            InventoryItemData inventoryItem = backpackData.Items[slotIndex];
            if (inventoryItem == null || inventoryItem.ItemId != itemId || inventoryItem.Quantity < count)
                return false;

            inventoryItem.Quantity -= count;
            if (inventoryItem.Quantity <= 0)
                backpackData.Items.RemoveAt(slotIndex);

            return true;
        }

        private void AddItemToBackpack(BackpackData backpackData, int itemId, int quantity)
        {
            if (backpackData?.Items == null || itemId <= 0 || quantity <= 0)
                return;

            ItemData itemData = DataComponent.Instance.Get<ItemData>(itemId);
            int maxStack = itemData != null && itemData.MaxStack > 0 ? itemData.MaxStack : 1;
            int remaining = quantity;

            for (int i = 0; i < backpackData.Items.Count && remaining > 0; i++)
            {
                InventoryItemData inventoryItem = backpackData.Items[i];
                if (inventoryItem == null || inventoryItem.ItemId != itemId || inventoryItem.Quantity >= maxStack)
                    continue;

                int addCount = UnityEngine.Mathf.Min(maxStack - inventoryItem.Quantity, remaining);
                inventoryItem.Quantity += addCount;
                remaining -= addCount;
            }

            while (remaining > 0)
            {
                int addCount = UnityEngine.Mathf.Min(maxStack, remaining);
                backpackData.Items.Add(new InventoryItemData
                {
                    ItemId = itemId,
                    Quantity = addCount,
                    ItemType = itemData != null ? itemData.ItemType : ItemType.None,
                });
                remaining -= addCount;
            }
        }

    }
}
