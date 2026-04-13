using System;
using CrystalMagic.Core;
namespace CrystalMagic.UI
{
    /// <summary>
    /// 主菜单
    /// </summary>
    public sealed class MainMenuUIController : IDisposable
    {
        private readonly MainMenuUI _view;
        private bool _disposed;

        public MainMenuUIController(MainMenuUI view)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _view.StartRequested += OnStartRequested;
            _view.LoadRequested += OnLoadRequested;
            _view.ConfigRequested += OnConfigRequested;
            _view.ExitRequested += OnExitRequested;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            _view.StartRequested -= OnStartRequested;
            _view.LoadRequested -= OnLoadRequested;
            _view.ConfigRequested -= OnConfigRequested;
            _view.ExitRequested -= OnExitRequested;
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
