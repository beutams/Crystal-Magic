namespace CrystalMagic.UI
{
    public sealed class StashInteractUIController : UIControllerBase<StashInteractUI, StashInteractUIModel>
    {
        public StashInteractUIController(StashInteractUI view, StashInteractUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
            View.BindModel(Model);
            View.AddRequested += OnAddRequested;
            View.ReduceRequested += OnReduceRequested;
            View.QuantityInputChanged += OnQuantityInputChanged;
            View.ConfirmRequested += OnConfirmRequested;
            View.CancelRequested += OnCancelRequested;
            Model.RefreshRuntimeData();
        }

        protected override void OnClose()
        {
            View.AddRequested -= OnAddRequested;
            View.ReduceRequested -= OnReduceRequested;
            View.QuantityInputChanged -= OnQuantityInputChanged;
            View.ConfirmRequested -= OnConfirmRequested;
            View.CancelRequested -= OnCancelRequested;
        }

        private void OnAddRequested()
        {
            Model.AddQuantity(1);
        }

        private void OnReduceRequested()
        {
            Model.AddQuantity(-1);
        }

        private void OnQuantityInputChanged(string value)
        {
            if (!int.TryParse(value, out int quantity))
                quantity = 0;

            Model.SetQuantity(quantity);
        }

        private void OnConfirmRequested()
        {
            int quantity = Model.Quantity;
            if (quantity <= 0)
                return;

            if (Model.Mode == StashInteractMode.Store)
                StoreItems(quantity);
            else
                WithdrawItems(quantity);
        }

        private void OnCancelRequested()
        {
            View.Close();
        }

        private void StoreItems(int quantity)
        {
            CrystalMagic.Core.BackpackData backpackData = CrystalMagic.Core.SaveDataComponent.Instance.GetBackpackData();
            CrystalMagic.Core.StashData stashData = CrystalMagic.Core.SaveDataComponent.Instance.GetStashData();
            if (backpackData?.Items == null || stashData?.Items == null)
                return;

            int slotIndex = Model.SourceSlotIndex;
            if (slotIndex < 0 || slotIndex >= backpackData.Items.Count)
                return;

            CrystalMagic.Core.InventoryItemData inventoryItem = backpackData.Items[slotIndex];
            if (inventoryItem == null || inventoryItem.ItemId != Model.ItemId || inventoryItem.Quantity < quantity)
                return;

            AddItem(stashData.Items, Model.ItemId, quantity, inventoryItem.ItemType);
            inventoryItem.Quantity -= quantity;
            if (inventoryItem.Quantity <= 0)
                backpackData.Items.RemoveAt(slotIndex);

            CrystalMagic.Core.SaveDataComponent.Instance.NotifyStashDataChanged();
            CrystalMagic.Core.SaveDataComponent.Instance.NotifyBackpackDataChanged();
            View.Close();
        }

        private void WithdrawItems(int quantity)
        {
            CrystalMagic.Core.StashData stashData = CrystalMagic.Core.SaveDataComponent.Instance.GetStashData();
            CrystalMagic.Core.BackpackData backpackData = CrystalMagic.Core.SaveDataComponent.Instance.GetBackpackData();
            CrystalMagic.Core.CharacterPropData propData = CrystalMagic.Core.SaveDataComponent.Instance.GetCharacterPropData();
            if (stashData?.Items == null || backpackData?.Items == null)
                return;

            int slotIndex = Model.SourceSlotIndex;
            if (slotIndex < 0 || slotIndex >= stashData.Items.Count)
                return;

            CrystalMagic.Core.InventoryItemData inventoryItem = stashData.Items[slotIndex];
            if (inventoryItem == null || inventoryItem.ItemId != Model.ItemId || inventoryItem.Quantity < quantity)
                return;

            bool isPropItem = CrystalMagic.Core.PropInventoryUtility.IsPropItem(Model.ItemId);
            if (isPropItem)
            {
                if (!CrystalMagic.Core.PropInventoryUtility.CanAddProp(propData, Model.ItemId, quantity))
                    return;

                if (CrystalMagic.Core.PropInventoryUtility.AddProp(propData, Model.ItemId, quantity) != quantity)
                    return;
            }
            else
            {
                if (!CrystalMagic.Core.InventoryUtility.CanAddItemToBackpack(backpackData, Model.ItemId, quantity))
                    return;

                if (CrystalMagic.Core.InventoryUtility.AddItemToBackpack(backpackData, Model.ItemId, quantity) != quantity)
                    return;
            }

            inventoryItem.Quantity -= quantity;
            if (inventoryItem.Quantity <= 0)
                stashData.Items.RemoveAt(slotIndex);

            if (isPropItem)
                CrystalMagic.Core.SaveDataComponent.Instance.NotifyCharacterPropDataChanged();
            else
                CrystalMagic.Core.SaveDataComponent.Instance.NotifyBackpackDataChanged();

            CrystalMagic.Core.SaveDataComponent.Instance.NotifyStashDataChanged();
            View.Close();
        }

        private void AddItem(System.Collections.Generic.List<CrystalMagic.Core.InventoryItemData> items, int itemId, int quantity, CrystalMagic.Game.Data.ItemType fallbackItemType)
        {
            CrystalMagic.Core.InventoryUtility.AddItem(items, int.MaxValue, itemId, quantity, fallbackItemType);
        }
    }
}
