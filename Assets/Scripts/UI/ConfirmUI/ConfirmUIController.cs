namespace CrystalMagic.UI
{
    public sealed class ConfirmUIController : UIControllerBase<ConfirmUI, ConfirmUIModel>
    {
        public ConfirmUIController(ConfirmUI view, ConfirmUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
            View.SetTitle(Model.Title);
            View.SetContent(Model.Content);
            Bindings.Bind(() => View.ConfirmClicked += OnConfirmClicked, () => View.ConfirmClicked -= OnConfirmClicked);
            Bindings.Bind(() => View.CancelClicked += OnCancelClicked, () => View.CancelClicked -= OnCancelClicked);
        }

        private void OnConfirmClicked()
        {
            Model.ConfirmAction?.Invoke();
            View.Close();
        }

        private void OnCancelClicked()
        {
            Model.CancelAction?.Invoke();
            View.Close();
        }
    }
}
