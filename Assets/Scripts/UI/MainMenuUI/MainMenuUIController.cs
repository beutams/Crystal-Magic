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
            EventComponent.Instance.Publish(new MainMenuStartRequestedEvent());
        }

        private void OnLoadRequested()
        {
            EventComponent.Instance.Publish(new MainMenuLoadRequestedEvent("autosave"));
        }

        private void OnConfigRequested()
        {
            EventComponent.Instance.Publish(new MainMenuConfigRequestedEvent());
        }

        private void OnExitRequested()
        {
            EventComponent.Instance.Publish(new MainMenuExitRequestedEvent());
        }
    }
}
