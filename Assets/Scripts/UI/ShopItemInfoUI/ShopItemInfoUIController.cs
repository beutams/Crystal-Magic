namespace CrystalMagic.UI
{
    public sealed class ShopItemInfoUIController : UIControllerBase<ShopItemInfoUI, ShopItemInfoUIModel>
    {
        public ShopItemInfoUIController(ShopItemInfoUI view, ShopItemInfoUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
            View.BindModel(Model);
        }
    }
}
