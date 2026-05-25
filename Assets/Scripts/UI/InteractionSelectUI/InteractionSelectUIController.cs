using CrystalMagic.Core;

namespace CrystalMagic.UI
{
    public sealed class InteractionSelectUIController : UIControllerBase<InteractionSelectUI, InteractionSelectUIModel>
    {
        public InteractionSelectUIController(InteractionSelectUI view, InteractionSelectUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
            View.BindModel(Model);
            Bindings.Bind(() => View.OptionClicked += OnOptionClicked, () => View.OptionClicked -= OnOptionClicked);
        }

        private void OnOptionClicked(InteractionSelectOptionDisplayData option)
        {
            Model.ConfirmSelection(option);
            UIComponent.Instance.ReleaseUI(View);
        }
    }
}
