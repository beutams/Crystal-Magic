using CrystalMagic.Core;

namespace CrystalMagic.UI
{
    public sealed class ShopSellUIController : UIControllerBase<ShopSellUI, ShopSellUIModel>
    {
        public ShopSellUIController(ShopSellUI view, ShopSellUIModel model)
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

            TownData townData = SaveDataComponent.Instance.GetTownData();
            BackpackData backpackData = SaveDataComponent.Instance.GetBackpackData();
            if (townData == null || backpackData?.Items == null)
                return;

            int slotIndex = Model.SlotIndex;
            if (slotIndex < 0 || slotIndex >= backpackData.Items.Count)
                return;

            InventoryItemData inventoryItem = backpackData.Items[slotIndex];
            if (inventoryItem == null || inventoryItem.ItemId != Model.ItemId || inventoryItem.Quantity < quantity)
                return;

            long totalSellPrice = (long)Model.Price * quantity;
            if (totalSellPrice > 0)
                townData.StashMoney += totalSellPrice;

            inventoryItem.Quantity -= quantity;
            if (inventoryItem.Quantity <= 0)
                backpackData.Items.RemoveAt(slotIndex);

            SaveDataComponent.Instance.NotifyBackpackDataChanged();
            View.Close();
        }

        private void OnCancelRequested()
        {
            View.Close();
        }
    }
}
