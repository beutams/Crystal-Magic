namespace CrystalMagic.UI
{
    public sealed class ShopUIController : UIControllerBase<ShopUI, ShopUIModel>
    {
        private ShopItemInfoUI _itemInfoUI;
        private ShopBuyUI _buyUI;
        private ShopSellUI _sellUI;
        private readonly System.Action<CrystalMagic.Core.CommonGameEvent> _refreshHandler;

        public ShopUIController(ShopUI view, ShopUIModel model)
            : base(view, model)
        {
            _refreshHandler = _ => Model.Refresh();
        }

        protected override void OnOpen()
        {
            View.BindModel(Model);
            View.CommodityHoverReady += OnCommodityHoverReady;
            View.CommodityBuyRequested += OnCommodityBuyRequested;
            View.InventorySellRequested += OnInventorySellRequested;
            View.CommodityHoverExited += OnCommodityHoverExited;
            BindEvent(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.SaveDataComponent.BackpackDataChangedEventName), _refreshHandler);
            BindEvent(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.SaveDataComponent.TownDataChangedEventName), _refreshHandler);
            Model.Refresh();
        }

        protected override void OnClose()
        {
            View.CommodityHoverReady -= OnCommodityHoverReady;
            View.CommodityBuyRequested -= OnCommodityBuyRequested;
            View.InventorySellRequested -= OnInventorySellRequested;
            View.CommodityHoverExited -= OnCommodityHoverExited;
            CloseItemInfoUI();
            CloseBuyUI();
            CloseSellUI();
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

        private void OnCommodityBuyRequested(ShopCommodityDisplayData data)
        {
            if (data == null)
                return;

            CloseItemInfoUI();
            CloseBuyUI();
            CloseSellUI();

             if (!CanOpenBuyUI(data, out string tipMessage))
            {
                ShowTip(tipMessage);
                return;
            }

            _buyUI = CrystalMagic.Core.UIComponent.Instance.OpenChild<ShopBuyUI>(View, new CrystalMagic.UI.ShopBuyUIOpenData
            {
                ItemId = data.ItemId,
                Name = data.Name,
                HaveCount = GetHaveCount(data.ItemId),
                Description = data.Description,
                Price = data.Price,
                IconPath = data.IconPath,
            });
        }

        private void OnInventorySellRequested(ShopInventoryDisplayData data)
        {
            if (data == null)
                return;

            CloseItemInfoUI();
            CloseBuyUI();
            CloseSellUI();

            _sellUI = CrystalMagic.Core.UIComponent.Instance.OpenChild<ShopSellUI>(View, new ShopSellUIOpenData
            {
                SlotIndex = data.SlotIndex,
                ItemId = data.ItemId,
                Name = data.Name,
                HaveCount = data.Count,
                Description = data.Description,
                Price = data.SellPrice,
                IconPath = data.IconPath,
            });
        }

        private void CloseItemInfoUI()
        {
            if (_itemInfoUI == null)
                return;

            _itemInfoUI.Close();
            _itemInfoUI = null;
        }

        private void CloseBuyUI()
        {
            if (_buyUI == null)
                return;

            _buyUI.Close();
            _buyUI = null;
        }

        private void CloseSellUI()
        {
            if (_sellUI == null)
                return;

            _sellUI.Close();
            _sellUI = null;
        }

        private int GetHaveCount(int itemId)
        {
            CrystalMagic.Core.BackpackData backpackData = CrystalMagic.Core.SaveDataComponent.Instance.GetBackpackData();
            CrystalMagic.Core.CharacterPropData propData = CrystalMagic.Core.SaveDataComponent.Instance.GetCharacterPropData();
            return CrystalMagic.Core.InventoryUtility.GetItemCountInCharacterInventory(backpackData, propData, itemId);
        }

        private bool CanOpenBuyUI(ShopCommodityDisplayData data, out string tipMessage)
        {
            tipMessage = string.Empty;
            if (data == null)
            {
                tipMessage = CrystalMagic.Core.LocalizationComponent.Instance.Get("ui.shop.purchase_unavailable");
                return false;
            }

            if (data.Price < 0)
            {
                tipMessage = CrystalMagic.Core.LocalizationComponent.Instance.Get("ui.shop.purchase_unavailable");
                return false;
            }

            long money = CrystalMagic.Core.SaveDataComponent.Instance.GetTownData()?.StashMoney ?? 0;
            if (money < data.Price)
            {
                tipMessage = CrystalMagic.Core.LocalizationComponent.Instance.Get("ui.shop.insufficient_money");
                return false;
            }

            if (!CrystalMagic.Core.InventoryUtility.CanAddItemToCharacterInventory(
                    CrystalMagic.Core.SaveDataComponent.Instance.GetBackpackData(),
                    CrystalMagic.Core.SaveDataComponent.Instance.GetCharacterPropData(),
                    data.ItemId,
                    1))
            {
                tipMessage = CrystalMagic.Core.LocalizationComponent.Instance.Get("ui.shop.inventory_full");
                return false;
            }

            return true;
        }

        private void ShowTip(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            CrystalMagic.Core.UIComponent.Instance.Open<TipForm>(new TipFormOpenData
            {
                Info = message,
            });
        }
    }
}
