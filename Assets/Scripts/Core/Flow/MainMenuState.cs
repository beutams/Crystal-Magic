using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 主菜单状态
    /// </summary>
    public class MainMenuState : GameState
    {
        private MainMenuUI _mainMenuUI;
        private GameObject _cameraGo;
        private bool _eventsBound;

        public override void OnEnter()
        {
            Debug.Log("[MainMenuState] Entered MainMenu");

            // 创建 UI 正交相机
            _cameraGo = new GameObject("MainMenuCamera");
            Camera cam = _cameraGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            cam.cullingMask = LayerMask.GetMask("UI");
            cam.depth = 0;
            SceneCamera sceneCamera = _cameraGo.AddComponent<SceneCamera>();

            // 从对象池加载并显示 MainMenu UI
            BindEvents();
            _mainMenuUI = UIComponent.Instance.Open<MainMenuUI>();

            if (_mainMenuUI != null)
            {
                Debug.Log("[MainMenuState] MainMenu UI loaded and displayed");
            }
            else
            {
                UnbindEvents();
                Debug.LogError("[MainMenuState] Failed to open MainMenu UI");
            }
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

            if (_cameraGo != null)
            {
                Object.Destroy(_cameraGo);
                _cameraGo = null;
            }
        }

        public override void OnUpdate()
        {
        }

        /// <summary>
        /// 从主菜单进入城镇（带转场）
        /// </summary>
        public void GoToTown(object data = null)
        {
            TransitionData transData = new TransitionData
            {
                TargetSceneName = "Game",
                TargetStateType = typeof(TownState),
                TargetStateData = data
            };
            GameFlowComponent.Instance.SetState<TransitionState>(transData);
        }

        /// <summary>
        /// 从主菜单读档进入游戏（带转场）
        /// </summary>
        public void StartLoadGame(string slotName = "autosave")
        {
            GameFlowComponent.Instance.SetState<LoadGameState>(slotName);
        }

        private void BindEvents()
        {
            if (_eventsBound)
                return;

            EventComponent.Instance.Subscribe<MainMenuStartRequestedEvent>(HandleStartGameRequested);
            EventComponent.Instance.Subscribe<MainMenuLoadRequestedEvent>(HandleLoadGameRequested);
            EventComponent.Instance.Subscribe<MainMenuConfigRequestedEvent>(HandleConfigOpenRequested);
            EventComponent.Instance.Subscribe<MainMenuExitRequestedEvent>(HandleExitGameRequested);
            _eventsBound = true;
        }

        private void UnbindEvents()
        {
            if (!_eventsBound)
                return;

            EventComponent.Instance.Unsubscribe<MainMenuStartRequestedEvent>(HandleStartGameRequested);
            EventComponent.Instance.Unsubscribe<MainMenuLoadRequestedEvent>(HandleLoadGameRequested);
            EventComponent.Instance.Unsubscribe<MainMenuConfigRequestedEvent>(HandleConfigOpenRequested);
            EventComponent.Instance.Unsubscribe<MainMenuExitRequestedEvent>(HandleExitGameRequested);
            _eventsBound = false;
        }

        private void HandleStartGameRequested(MainMenuStartRequestedEvent gameEvent)
        {
            SaveDataComponent.Instance.Save();
            GoToTown();
        }

        private void HandleLoadGameRequested(MainMenuLoadRequestedEvent gameEvent)
        {
            string slotName = string.IsNullOrEmpty(gameEvent.SlotName) ? "autosave" : gameEvent.SlotName;
            StartLoadGame(slotName);
        }

        private void HandleConfigOpenRequested(MainMenuConfigRequestedEvent gameEvent)
        {
            Debug.Log("[MainMenuState] Config clicked");
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
