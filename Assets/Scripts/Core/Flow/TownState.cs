using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 城镇状态
    /// </summary>
    public class TownState : GameState
    {
        public const string SceneName = "TownScene";
        private const string UIPlayerInputLockReason = "TownState.UIOpen";
        private CharacterUI _characterUI;
        private GameMenuUI _gameMenuUI;
        private bool _inputBound;
        private bool _playerInputLockedByUI;

        public override void OnEnter()
        {
            Debug.Log("[TownState] Entered Town");
            InputComponent.Instance?.SetBattleInputEnabled(false);
            SaveDataComponent.Instance?.SetCurrentLocation(SaveAreaType.Town);
            BindInput();
            
            // 可以在这里访问 StateData（如果是从读档进入）
            if (StateData is LoadGameContext context)
            {
                Debug.Log($"[TownState] Loaded from slot index: {context.SaveIndex}");
            }
        }

        public override void OnExit()
        {
            Debug.Log("[TownState] Exited Town");
            ReleaseUIInputLock();
            UnbindInput();
        }

        public override void OnUpdate()
        {
            RefreshUIInputLock();
        }

        public static TransitionData CreateEnterTransitionData(object data = null)
        {
            return new TransitionData
            {
                TargetSceneName = SceneName,
                TargetStateType = typeof(TownState),
                TargetStateData = data,
                TransitionUIName = "TransitionUI",
                ForceReloadTargetScene = true,
            };
        }

        private void BindInput()
        {
            if (_inputBound || InputComponent.Instance == null)
                return;

            InputComponent.Instance.OnInventory += HandleInventory;
            if (UIComponent.Instance != null)
                UIComponent.Instance.EscapeUnhandled += HandleUnhandledEscape;
            _inputBound = true;
        }

        private void UnbindInput()
        {
            if (!_inputBound)
                return;

            if (InputComponent.Instance != null)
                InputComponent.Instance.OnInventory -= HandleInventory;
            if (UIComponent.Instance != null)
                UIComponent.Instance.EscapeUnhandled -= HandleUnhandledEscape;
            _inputBound = false;
        }

        private void HandleInventory()
        {
            if (_characterUI == null || !UIComponent.Instance.IsManaged(_characterUI))
            {
                _characterUI = UIComponent.Instance.Open<CharacterUI>();
                return;
            }

            if (_characterUI.gameObject.activeSelf)
            {
                _characterUI.Close();
                return;
            }

            UIComponent.Instance.ShowUI(_characterUI);
        }

        private void HandleUnhandledEscape()
        {
            if (_gameMenuUI == null || !UIComponent.Instance.IsManaged(_gameMenuUI))
            {
                _gameMenuUI = UIComponent.Instance.Open<GameMenuUI>();
                return;
            }

            if (_gameMenuUI.gameObject.activeSelf)
                return;

            UIComponent.Instance.ShowUI(_gameMenuUI);
        }

        private void RefreshUIInputLock()
        {
            bool shouldLock = UIComponent.Instance != null && UIComponent.Instance.HasActiveSceneScopedPanel(SceneName);
            if (shouldLock == _playerInputLockedByUI)
                return;

            if (shouldLock)
            {
                GameGateComponent.Instance.Lock(GameGateType.PlayerInput, UIPlayerInputLockReason);
                _playerInputLockedByUI = true;
                return;
            }

            ReleaseUIInputLock();
        }

        private void ReleaseUIInputLock()
        {
            if (!_playerInputLockedByUI)
                return;

            GameGateComponent.Instance.Unlock(GameGateType.PlayerInput, UIPlayerInputLockReason);
            _playerInputLockedByUI = false;
        }
    }
}
