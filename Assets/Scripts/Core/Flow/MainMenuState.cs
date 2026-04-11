using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 主菜单状态
    /// </summary>
    public class MainMenuState : GameState
    {
        private UIBase _mainMenuUI;
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
                _mainMenuUI = uiInstance.GetComponent<UIBase>();

                if (_mainMenuUI != null)
                {
                    UIComponent.Instance.ShowUI(_mainMenuUI);
                    Debug.Log("[MainMenuState] MainMenu UI loaded and displayed");
                }
                else
                {
                    Debug.LogError("[MainMenuState] MainMenu prefab missing UIBase component");
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

            if (_mainMenuUI != null)
            {
                UIComponent.Instance.CloseUI(_mainMenuUI);
                PoolComponent.Instance.Release(_mainMenuUI.gameObject);
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
    }
}
