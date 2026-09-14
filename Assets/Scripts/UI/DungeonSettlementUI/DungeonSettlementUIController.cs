namespace CrystalMagic.UI
{
    public sealed class DungeonSettlementUIController : UIControllerBase<DungeonSettlementUI, DungeonSettlementUIModel>
    {
        public DungeonSettlementUIController(DungeonSettlementUI view, DungeonSettlementUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
            View.BindModel(Model);
            Bindings.Bind(() => View.ConfirmClicked += OnConfirmClicked, () => View.ConfirmClicked -= OnConfirmClicked);
        }

        private void OnConfirmClicked()
        {
            Model.ConfirmAction?.Invoke();
            View.Close();
        }
    }
}
