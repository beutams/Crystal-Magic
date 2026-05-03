namespace CrystalMagic.UI
{
    public sealed class TipFormController : UIControllerBase<TipForm, TipFormModel>
    {
        public TipFormController(TipForm view, TipFormModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
            View.BindModel(Model);
        }
    }
}
