using UnityEngine;

namespace CrystalMagic.Core
{
    // The game's camera looks straight along Z at the XY map. Keep the whole
    // viewport inside the map, including its furthest rendered depth plane.
    public sealed class CameraWorldBoundsConstraint
    {
        private Bounds _bounds;
        private int _ownerId;
        private Camera _fittedCamera;
        private float _originalSize;
        private float _originalFieldOfView;

        public void Set(int ownerId, Bounds bounds)
        {
            if (ownerId == 0 || bounds.size.x <= 0 || bounds.size.y <= 0 ||
                !IsFinite(bounds.min) || !IsFinite(bounds.max)) return;
            _bounds = bounds;
            _ownerId = ownerId;
        }

        public void Clear(int ownerId)
        {
            if (ownerId != 0 && ownerId == _ownerId) Reset();
        }

        public void Reset()
        {
            RestoreLens();
            _ownerId = 0;
            _bounds = default;
        }

        public void Apply(Camera camera)
        {
            if (_ownerId == 0 || camera == null) return;
            if (_fittedCamera != camera)
            {
                RestoreLens();
                _fittedCamera = camera;
                _originalSize = camera.orthographicSize;
                _originalFieldOfView = camera.fieldOfView;
            }

            float aspect = Mathf.Max(0.0001f, camera.aspect);
            // Re-evaluate against the original lens every frame: resizing to a
            // narrower window or returning to a larger map must allow zoom-out.
            float maxHalfHeight = Mathf.Min(_bounds.extents.y, _bounds.extents.x / aspect);
            if (camera.orthographic)
                camera.orthographicSize = Mathf.Min(_originalSize, maxHalfHeight);
            else
            {
                float distance = FurthestPlaneDistance(camera.transform.position.z);
                float maxFieldOfView = 2f * Mathf.Atan2(maxHalfHeight, distance) * Mathf.Rad2Deg;
                camera.fieldOfView = Mathf.Min(_originalFieldOfView, maxFieldOfView);
            }
            camera.transform.position = ClampPosition(camera, camera.transform.position);
        }

        public Vector3 ClampPosition(Camera camera, Vector3 position)
        {
            if (_ownerId == 0 || camera == null) return position;
            float halfHeight = camera.orthographic ? camera.orthographicSize :
                Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * FurthestPlaneDistance(position.z);
            float halfWidth = halfHeight * Mathf.Max(0.0001f, camera.aspect);
            position.x = ClampAxis(position.x, _bounds.min.x, _bounds.max.x, halfWidth);
            position.y = ClampAxis(position.y, _bounds.min.y, _bounds.max.y, halfHeight);
            return position;
        }

        private float FurthestPlaneDistance(float cameraZ) =>
            Mathf.Max(Mathf.Abs(cameraZ - _bounds.min.z), Mathf.Abs(cameraZ - _bounds.max.z));

        private static float ClampAxis(float value, float minimum, float maximum, float halfView)
        {
            float minCenter = minimum + halfView, maxCenter = maximum - halfView;
            return minCenter > maxCenter ? (minimum + maximum) * 0.5f : Mathf.Clamp(value, minCenter, maxCenter);
        }

        private void RestoreLens()
        {
            if (_fittedCamera != null)
            {
                _fittedCamera.orthographicSize = _originalSize;
                _fittedCamera.fieldOfView = _originalFieldOfView;
            }
            _fittedCamera = null;
        }

        private static bool IsFinite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
