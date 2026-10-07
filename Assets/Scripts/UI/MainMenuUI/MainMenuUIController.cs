using CrystalMagic.Core;
namespace CrystalMagic.UI
{
    /// <summary>
    /// 主菜单
    /// </summary>
    public sealed class MainMenuUIController : UIControllerBase<MainMenuUI, MainMenuUIModel>
    {
        public MainMenuUIController(MainMenuUI view, MainMenuUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
            View.StartRequested += OnStartRequested;
            View.LoadRequested += OnLoadRequested;
            View.ConfigRequested += OnConfigRequested;
            View.ExitRequested += OnExitRequested;
        }

        protected override void OnClose()
        {
            View.StartRequested -= OnStartRequested;
            View.LoadRequested -= OnLoadRequested;
            View.ConfigRequested -= OnConfigRequested;
            View.ExitRequested -= OnExitRequested;
        }

        private void OnStartRequested()
        {
            UIComponent.Instance.OpenChild<SaveUI>(View);
        }

        private void OnLoadRequested()
        {
            UIComponent.Instance.OpenChild<LoadUI>(View, new LoadUIOpenData(
                slotIndex => EventComponent.Instance.Publish(new MainMenuLoadRequestedEvent(slotIndex))));
        }

        private void OnConfigRequested()
        {
            UIComponent.Instance.OpenChild<SettingUI>(View);
        }

        private void OnExitRequested()
        {
            UIComponent.Instance.OpenChild<ConfirmUI>(View, new ConfirmUIOpenData(
                LocalizationComponent.Instance.Get("ui.main_menu.exit_game"),
                LocalizationComponent.Instance.Get("ui.confirm.exit_game.content"),
                () => EventComponent.Instance.Publish(new MainMenuExitRequestedEvent())));
        }
    }
}
