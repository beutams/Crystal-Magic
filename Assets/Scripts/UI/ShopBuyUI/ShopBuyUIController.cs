using CrystalMagic.Core;
using CrystalMagic.Game.Data;

namespace CrystalMagic.UI
{
    public sealed class ShopBuyUIController : UIControllerBase<ShopBuyUI, ShopBuyUIModel>
    {
        public ShopBuyUIController(ShopBuyUI view, ShopBuyUIModel model)
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
            if (townData == null || backpackData == null)
                return;

            long totalCost = (long)Model.Price * quantity;
            if (totalCost <= 0 || townData.StashMoney < totalCost)
                return;

            townData.StashMoney -= totalCost;
            AddItemToBackpack(backpackData, Model.ItemId, quantity);
            SaveDataComponent.Instance.NotifyBackpackDataChanged();
            View.Close();
        }

        private void OnCancelRequested()
        {
            View.Close();
        }

        private void AddItemToBackpack(BackpackData backpackData, int itemId, int quantity)
        {
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
