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
            View.InventorySkillStoneDropped += OnInventorySkillStoneDropped;
            View.InventoryEquipDropped += OnInventoryEquipDropped;
            View.InventoryItemMoved += OnInventoryItemMoved;
            View.InventoryPropDropped += OnInventoryPropDropped;
            View.EquipReturnedToInventory += OnEquipReturnedToInventory;
            View.SpiritEquipSwapped += OnSpiritEquipSwapped;
            View.SkillAdditionRequested += OnSkillAdditionRequested;
            View.SkillReordered += OnSkillReordered;
            View.SkillReturnedToInventory += OnSkillReturnedToInventory;
            View.PropReturnedToInventory += OnPropReturnedToInventory;
            View.PropSlotMoved += OnPropSlotMoved;
            BindEvent(new CommonGameEvent(PlayerInputComponent.SkillChainChangedEventName), _refreshHandler);
            BindEvent(new CommonGameEvent(SaveDataComponent.CharacterDataChangedEventName), _refreshHandler);
            Model.Refresh();
        }

        protected override void OnClose()
        {
            View.InventorySkillStoneDropped -= OnInventorySkillStoneDropped;
            View.InventoryEquipDropped -= OnInventoryEquipDropped;
            View.InventoryItemMoved -= OnInventoryItemMoved;
            View.InventoryPropDropped -= OnInventoryPropDropped;
            View.EquipReturnedToInventory -= OnEquipReturnedToInventory;
            View.SpiritEquipSwapped -= OnSpiritEquipSwapped;
            View.SkillAdditionRequested -= OnSkillAdditionRequested;
            View.SkillReordered -= OnSkillReordered;
            View.SkillReturnedToInventory -= OnSkillReturnedToInventory;
            View.PropReturnedToInventory -= OnPropReturnedToInventory;
            View.PropSlotMoved -= OnPropSlotMoved;
            CloseEffectSelectUI();
        }

        private void OnInventorySkillStoneDropped(CharacterInventoryDisplayData data, int insertIndex)
        {
            if (data == null || data.ItemType != ItemType.SkillStone)
                return;

            if (!PlayerCharacterUtility.TryBeginEdit(out PlayerCharacterEdit edit))
                return;
            BackpackData backpackData = edit.Data.Backpack;
            SkillCData skillData = edit.Data.Skills;
            if (backpackData?.Items == null || skillData?.Chains == null)
                return;

            if (!TryConsumeBackpackItem(backpackData, data.SlotIndex, data.ItemId, 1))
                return;

            int skillChainIndex = UnityEngine.Mathf.Clamp(PlayerInputUtility.GetSkillChainIndex(), 0, skillData.Chains.Length - 1);
            SkillChainData chain = skillData.Chains[skillChainIndex] ??= new SkillChainData { Index = skillChainIndex };
            chain.EnsureSlots();
            int clampedInsertIndex = UnityEngine.Mathf.Clamp(insertIndex, 0, chain.Slots.Count);
            chain.Slots.Insert(clampedInsertIndex, new SkillChainSlotData
            {
                SkillStoneItemId = data.ItemId,
            });

            PlayerCharacterUtility.CommitEdit(edit);
        }

        private void OnInventoryEquipDropped(CharacterInventoryDisplayData data, int equipSlotIndex)
        {
            if (data == null || !IsEquippableItem(data.ItemType))
                return;

            if (!PlayerCharacterUtility.TryBeginEdit(out PlayerCharacterEdit edit))
                return;
            BackpackData backpackData = edit.Data.Backpack;
            EquipmentData equipmentData = edit.Data.Equipment;
            if (backpackData?.Items == null || equipmentData == null)
                return;

            if (equipSlotIndex < 0 || equipSlotIndex >= 5)
                return;

            ItemData itemData = DataComponent.Instance.Get<ItemData>(data.ItemId);
            if (!CanEquipToSlot(itemData, equipSlotIndex))
                return;

            int oldItemId = EquipmentUtility.GetEquippedItemId(equipmentData, equipSlotIndex);
            if (!TryConsumeBackpackItem(backpackData, data.SlotIndex, data.ItemId, 1))
                return;

            if (oldItemId >= 0)
            {
                if (InventoryUtility.AddItemToBackpack(backpackData, oldItemId, 1) != 1)
                {
                    PublishBackpackFull();
                    return;
                }
            }

            EquipmentUtility.SetEquippedItemId(equipmentData, equipSlotIndex, data.ItemId);
            PlayerCharacterUtility.CommitEdit(edit);
        }

        private void OnInventoryItemMoved(CharacterInventoryDisplayData data, int targetSlotIndex)
        {
            if (data == null || !PlayerCharacterUtility.TryBeginEdit(out PlayerCharacterEdit edit))
                return;

            if (InventoryUtility.TryMoveBackpackSlot(edit.Data.Backpack, data.SlotIndex, targetSlotIndex))
                PlayerCharacterUtility.CommitEdit(edit);
        }

        private void OnInventoryPropDropped(CharacterInventoryDisplayData data, int propSlotIndex)
        {
            if (data == null || data.ItemType != ItemType.Prop ||
                !PlayerCharacterUtility.TryBeginEdit(out PlayerCharacterEdit edit))
                return;

            if (PropInventoryUtility.TryMoveBackpackToPropSlot(
                    edit.Data.Backpack,
                    edit.Data.Props,
                    data.SlotIndex,
                    propSlotIndex))
            {
                PlayerCharacterUtility.CommitEdit(edit);
            }
        }

        private void OnEquipReturnedToInventory(int equipSlotIndex, int inventorySlotIndex)
        {
            if (!PlayerCharacterUtility.TryBeginEdit(out PlayerCharacterEdit edit))
                return;
            EquipmentData equipmentData = edit.Data.Equipment;
            BackpackData backpackData = edit.Data.Backpack;
            if (equipmentData == null || backpackData?.Items == null)
                return;

            int itemId = EquipmentUtility.GetEquippedItemId(equipmentData, equipSlotIndex);
            if (itemId < 0)
                return;

            ItemData itemData = DataComponent.Instance.Get<ItemData>(itemId);
            if (itemData == null || !IsEquippableItem(itemData.ItemType))
                return;

            if (!InventoryUtility.TryAddItemToBackpackSlot(
                    backpackData,
                    inventorySlotIndex,
                    itemId,
                    1,
                    itemData.ItemType))
            {
                PublishBackpackFull();
                return;
            }

            EquipmentUtility.SetEquippedItemId(equipmentData, equipSlotIndex, -1);
            PlayerCharacterUtility.CommitEdit(edit);
        }

        private void OnSpiritEquipSwapped(int sourceSlotIndex, int targetSlotIndex)
        {
            if (sourceSlotIndex < 1 || sourceSlotIndex > 4 || targetSlotIndex < 1 || targetSlotIndex > 4 || sourceSlotIndex == targetSlotIndex)
                return;

            if (!PlayerCharacterUtility.TryBeginEdit(out PlayerCharacterEdit edit))
                return;
            EquipmentData equipmentData = edit.Data.Equipment;
            if (equipmentData == null)
                return;

            int sourceSpiritIndex = sourceSlotIndex - 1;
            int targetSpiritIndex = targetSlotIndex - 1;
            if (EquipmentUtility.SwapSpiritSlots(equipmentData, sourceSpiritIndex, targetSpiritIndex))
                PlayerCharacterUtility.CommitEdit(edit);
        }

        private void OnSkillReordered(CharacterSkillDisplayData data, int insertIndex)
        {
            if (data == null)
                return;

            if (!PlayerCharacterUtility.TryBeginEdit(out PlayerCharacterEdit edit))
                return;
            SkillCData skillData = edit.Data.Skills;
            if (skillData?.Chains == null)
                return;

            int skillChainIndex = UnityEngine.Mathf.Clamp(PlayerInputUtility.GetSkillChainIndex(), 0, skillData.Chains.Length - 1);
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
            PlayerCharacterUtility.CommitEdit(edit);
        }

        private void OnSkillReturnedToInventory(CharacterSkillDisplayData data, int inventorySlotIndex)
        {
            if (data == null)
                return;

            if (!PlayerCharacterUtility.TryBeginEdit(out PlayerCharacterEdit edit))
                return;
            SkillCData skillData = edit.Data.Skills;
            BackpackData backpackData = edit.Data.Backpack;
            if (skillData?.Chains == null || backpackData?.Items == null)
                return;

            int skillChainIndex = UnityEngine.Mathf.Clamp(PlayerInputUtility.GetSkillChainIndex(), 0, skillData.Chains.Length - 1);
            SkillChainData chain = skillData.Chains[skillChainIndex];
            chain?.EnsureSlots();
            if (chain?.Slots == null || data.SkillIndex < 0 || data.SkillIndex >= chain.Slots.Count)
                return;

            int skillId = chain.Slots[data.SkillIndex].SkillStoneItemId;
            if (!InventoryUtility.TryAddItemToBackpackSlot(
                    backpackData,
                    inventorySlotIndex,
                    skillId,
                    1,
                    ItemType.SkillStone))
            {
                PublishBackpackFull();
                return;
            }

            chain.Slots.RemoveAt(data.SkillIndex);
            PlayerCharacterUtility.CommitEdit(edit);
        }

        private void OnPropReturnedToInventory(int propSlotIndex, int inventorySlotIndex)
        {
            if (!PlayerCharacterUtility.TryBeginEdit(out PlayerCharacterEdit edit))
                return;

            if (PropInventoryUtility.TryMovePropToBackpackSlot(
                    edit.Data.Backpack,
                    edit.Data.Props,
                    propSlotIndex,
                    inventorySlotIndex))
            {
                PlayerCharacterUtility.CommitEdit(edit);
                return;
            }

            PublishBackpackFull();
        }

        private void OnPropSlotMoved(int sourceSlotIndex, int targetSlotIndex)
        {
            if (!PlayerCharacterUtility.TryBeginEdit(out PlayerCharacterEdit edit) ||
                edit.Data.Props?.Slots == null ||
                sourceSlotIndex < 0 || sourceSlotIndex >= edit.Data.Props.Slots.Count ||
                targetSlotIndex < 0 || targetSlotIndex >= edit.Data.Props.Slots.Count ||
                sourceSlotIndex == targetSlotIndex)
            {
                return;
            }

            (edit.Data.Props.Slots[sourceSlotIndex], edit.Data.Props.Slots[targetSlotIndex]) =
                (edit.Data.Props.Slots[targetSlotIndex], edit.Data.Props.Slots[sourceSlotIndex]);
            PlayerCharacterUtility.CommitEdit(edit);
        }

        private void OnSkillAdditionRequested(CharacterSkillDisplayData data)
        {
            if (data == null || data.SkillIndex <= 0)
                return;

            if (!PlayerCharacterUtility.TryBeginEdit(out PlayerCharacterEdit edit))
                return;
            SkillCData skillData = edit.Data.Skills;
            if (skillData?.Chains == null)
                return;

            int skillChainIndex = UnityEngine.Mathf.Clamp(PlayerInputUtility.GetSkillChainIndex(), 0, skillData.Chains.Length - 1);
            SkillChainData chain = skillData.Chains[skillChainIndex];
            chain?.EnsureSlots();
            if (chain?.Slots == null || data.SkillIndex < 0 || data.SkillIndex >= chain.Slots.Count)
                return;

            SkillChainSlotData slot = chain.Slots[data.SkillIndex];

            CloseEffectSelectUI();
            _effectSelectUI = UIComponent.Instance.OpenChild<EffectSelectUI>(View, new EffectSelectUIOpenData
            {
                Edit = edit,
                SkillChainIndex = skillChainIndex,
                SkillSlotIndex = data.SkillIndex,
                SelectedAdditionId = slot?.SkillAdditionId ?? -1,
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

        private bool TryConsumeBackpackItem(BackpackData backpackData, int slotIndex, int itemId, int count)
        {
            return InventoryUtility.TryConsumeBackpackItem(backpackData, slotIndex, itemId, count);
        }

        private static void PublishBackpackFull()
        {
            EventComponent.Instance.Publish(new PickupFeedbackEvent(
                PickupFeedbackType.BackpackFull,
                -1,
                0));
        }

    }
}
