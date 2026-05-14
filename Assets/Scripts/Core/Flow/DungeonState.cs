using CrystalMagic.UI;
using UnityEngine;

namespace CrystalMagic.Core
{
    public abstract class BattleStateBase : GameState
    {
        private const string UIPlayerInputLockReason = "BattleStateBase.UIOpen";
        private UIBase _battleUI;
        private CharacterUI _characterUI;
        private PropertyUI _propertyUI;
        private GameMenuUI _gameMenuUI;
        private UnitHealthBarManager _unitHealthBarManager;
        private bool _inputBound;
        private bool _playerInputLockedByUI;

        protected virtual string BattleUIName => "BattleUI";
        protected abstract string BattleSceneName { get; }

        public sealed override void OnEnter()
        {
            OnEnterBattle();
            _unitHealthBarManager ??= new UnitHealthBarManager();
            _unitHealthBarManager.Initialize();
            OpenBattleUI();
            BindInput();
        }

        public sealed override void OnUpdate()
        {
            OnUpdateBattle();
            RuntimeDataComponent.Instance?.TickPropSharedCooldown(Time.deltaTime);
            _unitHealthBarManager?.Tick();
            RefreshUIInputLock();
        }

        public sealed override void OnExit()
        {
            _unitHealthBarManager?.Dispose();
            _unitHealthBarManager = null;
            ReleaseUIInputLock();
            UnbindInput();
            OnExitBattle();
            _characterUI = null;
            _propertyUI = null;
            _battleUI = null;
        }

        protected virtual void OnEnterBattle()
        {
        }

        protected virtual void OnExitBattle()
        {
        }

        protected virtual void OnUpdateBattle()
        {
        }

        private void OpenBattleUI()
        {
            if (string.IsNullOrWhiteSpace(BattleUIName) || UIComponent.Instance == null)
            {
                return;
            }

            _battleUI = UIComponent.Instance.Open(BattleUIName);
        }

        private void BindInput()
        {
            if (_inputBound || InputComponent.Instance == null)
                return;

            InputComponent.Instance.OnInventory += HandleInventory;
            InputComponent.Instance.OnProperty += HandleProperty;
            if (UIComponent.Instance != null)
                UIComponent.Instance.EscapeUnhandled += HandleUnhandledEscape;
            _inputBound = true;
        }

        private void UnbindInput()
        {
            if (!_inputBound)
                return;

            if (InputComponent.Instance != null)
            {
                InputComponent.Instance.OnInventory -= HandleInventory;
                InputComponent.Instance.OnProperty -= HandleProperty;
            }
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

        private void HandleProperty()
        {
            if (_propertyUI == null || !UIComponent.Instance.IsManaged(_propertyUI))
            {
                _propertyUI = UIComponent.Instance.Open<PropertyUI>();
                return;
            }

            if (_propertyUI.gameObject.activeSelf)
            {
                _propertyUI.Close();
                return;
            }

            UIComponent.Instance.ShowUI(_propertyUI);
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
            bool shouldLock = UIComponent.Instance != null
                && UIComponent.Instance.HasActiveSceneScopedPanel(BattleSceneName, BattleUIName);
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

    public sealed class TrainingState : BattleStateBase
    {
        public const string SceneName = "TrainingScene";
        protected override string BattleSceneName => SceneName;

        protected override void OnEnterBattle()
        {
            Debug.Log("[TrainingState] Entered Training Ground");
            SaveDataComponent.Instance?.SetCurrentLocation(SaveAreaType.Training);

            if (StateData is LoadGameContext context)
            {
                Debug.Log($"[TrainingState] Loaded from save slot: {context.SaveIndex}");
            }
        }

        protected override void OnExitBattle()
        {
            Debug.Log("[TrainingState] Exited Training Ground");
        }

        public static TransitionData CreateEnterTransitionData(object data = null)
        {
            return new TransitionData
            {
                TargetSceneName = SceneName,
                TargetStateType = typeof(TrainingState),
                TargetStateData = data,
                TransitionUIName = "TransitionUI",
                ForceReloadTargetScene = true,
            };
        }
    }

    public class DungeonState : BattleStateBase
    {
        public const string SceneName = "DungeonScene";
        protected override string BattleSceneName => SceneName;

        protected override void OnEnterBattle()
        {
            Debug.Log("[DungeonState] Entered Dungeon");
            int dungeonFloor = 1;
            SaveAreaType previousAreaType = SaveDataComponent.Instance.GetLocationData()?.AreaType ?? SaveAreaType.Town;

            if (StateData is LoadGameContext context)
            {
                dungeonFloor = context.DungeonFloor;
                Debug.Log($"[DungeonState] Resuming dungeon at floor: {context.DungeonFloor}");
            }

            if (previousAreaType == SaveAreaType.Dungeon)
            {
                SaveDataComponent.Instance.EnsureDungeonRunExists(dungeonFloor);
            }
            else
            {
                SaveDataComponent.Instance.BeginDungeonRunFromPersistent(dungeonFloor);
            }

            SaveDataComponent.Instance?.SetCurrentLocation(SaveAreaType.Dungeon, dungeonFloor);
        }

        protected override void OnExitBattle()
        {
            Debug.Log("[DungeonState] Exited Dungeon");
        }
    }
}
