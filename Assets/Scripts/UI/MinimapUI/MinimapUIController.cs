namespace CrystalMagic.UI
{
    public sealed class MinimapUIController : UIControllerBase<MinimapUI, MinimapUIModel>
    {
        public MinimapUIController(MinimapUI view, MinimapUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
            View.BindModel(Model);
            Model.Refresh();
        }

        protected override void OnUpdate()
        {
            Model.RefreshRuntime();
        }
    }
}
