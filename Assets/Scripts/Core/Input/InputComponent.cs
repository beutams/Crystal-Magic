using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CrystalMagic.Core {
    public struct InputState
    {
        public Vector2 Move;
        public Vector3 PointerWorldPosition;
        public bool IsPrimaryHeld;
        public bool IsInteractHeld;
        public bool IsInventoryHeld;
        public bool IsPropertyHeld;
        public bool IsEscapeHeld;
        public bool IsSkillHeld;
        public int SkillChainIndex;
        public bool IsNextSkillChainHeld;
        public bool IsUsePropHeld;
        public int PropIndex;
    }

    /// <summary>
    /// 输入组件
    /// </summary>
    public class InputComponent : GameComponent<InputComponent>
    {
        public override int Priority => 5;
        private InputControls _controls;
        private bool _playerInputLocked;
        private bool _uiInputLocked;
        private bool _battleInputEnabled;
        private InputState _currentState;

        public InputState CurrentState => _currentState;

        #region 事件
        public event Action<Vector2> OnMove;
        public event Action<Vector3> OnMouseWorldPosition;
        public event Action OnMouseClick;
        public event Action OnMousePress;
        public event Action OnInteract;
        public event Action OnInventory;
        public event Action OnProperty;
        public event Action OnEscape;
        public event Action<int> OnUseProp;
        public event Action<InputState> OnInputStateChanged;
        #endregion

        #region 调用
        public override void Initialize()
        {
            base.Initialize();
            _controls = new InputControls();
            ResetInputState();

            _controls.Interaction.Move.performed += HandleMove;
            _controls.Interaction.Move.canceled += HandleMoveCanceled;
            _controls.Interaction.Interact.performed += HandleInteract;
            _controls.Interaction.Interact.canceled += HandleInteractCanceled;
            _controls.Interaction.Inventory.performed += HandleInventory;
            _controls.Interaction.Inventory.canceled += HandleInventoryCanceled;
            _controls.Interaction.Property.performed += HandleProperty;
            _controls.Interaction.Property.canceled += HandlePropertyCanceled;
            _controls.Interaction.Click.performed += HandleClick;
            _controls.Interaction.Click.canceled += HandleClickCanceled;
            _controls.Battle.UseProp.performed += HandleUseProp;
            _controls.Battle.UseProp.canceled += HandleUsePropCanceled;
            _controls.Battle.Skill.performed += HandleSkill;
            _controls.Battle.Skill.canceled += HandleSkillCanceled;
            _controls.Battle.Tab.performed += HandleTab;
            _controls.Battle.Tab.canceled += HandleTabCanceled;
            _controls.Global.ESC.performed += HandleEscape;
            _controls.Global.ESC.canceled += HandleEscapeCanceled;

            _controls.Interaction.Enable();
            _controls.Global.Enable();
            _controls.Battle.Disable();
            _playerInputLocked = GameGateComponent.Instance.IsPlayerInputLocked;
            _uiInputLocked = GameGateComponent.Instance.IsUIInputLocked;
            _battleInputEnabled = false;
            ApplyPlayerInputLockState();
            EventComponent.Instance.Subscribe<GameGateChangedEvent>(HandleGameGateChanged);
        }

        public override void Cleanup()
        {
            EventComponent.Instance.Unsubscribe<GameGateChangedEvent>(HandleGameGateChanged);
            if (_controls != null)
            {
                _controls.Interaction.Move.performed -= HandleMove;
                _controls.Interaction.Move.canceled -= HandleMoveCanceled;
                _controls.Interaction.Interact.performed -= HandleInteract;
                _controls.Interaction.Interact.canceled -= HandleInteractCanceled;
                _controls.Interaction.Inventory.performed -= HandleInventory;
                _controls.Interaction.Inventory.canceled -= HandleInventoryCanceled;
                _controls.Interaction.Property.performed -= HandleProperty;
                _controls.Interaction.Property.canceled -= HandlePropertyCanceled;
                _controls.Interaction.Click.performed -= HandleClick;
                _controls.Interaction.Click.canceled -= HandleClickCanceled;
                _controls.Battle.UseProp.performed -= HandleUseProp;
                _controls.Battle.UseProp.canceled -= HandleUsePropCanceled;
                _controls.Battle.Skill.performed -= HandleSkill;
                _controls.Battle.Skill.canceled -= HandleSkillCanceled;
                _controls.Battle.Tab.performed -= HandleTab;
                _controls.Battle.Tab.canceled -= HandleTabCanceled;
                _controls.Global.ESC.performed -= HandleEscape;
                _controls.Global.ESC.canceled -= HandleEscapeCanceled;

                _controls.Interaction.Disable();
                _controls.Battle.Disable();
                _controls.Global.Disable();
                _controls.Dispose();
                _controls = null;
            }

            base.Cleanup();
        }
        private void HandleMove(InputAction.CallbackContext ctx)
        {
            _currentState.Move = ctx.ReadValue<Vector2>();
            PublishInputState();
            OnMove?.Invoke(_currentState.Move);
        }

        private void HandleMoveCanceled(InputAction.CallbackContext ctx)
        {
            _currentState.Move = Vector2.zero;
            PublishInputState();
            OnMove?.Invoke(Vector2.zero);
        }

        private void HandleClick(InputAction.CallbackContext ctx)
        {
            _currentState.IsPrimaryHeld = true;
            PublishInputState();
            OnMouseClick?.Invoke();
        }

        private void HandleClickCanceled(InputAction.CallbackContext ctx)
        {
            _currentState.IsPrimaryHeld = false;
            PublishInputState();
        }

        private void HandleInteract(InputAction.CallbackContext ctx)
        {
            _currentState.IsInteractHeld = true;
            PublishInputState();
            OnInteract?.Invoke();
        }

        private void HandleInteractCanceled(InputAction.CallbackContext ctx)
        {
            _currentState.IsInteractHeld = false;
            PublishInputState();
        }

        private void HandleInventory(InputAction.CallbackContext ctx)
        {
            _currentState.IsInventoryHeld = true;
            PublishInputState();
            OnInventory?.Invoke();
        }

        private void HandleInventoryCanceled(InputAction.CallbackContext ctx)
        {
            _currentState.IsInventoryHeld = false;
            PublishInputState();
        }

        private void HandleProperty(InputAction.CallbackContext ctx)
        {
            _currentState.IsPropertyHeld = true;
            PublishInputState();
            OnProperty?.Invoke();
        }

        private void HandlePropertyCanceled(InputAction.CallbackContext ctx)
        {
            _currentState.IsPropertyHeld = false;
            PublishInputState();
        }

        private void HandleUseProp(InputAction.CallbackContext ctx)
        {
            int shortcutNumber = Mathf.RoundToInt(ctx.ReadValue<float>());
            int shortcutIndex = shortcutNumber - 1;
            if (shortcutIndex < 0)
                return;

            _currentState.IsUsePropHeld = true;
            _currentState.PropIndex = shortcutIndex;
            PublishInputState();
            OnUseProp?.Invoke(shortcutIndex);
        }

        private void HandleUsePropCanceled(InputAction.CallbackContext ctx)
        {
            _currentState.IsUsePropHeld = false;
            _currentState.PropIndex = -1;
            PublishInputState();
        }

        private void HandleSkill(InputAction.CallbackContext ctx)
        {
            int skillChainNumber = Mathf.RoundToInt(ctx.ReadValue<float>());
            int skillChainIndex = skillChainNumber - 1;
            if (skillChainIndex < 0 || skillChainIndex >= 5)
                return;

            _currentState.IsSkillHeld = true;
            _currentState.SkillChainIndex = skillChainIndex;
            PublishInputState();
            RuntimeDataComponent.Instance.SetCurrentSkillChainIndex(skillChainIndex, SaveDataComponent.Instance?.GetSkillData());
        }

        private void HandleSkillCanceled(InputAction.CallbackContext ctx)
        {
            _currentState.IsSkillHeld = false;
            _currentState.SkillChainIndex = -1;
            PublishInputState();
        }

        private void HandleTab(InputAction.CallbackContext ctx)
        {
            _currentState.IsNextSkillChainHeld = true;
            PublishInputState();
            RuntimeDataComponent.Instance.SelectNextSkillChain(SaveDataComponent.Instance?.GetSkillData());
        }

        private void HandleTabCanceled(InputAction.CallbackContext ctx)
        {
            _currentState.IsNextSkillChainHeld = false;
            PublishInputState();
        }

        private void HandleEscape(InputAction.CallbackContext ctx)
        {
            if (_uiInputLocked)
                return;

            _currentState.IsEscapeHeld = true;
            PublishInputState();
            OnEscape?.Invoke();
        }

        private void HandleEscapeCanceled(InputAction.CallbackContext ctx)
        {
            _currentState.IsEscapeHeld = false;
            PublishInputState();
        }

        public void SetBattleInputEnabled(bool enabled)
        {
            _battleInputEnabled = enabled;
            ApplyPlayerInputLockState();
        }
        #endregion

        private void Update()
        {
            if (!_playerInputLocked)
            {
                UpdateWorldPosition();
                UpdateMousePress();
            }
        }
        private void UpdateWorldPosition()
        {
            if (Mouse.current == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector2 screen = Mouse.current.position.ReadValue();
            Ray ray = cam.ScreenPointToRay(screen);

            var plane = new Plane(Vector3.forward, Vector3.zero);
            if (!plane.Raycast(ray, out float enter))
                return;

            Vector3 worldPos = ray.GetPoint(enter);
            worldPos.z = 0f;
            if (_currentState.PointerWorldPosition != worldPos)
            {
                _currentState.PointerWorldPosition = worldPos;
                PublishInputState();
            }

            OnMouseWorldPosition?.Invoke(worldPos);
        }

        private void UpdateMousePress()
        {
            if (_controls == null || !_controls.Interaction.Click.IsPressed())
                return;

            OnMousePress?.Invoke();
        }

        private void HandleGameGateChanged(GameGateChangedEvent gameEvent)
        {
            switch (gameEvent.GateType)
            {
                case GameGateType.PlayerInput:
                    _playerInputLocked = gameEvent.IsLocked;
                    ApplyPlayerInputLockState();
                    break;
                case GameGateType.UIInput:
                    _uiInputLocked = gameEvent.IsLocked;
                    if (_uiInputLocked)
                    {
                        _currentState.IsEscapeHeld = false;
                        PublishInputState();
                    }
                    break;
            }
        }

        private void ApplyPlayerInputLockState()
        {
            if (_controls == null)
                return;

            if (_playerInputLocked)
            {
                _controls.Interaction.Disable();
                _controls.Battle.Disable();
                ClearPlayerInputState();
                return;
            }

            _controls.Interaction.Enable();
            if (_battleInputEnabled)
            {
                _controls.Battle.Enable();
                return;
            }

            _controls.Battle.Disable();
            ClearBattleInputState();
        }

        private void ResetInputState()
        {
            _currentState = new InputState
            {
                SkillChainIndex = -1,
                PropIndex = -1,
            };
        }

        private void ClearPlayerInputState()
        {
            _currentState.Move = Vector2.zero;
            _currentState.IsPrimaryHeld = false;
            _currentState.IsInteractHeld = false;
            _currentState.IsInventoryHeld = false;
            _currentState.IsPropertyHeld = false;
            ClearBattleInputState(publish: false);
            PublishInputState();
        }

        private void ClearBattleInputState(bool publish = true)
        {
            _currentState.IsSkillHeld = false;
            _currentState.SkillChainIndex = -1;
            _currentState.IsNextSkillChainHeld = false;
            _currentState.IsUsePropHeld = false;
            _currentState.PropIndex = -1;
            if (publish)
                PublishInputState();
        }

        private void PublishInputState()
        {
            OnInputStateChanged?.Invoke(_currentState);
        }
    }
}
