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
        [SerializeField] private bool _followPlayerTag = true;
        [SerializeField] private Vector2 _followOffset = Vector2.zero;
        [SerializeField, Min(0f)] private float _followSmooth = 0f;

        public Camera Camera { get; private set; }
        public bool FollowPlayerTag => _followPlayerTag;
        public float FollowSmooth => _followSmooth;

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

        public Vector3 GetDesiredPosition(Vector3 targetPosition, Vector3 currentCameraPosition)
        {
            return new Vector3(
                targetPosition.x + _followOffset.x,
                targetPosition.y + _followOffset.y,
                currentCameraPosition.z
            );
        }
    }
}
