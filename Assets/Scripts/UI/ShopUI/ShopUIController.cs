namespace CrystalMagic.UI
{
    public sealed class ShopUIController : UIControllerBase<ShopUI, ShopUIModel>
    {
        public ShopUIController(ShopUI view, ShopUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
            View.BindModel(Model);
            Model.Refresh();
        }
    }
}
