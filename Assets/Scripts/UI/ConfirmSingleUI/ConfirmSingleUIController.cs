namespace CrystalMagic.UI
{
    public sealed class ConfirmSingleUIController : UIControllerBase<ConfirmSingleUI, ConfirmSingleUIModel>
    {
        public ConfirmSingleUIController(ConfirmSingleUI view, ConfirmSingleUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
            View.BindModel(Model);
            View.SetTitle(Model.Title);
            View.SetContent(Model.Content);
            View.SetConfirmLabel(Model.ConfirmLabel);
            Bindings.Bind(() => View.ConfirmClicked += OnConfirmClicked, () => View.ConfirmClicked -= OnConfirmClicked);
        }

        private void OnConfirmClicked()
        {
            Model.ConfirmAction?.Invoke();
            View.Close();
        }
    }
}
