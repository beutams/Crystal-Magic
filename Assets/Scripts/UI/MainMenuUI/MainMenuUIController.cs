using System;
using CrystalMagic.Core;
using UnityEngine;

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
            SaveDataComponent.Instance.Save();
            GetMainMenuState()?.GoToTown();
        }

        private void OnLoadRequested()
        {
            GetMainMenuState()?.StartLoadGame();
        }

        private void OnConfigRequested()
        {
            Debug.Log("[MainMenuUI] Config clicked");
        }

        private void OnExitRequested()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private static MainMenuState GetMainMenuState()
            => GameFlowComponent.Instance.GetCurrentState() as MainMenuState;
    }
}
