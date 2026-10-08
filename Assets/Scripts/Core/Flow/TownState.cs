using UnityEngine;
using CrystalMagic.UI;
using Unity.Entities;

namespace CrystalMagic.Core {
    /// <summary>
    /// 城镇状态
    /// </summary>
    public class TownState : GameState
    {
        public const string SceneName = "TownScene";
        public const string SubSceneName = "TownSubScene";
        private const string UIPlayerInputLockReason = "TownState.UIOpen";
        private CharacterUI _characterUI;
        private InteractionPromptManager _interactionPromptManager;
        private NotificationUI _notificationUI;
        private bool _inputBound;
        private bool _playerInputLockedByUI;
        private World _introWorld;
        private Entity _introTrigger;

        public override void OnEnter()
        {
            Debug.Log("[TownState] Entered Town");
            InputComponent.Instance?.SetBattleInputEnabled(false);
            LoadGameContext context = StateData as LoadGameContext;
            using (SceneLoadTiming.Measure("Town: bind character and restore player state"))
            {
                GameRuntimeStateUtility.BindPlayerCharacterData(context?.Character ?? new CharacterData());
                GameRuntimeStateUtility.ApplyPlayerRuntimeState(context?.Player);
            }
            using (SceneLoadTiming.Measure("Town: initialize interaction prompts"))
            {
                _interactionPromptManager ??= new InteractionPromptManager();
                _interactionPromptManager.Initialize();
            }
            using (SceneLoadTiming.Measure("Town: open NotificationUI"))
                _notificationUI = UIComponent.Instance.Open<NotificationUI>();
            using (SceneLoadTiming.Measure("Town: bind input"))
                BindInput();
            
            // 可以在这里访问 StateData（如果是从读档进入）
            if (context != null)
            {
                Debug.Log($"[TownState] Loaded from slot index: {context.SaveIndex}");
            }
        }

        public override void OnExit()
        {
            if (_introWorld != null && _introWorld.IsCreated && _introWorld.EntityManager.Exists(_introTrigger))
            {
                _introWorld.GetExistingSystemManaged<StateScriptManagedCommandSystem>()?.CancelNpcInteraction(_introTrigger);
                _introWorld.EntityManager.DestroyEntity(_introTrigger);
            }
            _introWorld = null;
            _introTrigger = Entity.Null;
            Debug.Log("[TownState] Exited Town");
            _interactionPromptManager?.Dispose();
            _interactionPromptManager = null;
            if (_notificationUI != null && UIComponent.Instance.IsManaged(_notificationUI))
                UIComponent.Instance.ReleaseUI(_notificationUI);
            _notificationUI = null;
            ReleaseUIInputLock();
            UnbindInput();
        }

        public override void OnUpdate()
        {
            _interactionPromptManager?.Tick();
            RefreshUIInputLock();
            if (_introTrigger == Entity.Null && !TransitionComponent.Instance.IsTransitioning &&
                TownIntroTriggerUtility.IsPending(SaveDataComponent.Instance) &&
                GameRuntimeStateUtility.TryGetPlayerEntity(out EntityManager manager, out Entity player))
            {
                _introTrigger = TownIntroTriggerUtility.TryCreate(manager, player);
                if (_introTrigger != Entity.Null)
                    _introWorld = manager.World;
            }
        }

        public static TransitionData CreateEnterTransitionData(object data = null)
        {
            return new TransitionData
            {
                TargetSceneName = SceneName,
                TargetStateType = typeof(TownState),
                TargetStateData = data,
                TransitionUIName = "TransitionUI",
                KeepCurrentMainScene = true,
                ActiveSubSceneNames = new[] { DungeonState.RegistrySubSceneName, SubSceneName },
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
            if (GameWorldManager.GameWorld?.GetExistingSystemManaged<StateScriptManagedCommandSystem>()?.HasNpcInteraction == true)
                return;
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
            if (_characterUI == null || !UIComponent.Instance.IsManaged(_characterUI))
            {
                _characterUI = UIComponent.Instance.Open<CharacterUI>(CharacterPage.Setting);
                return;
            }

            UIComponent.Instance.ShowUI(_characterUI);
            _characterUI.ShowSettings();
        }

        private void RefreshUIInputLock()
        {
            bool shouldLock = UIComponent.Instance != null && UIComponent.Instance.HasActiveSceneScopedPanel(SceneName, nameof(NotificationUI));
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
