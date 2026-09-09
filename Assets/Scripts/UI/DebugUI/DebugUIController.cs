using CrystalMagic.Core;

namespace CrystalMagic.UI
{
    public sealed class DebugUIController : UIControllerBase<DebugUI, DebugUIModel>
    {
        public DebugUIController(DebugUI view, DebugUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
            View.BindModel(Model);
            Bindings.Bind(
                () => View.ContentToggleRequested += HandleContentToggleRequested,
                () => View.ContentToggleRequested -= HandleContentToggleRequested);
            Bindings.Bind(
                () => View.ContentHideRequested += HandleContentHideRequested,
                () => View.ContentHideRequested -= HandleContentHideRequested);
            Bindings.Bind(
                () => View.PageRequested += HandlePageRequested,
                () => View.PageRequested -= HandlePageRequested);
            BindEvent<UnitDamagedEvent>(Model.HandleUnitDamaged);
        }

        protected override void OnUpdate()
        {
            Model.RefreshRuntime();
        }

        private void HandleContentToggleRequested()
        {
            Model.SetContentVisible(!Model.IsContentVisible);
        }

        private void HandleContentHideRequested()
        {
            Model.SetContentVisible(false);
        }

        private void HandlePageRequested(DebugPage page)
        {
            Model.SelectPage(page);
        }
    }
}
