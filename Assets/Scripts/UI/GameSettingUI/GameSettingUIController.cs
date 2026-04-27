namespace CrystalMagic.UI
{
    using CrystalMagic.Core;

    public sealed class GameMenuUIController : UIControllerBase<GameMenuUI, GameMenuUIModel>
    {
        public GameMenuUIController(GameMenuUI view, GameMenuUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
            View.BindModel(Model);
            Bindings.Bind(() => View.SaveRequested += OnSaveRequested, () => View.SaveRequested -= OnSaveRequested);
            Model.ReloadFromSettings();
        }

        private void OnSaveRequested()
        {
            foreach (UIBase child in UIComponent.Instance.GetChildren(View))
            {
                if (child is GameSaveUI)
                    return;
            }

            UIComponent.Instance.OpenChild<GameSaveUI>(View);
        }
    }
}
