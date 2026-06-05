using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CrystalMagic.Core {
    /// <summary>
    /// 输入组件
    /// </summary>
    public class InputComponent : GameComponent<InputComponent>
    {
        public override int Priority => 5;
        private InputControls _controls;
        private bool _playerInputLocked;
        private bool _uiInputLocked;

        #region 事件
        public event Action<Vector2> OnMove;
        public event Action<Vector3> OnMouseWorldPosition;
        public event Action OnMouseClick;
        public event Action OnMousePress;
        public event Action OnInteract;
        public event Action OnInventory;
        public event Action OnProperty;
        public event Action OnEscape;
        public event Action<int> OnPropShortcut;
        #endregion

        #region 调用
        public override void Initialize()
        {
            base.Initialize();
            _controls = new InputControls();

            _controls.Town.Move.performed += HandleMove;
            _controls.Town.Move.canceled += HandleMoveCanceled;
            _controls.Town.Interact.performed += HandleInteract;
            _controls.Town.Click.performed += HandleClick;
            _controls.Town.Inventory.performed += HandleInventory;
            _controls.Town.Property.performed += HandleProperty;
            _controls.Town.Skill.performed += HandleSkill;
            _controls.Town.Tab.performed += HandleTab;

            _controls.Town.Enable();
            _playerInputLocked = GameGateComponent.Instance.IsPlayerInputLocked;
            _uiInputLocked = GameGateComponent.Instance.IsUIInputLocked;
            ApplyPlayerInputLockState();
            EventComponent.Instance.Subscribe<GameGateChangedEvent>(HandleGameGateChanged);
        }

        public override void Cleanup()
        {
            EventComponent.Instance.Unsubscribe<GameGateChangedEvent>(HandleGameGateChanged);
            if (_controls != null)
            {
                _controls.Town.Move.performed -= HandleMove;
                _controls.Town.Move.canceled -= HandleMoveCanceled;
                _controls.Town.Interact.performed -= HandleInteract;
                _controls.Town.Click.performed -= HandleClick;
                _controls.Town.Inventory.performed -= HandleInventory;
                _controls.Town.Property.performed -= HandleProperty;
                _controls.Town.Skill.performed -= HandleSkill;
                _controls.Town.Tab.performed -= HandleTab;

                _controls.Town.Disable();
                _controls.Dispose();
                _controls = null;
            }

            base.Cleanup();
        }
        private void HandleMove(InputAction.CallbackContext ctx) => OnMove?.Invoke(ctx.ReadValue<Vector2>());
        private void HandleMoveCanceled(InputAction.CallbackContext ctx) => OnMove?.Invoke(Vector2.zero);
        private void HandleClick(InputAction.CallbackContext ctx) => OnMouseClick?.Invoke();
        private void HandleInteract(InputAction.CallbackContext ctx) => OnInteract?.Invoke();
        private void HandleInventory(InputAction.CallbackContext ctx) => OnInventory?.Invoke();
        private void HandleProperty(InputAction.CallbackContext ctx) => OnProperty?.Invoke();
        private void HandleSkill(InputAction.CallbackContext ctx)
        {
            int skillChainNumber = Mathf.RoundToInt(ctx.ReadValue<float>());
            int skillChainIndex = skillChainNumber - 1;
            if (skillChainIndex < 0 || skillChainIndex >= 5)
                return;

            RuntimeDataComponent.Instance.SetCurrentSkillChainIndex(skillChainIndex, SaveDataComponent.Instance?.GetSkillData());
        }
        private void HandleTab(InputAction.CallbackContext ctx)
        {
            RuntimeDataComponent.Instance.SelectNextSkillChain(SaveDataComponent.Instance?.GetSkillData());
        }
        #endregion


        private void Update()
        {
            if (!_playerInputLocked)
            {
                UpdateWorldPosition();
                UpdateMousePress();
                UpdatePropShortcuts();
            }
            UpdateEscape();
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
            OnMouseWorldPosition?.Invoke(worldPos);
        }

        private void UpdateMousePress()
        {
            if (_controls == null || !_controls.Town.Click.IsPressed())
                return;

            OnMousePress?.Invoke();
        }

        private void UpdateEscape()
        {
            if (_uiInputLocked)
                return;

            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
                return;

            OnEscape?.Invoke();
        }

        private void UpdatePropShortcuts()
        {
            if (Keyboard.current == null)
                return;

            if (Keyboard.current.f1Key.wasPressedThisFrame)
                OnPropShortcut?.Invoke(0);
            if (Keyboard.current.f2Key.wasPressedThisFrame)
                OnPropShortcut?.Invoke(1);
            if (Keyboard.current.f3Key.wasPressedThisFrame)
                OnPropShortcut?.Invoke(2);
            if (Keyboard.current.f4Key.wasPressedThisFrame)
                OnPropShortcut?.Invoke(3);
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
                    break;
            }
        }

        private void ApplyPlayerInputLockState()
        {
            if (_controls == null)
                return;

            if (_playerInputLocked)
            {
                _controls.Town.Disable();
                return;
            }

            _controls.Town.Enable();
        }
    }
}
