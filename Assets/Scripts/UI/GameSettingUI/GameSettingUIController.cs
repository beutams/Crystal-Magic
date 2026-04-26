namespace CrystalMagic.UI
{
    using CrystalMagic.Core;

    public sealed class GameSettingUIController : UIControllerBase<GameSettingUI, GameSettingUIModel>
    {
        public GameSettingUIController(GameSettingUI view, GameSettingUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
            Bindings.Bind(() => View.SaveRequested += OnSaveRequested, () => View.SaveRequested -= OnSaveRequested);
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
