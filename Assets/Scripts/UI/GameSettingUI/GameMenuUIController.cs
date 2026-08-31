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
            Bindings.Bind(() => View.ContinueRequested += OnContinueRequested, () => View.ContinueRequested -= OnContinueRequested);
            Bindings.Bind(() => View.SaveRequested += OnSaveRequested, () => View.SaveRequested -= OnSaveRequested);
            Bindings.Bind(() => View.ReturnMainMenuRequested += OnReturnMainMenuRequested, () => View.ReturnMainMenuRequested -= OnReturnMainMenuRequested);
            Model.ReloadFromSettings();
        }

        private void OnContinueRequested()
        {
            View.Close();
        }

        private void OnSaveRequested()
        {
            if (!SaveDataComponent.Instance.Save())
                return;

            ConfirmUIOpenData openData = new(
                LocalizationComponent.Instance.Get("ui.confirm.save"),
                LocalizationComponent.Instance.Get("ui.confirm.save_success.content"));
            UIComponent.Instance.OpenChild<ConfirmUI>(View, openData);
        }

        private void OnReturnMainMenuRequested()
        {
            GameFlowComponent.Instance.BeginTransition(new TransitionData
            {
                TargetSceneName = "MainMenu",
                TargetStateType = typeof(MainMenuState),
                TransitionUIName = "TransitionUI",
                ForceReloadTargetScene = true,
            });
        }
    }
}
