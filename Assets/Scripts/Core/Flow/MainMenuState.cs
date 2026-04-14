using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 主菜单状态
    /// </summary>
    public class MainMenuState : GameState
    {
        private MainMenuUI _mainMenuUI;
        private bool _eventsBound;

        public override void OnEnter()
        {
            Debug.Log("[MainMenuState] Entered MainMenu");
            // 从对象池加载并显示 MainMenu UI
            BindEvents();
            _mainMenuUI = UIComponent.Instance.Open<MainMenuUI>();
        }

        public override void OnExit()
        {
            Debug.Log("[MainMenuState] Exited MainMenu");
            UnbindEvents();
            if (_mainMenuUI != null)
            {
                UIComponent.Instance.ReleaseUI(_mainMenuUI);
                _mainMenuUI = null;
            }
        }
        /// <summary>
        /// 从主菜单读档进入游戏（带转场）
        /// </summary>
        public void StartLoadGame(int saveIndex)
        {
            GameFlowComponent.Instance.SetState<LoadGameState>(saveIndex);
        }

        private void BindEvents()
        {
            if (_eventsBound)
                return;

            EventComponent.Instance.Subscribe<MainMenuStartRequestedEvent>(HandleStartGameRequested);
            EventComponent.Instance.Subscribe<MainMenuLoadRequestedEvent>(HandleLoadGameRequested);
            EventComponent.Instance.Subscribe<MainMenuExitRequestedEvent>(HandleExitGameRequested);
            _eventsBound = true;
        }

        private void UnbindEvents()
        {
            if (!_eventsBound)
                return;

            EventComponent.Instance.Unsubscribe<MainMenuStartRequestedEvent>(HandleStartGameRequested);
            EventComponent.Instance.Unsubscribe<MainMenuLoadRequestedEvent>(HandleLoadGameRequested);
            EventComponent.Instance.Unsubscribe<MainMenuExitRequestedEvent>(HandleExitGameRequested);
            _eventsBound = false;
        }

        private void HandleStartGameRequested(MainMenuStartRequestedEvent gameEvent)
        {
            SaveDataComponent.Instance.SaveToSlot(gameEvent.Index);
            StartLoadGame(gameEvent.Index);
        }

        private void HandleLoadGameRequested(MainMenuLoadRequestedEvent gameEvent)
        {
            StartLoadGame(gameEvent.Index);
        }

        private void HandleExitGameRequested(MainMenuExitRequestedEvent gameEvent)
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
