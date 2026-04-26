namespace CrystalMagic.UI
{
    public sealed class ShopUIController : UIControllerBase<ShopUI, ShopUIModel>
    {
        private ShopItemInfoUI _itemInfoUI;

        public ShopUIController(ShopUI view, ShopUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
            View.BindModel(Model);
            View.CommodityHoverReady += OnCommodityHoverReady;
            View.CommodityHoverExited += OnCommodityHoverExited;
            Model.Refresh();
        }

        protected override void OnClose()
        {
            View.CommodityHoverReady -= OnCommodityHoverReady;
            View.CommodityHoverExited -= OnCommodityHoverExited;
            CloseItemInfoUI();
        }

        private void OnCommodityHoverReady(ShopCommodityDisplayData data)
        {
            if (data == null)
                return;

            CloseItemInfoUI();

            _itemInfoUI = CrystalMagic.Core.UIComponent.Instance.OpenChild<ShopItemInfoUI>(View, new ShopItemInfoUIOpenData
            {
                Name = data.Name,
                HaveCount = GetHaveCount(data.ItemId),
                Description = data.Description,
                Price = data.Price,
                IconPath = data.IconPath,
            });
        }

        private void OnCommodityHoverExited()
        {
            CloseItemInfoUI();
        }

        private void CloseItemInfoUI()
        {
            if (_itemInfoUI == null)
                return;

            _itemInfoUI.Close();
            _itemInfoUI = null;
        }

        private int GetHaveCount(int itemId)
        {
            CrystalMagic.Core.BackpackData backpackData = CrystalMagic.Core.SaveDataComponent.Instance.GetBackpackData();
            if (backpackData?.Items == null)
                return 0;

            int count = 0;
            for (int i = 0; i < backpackData.Items.Count; i++)
            {
                CrystalMagic.Core.InventoryItemData item = backpackData.Items[i];
                if (item == null || item.ItemId != itemId)
                    continue;

                count += item.Quantity;
            }

            return count;
        }
    }
}
