using CrystalMagic.Core;
using CrystalMagic.Game.Data;

namespace CrystalMagic.UI
{
    public sealed class CharacterUIController : UIControllerBase<CharacterUI, CharacterUIModel>
    {
        private EffectSelectUI _effectSelectUI;
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
            View.SpiritEquipSwapped += OnSpiritEquipSwapped;
            View.SkillAdditionRequested += OnSkillAdditionRequested;
            View.SkillReordered += OnSkillReordered;
            View.SkillReturnedToInventory += OnSkillReturnedToInventory;
            BindEvent(new CommonGameEvent(RuntimeDataComponent.SkillRuntimeDataChangedEventName), _refreshHandler);
            BindEvent(new CommonGameEvent(SaveDataComponent.SkillDataChangedEventName), _refreshHandler);
            BindEvent(new CommonGameEvent(SaveDataComponent.BackpackDataChangedEventName), _refreshHandler);
            BindEvent(new CommonGameEvent(SaveDataComponent.EquipmentDataChangedEventName), _refreshHandler);
            Model.Refresh();
        }

        protected override void OnClose()
        {
            View.ChangeSkillRequested -= OnChangeSkillRequested;
            View.InventorySkillStoneDropped -= OnInventorySkillStoneDropped;
            View.InventoryEquipDropped -= OnInventoryEquipDropped;
            View.EquipReturnedToInventory -= OnEquipReturnedToInventory;
            View.SpiritEquipSwapped -= OnSpiritEquipSwapped;
            View.SkillAdditionRequested -= OnSkillAdditionRequested;
            View.SkillReordered -= OnSkillReordered;
            View.SkillReturnedToInventory -= OnSkillReturnedToInventory;
            CloseEffectSelectUI();
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

            if (oldItemId >= 0)
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
            if (itemId < 0)
                return;

            ItemData itemData = DataComponent.Instance.Get<ItemData>(itemId);
            if (itemData == null || !IsEquippableItem(itemData.ItemType))
                return;

            SetEquippedItemId(equipmentData, equipSlotIndex, -1);
            AddItemToBackpack(backpackData, itemId, 1);
            SaveDataComponent.Instance.NotifyBackpackDataChanged();
            SaveDataComponent.Instance.NotifyEquipmentDataChanged();
        }

        private void OnSpiritEquipSwapped(int sourceSlotIndex, int targetSlotIndex)
        {
            if (sourceSlotIndex < 1 || sourceSlotIndex > 4 || targetSlotIndex < 1 || targetSlotIndex > 4 || sourceSlotIndex == targetSlotIndex)
                return;

            EquipmentData equipmentData = SaveDataComponent.Instance.GetEquipmentData();
            if (equipmentData?.SpiritSlots == null || equipmentData.SpiritSlots.Length < 4)
                return;

            int sourceSpiritIndex = sourceSlotIndex - 1;
            int targetSpiritIndex = targetSlotIndex - 1;
            int temp = equipmentData.SpiritSlots[sourceSpiritIndex];
            equipmentData.SpiritSlots[sourceSpiritIndex] = equipmentData.SpiritSlots[targetSpiritIndex];
            equipmentData.SpiritSlots[targetSpiritIndex] = temp;
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

        private void OnSkillAdditionRequested(CharacterSkillDisplayData data)
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

            CloseEffectSelectUI();
            _effectSelectUI = UIComponent.Instance.OpenChild<EffectSelectUI>(View, new EffectSelectUIOpenData
            {
                SkillSlotIndex = data.SkillIndex,
                SelectedAdditionId = chain.Slots[data.SkillIndex]?.SkillAdditionId ?? -1,
            });
        }

        private void CloseEffectSelectUI()
        {
            if (_effectSelectUI == null)
                return;

            _effectSelectUI.Close();
            _effectSelectUI = null;
        }

        private bool IsEquippableItem(ItemType itemType)
        {
            return itemType == ItemType.MagicStone || itemType == ItemType.Spirit;
        }

        private bool CanEquipToSlot(ItemData itemData, int equipSlotIndex)
        {
            if (itemData == null || !IsEquippableItem(itemData.ItemType))
                return false;

            if (equipSlotIndex == 0)
                return itemData.ItemType == ItemType.MagicStone;

            if (equipSlotIndex >= 1 && equipSlotIndex <= 4)
                return itemData.ItemType == ItemType.Spirit;

            return false;
        }

        private int GetEquippedItemId(EquipmentData equipmentData, int equipSlotIndex)
        {
            if (equipmentData == null)
                return -1;

            if (equipSlotIndex == 0)
                return equipmentData.MagicStoneId;

            int spiritIndex = equipSlotIndex - 1;
            if (equipmentData.SpiritSlots == null || spiritIndex < 0 || spiritIndex >= equipmentData.SpiritSlots.Length)
                return -1;

            return equipmentData.SpiritSlots[spiritIndex];
        }

        private void SetEquippedItemId(EquipmentData equipmentData, int equipSlotIndex, int itemId)
        {
            if (equipmentData == null)
                return;

            if (equipSlotIndex == 0)
            {
                equipmentData.MagicStoneId = itemId;
                return;
            }

            int spiritIndex = equipSlotIndex - 1;
            if (equipmentData.SpiritSlots == null || spiritIndex < 0 || spiritIndex >= equipmentData.SpiritSlots.Length)
                return;

            equipmentData.SpiritSlots[spiritIndex] = itemId;
        }

        private bool TryConsumeBackpackItem(BackpackData backpackData, int slotIndex, int itemId, int count)
        {
            return InventoryUtility.TryConsumeBackpackItem(backpackData, slotIndex, itemId, count);
        }

        private void AddItemToBackpack(BackpackData backpackData, int itemId, int quantity)
        {
            InventoryUtility.AddItemToBackpack(backpackData, itemId, quantity);
        }

    }
}
