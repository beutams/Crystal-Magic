namespace CrystalMagic.UI
{
    public sealed class PropertyUIController : UIControllerBase<PropertyUI, PropertyUIModel>
    {
        public PropertyUIController(PropertyUI view, PropertyUIModel model)
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
