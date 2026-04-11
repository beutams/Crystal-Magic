using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 场景相机标记组件
    /// 挂载在各场景的主相机上，由 CameraComponent 统一管理
    /// 场景卸载时随场景销毁，无需特殊处理
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SceneCamera : MonoBehaviour
    {
        [SerializeField] private bool _registerOnAwake = true;

        public Camera Camera { get; private set; }

        private void Awake()
        {
            Camera = GetComponent<Camera>();
            if (_registerOnAwake)
                CameraComponent.Instance?.Register(this);
        }

        private void OnDestroy()
        {
            CameraComponent.Instance?.Unregister(this);
        }
    }
}
