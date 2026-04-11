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
        #region 事件
        /// <summary>
        /// WASD 移动输入。
        /// </summary>
        public event Action<Vector2> OnMove;
        /// <summary>
        /// 鼠标在2D平面上的世界坐标。
        /// </summary>
        public event Action<Vector3> OnMouseWorldPosition;
        /// <summary>
        /// 鼠标点击
        /// </summary>
        public event Action OnMouseClick;
        #endregion

        #region 调用
        public override void Initialize()
        {
            base.Initialize();
            _controls = new InputControls();

            _controls.Town.Move.performed += HandleMove;
            _controls.Town.Move.canceled += HandleMoveCanceled;
            _controls.Town.Click.performed += HandleClick;
            _controls.Town.Click.canceled += HandleClickCanceled;
            _controls.Town.Enable();
        }

        public override void Cleanup()
        {
            if (_controls != null)
            {
                _controls.Town.Move.performed -= HandleMove;
                _controls.Town.Move.canceled -= HandleMoveCanceled;

                _controls.Town.Disable();
                _controls.Dispose();
                _controls = null;
            }

            base.Cleanup();
        }
        private void HandleMove(InputAction.CallbackContext ctx)
            => OnMove?.Invoke(ctx.ReadValue<Vector2>());
        private void HandleMoveCanceled(InputAction.CallbackContext ctx)
            => OnMove?.Invoke(Vector2.zero);
        private void HandleClick(InputAction.CallbackContext ctx)
            => OnMouseClick?.Invoke();
        private void HandleClickCanceled(InputAction.CallbackContext ctx)
            => OnMouseClick?.Invoke();


        #endregion


        private void Update()
        {
            UpdateWorldPosition();
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
    }
}
