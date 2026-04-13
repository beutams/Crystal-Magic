using CrystalMagic.UI;
using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 主菜单状态
    /// </summary>
    public class MainMenuState : GameState
    {
        private MainMenuUI _mainMenuUI;
        private MainMenuUIController _mainMenuController;
        private GameObject _cameraGo;

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
            GameObject uiInstance = PoolComponent.Instance.Get(AssetPathHelper.GetUIAsset("MainMenuUI"));

            if (uiInstance != null)
            {
                _mainMenuUI = uiInstance.GetComponent<MainMenuUI>();

                if (_mainMenuUI != null)
                {
                    BindController();
                    UIComponent.Instance.ShowUI(_mainMenuUI);
                    Debug.Log("[MainMenuState] MainMenu UI loaded and displayed");
                }
                else
                {
                    Debug.LogError("[MainMenuState] MainMenu prefab missing MainMenuUI component");
                }
            }
            else
            {
                Debug.LogError("[MainMenuState] Failed to get MainMenu from pool");
            }
        }

        public override void OnExit()
        {
            Debug.Log("[MainMenuState] Exited MainMenu");

            DisposeController();

            if (_mainMenuUI != null)
            {
                UIComponent.Instance.CloseUI(_mainMenuUI);
                PoolComponent.Instance.Release(_mainMenuUI.gameObject);
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

        private void BindController()
        {
            _mainMenuController = new MainMenuUIController(_mainMenuUI);
            EventComponent.Instance.Subscribe<MainMenuStartRequestedEvent>(HandleStartGameRequested);
            EventComponent.Instance.Subscribe<MainMenuLoadRequestedEvent>(HandleLoadGameRequested);
            EventComponent.Instance.Subscribe<MainMenuConfigRequestedEvent>(HandleConfigOpenRequested);
            EventComponent.Instance.Subscribe<MainMenuExitRequestedEvent>(HandleExitGameRequested);
        }

        private void DisposeController()
        {
            if (_mainMenuController == null)
                return;

            EventComponent.Instance.Unsubscribe<MainMenuStartRequestedEvent>(HandleStartGameRequested);
            EventComponent.Instance.Unsubscribe<MainMenuLoadRequestedEvent>(HandleLoadGameRequested);
            EventComponent.Instance.Unsubscribe<MainMenuConfigRequestedEvent>(HandleConfigOpenRequested);
            EventComponent.Instance.Unsubscribe<MainMenuExitRequestedEvent>(HandleExitGameRequested);
            _mainMenuController.Dispose();
            _mainMenuController = null;
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
