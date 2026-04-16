namespace CrystalMagic.UI
{
    public sealed class StashUIController : UIControllerBase<StashUI, StashUIModel>
    {
        public StashUIController(StashUI view, StashUIModel model)
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
