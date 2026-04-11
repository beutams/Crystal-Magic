using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 相机管理组件
    /// 追踪当前场景的主相机，提供全局访问入口
    /// 各场景相机挂载 SceneCamera 组件后自动注册/注销
    /// </summary>
    public class CameraComponent : GameComponent<CameraComponent>
    {
        public override int Priority => 13;

        private SceneCamera _current;

        /// <summary>当前活跃的场景相机</summary>
        public Camera Current => _current != null ? _current.Camera : Camera.main;

        public void Register(SceneCamera cam)
        {
            _current = cam;
            Debug.Log($"[CameraComponent] Registered: {cam.gameObject.name}");
        }

        public void Unregister(SceneCamera cam)
        {
            if (_current == cam)
            {
                _current = null;
                Debug.Log($"[CameraComponent] Unregistered: {cam.gameObject.name}");
            }
        }
        public override void Cleanup()
        {
            _current = null;
            base.Cleanup();
        }
    }
}
