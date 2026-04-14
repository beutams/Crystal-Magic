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
            UIComponent.Instance.OpenChild<LoadUI>(View);
        }

        private void OnConfigRequested()
        {
            
        }

        private void OnExitRequested()
        {
            EventComponent.Instance.Publish(new MainMenuExitRequestedEvent());
        }
    }
}
