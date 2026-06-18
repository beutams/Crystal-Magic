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
            CharacterPropData propData = SaveDataComponent.Instance.GetCharacterPropData();
            if (townData == null || backpackData == null)
                return;

            long totalCost = (long)Model.Price * quantity;
            if (totalCost < 0 || townData.StashMoney < totalCost)
                return;

            if (!InventoryUtility.CanAddItemToCharacterInventory(backpackData, propData, Model.ItemId, quantity))
                return;

            int addedCount = AddItemToCharacterInventory(backpackData, propData, Model.ItemId, quantity);
            if (addedCount != quantity)
                return;

            townData.StashMoney -= totalCost;
            SaveDataComponent.Instance.NotifyBackpackDataChanged();
            View.Close();
        }

        private void OnCancelRequested()
        {
            View.Close();
        }

        private int AddItemToCharacterInventory(BackpackData backpackData, CharacterPropData propData, int itemId, int quantity)
        {
            return InventoryUtility.AddItemToCharacterInventory(backpackData, propData, itemId, quantity);
        }
    }
}
