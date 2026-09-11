using System;
using Unity.Mathematics;
using UnityEngine;

namespace CrystalMagic.Core
{
    /// <summary>
    /// Owns the global runtime debug switch.
    /// </summary>
    public sealed class DebugComponent : GameComponent<DebugComponent>
    {
        [SerializeField]
        private bool _isEnabled = true;

        [Header("Spatial Query Visualization")]
        [SerializeField, Range(3, 128)]
        private int _circleSegmentCount = 32;

        [SerializeField, Range(1, 128)]
        private int _coneArcSegmentCount = 24;

        [SerializeField, Min(1)]
        private int _maxShapesPerFrame = 64;

        [SerializeField]
        private float _zOffset = -0.05f;

        [SerializeField]
        private bool _drawHitMarkers = true;

        [SerializeField, Min(0.01f)]
        private float _hitMarkerSize = 0.12f;

        [SerializeField]
        private Color _circleColor = Color.cyan;

        [SerializeField]
        private Color _forwardRectColor = Color.yellow;

        [SerializeField]
        private Color _coneColor = new Color(1f, 0.5f, 0f, 1f);

        [SerializeField]
        private Color _hitColor = Color.green;

        private bool _isQueryVisualizationSubscribed;
        private bool _drawCurrentQueryHits;
        private int _shapeDrawFrame = -1;
        private int _shapeCountThisFrame;

        public override int Priority => 3;

        public bool IsEnabled => _isEnabled;

        public event Action<bool> EnabledChanged;

        public override void Initialize()
        {
            base.Initialize();
            UpdateQueryVisualizationSubscription();
        }

        public void SetEnabled(bool isEnabled)
        {
            if (_isEnabled == isEnabled)
                return;

            _isEnabled = isEnabled;
            UpdateQueryVisualizationSubscription();
            EnabledChanged?.Invoke(_isEnabled);
        }

        public override void Cleanup()
        {
            UnsubscribeQueryVisualization();
            EnabledChanged = null;
            base.Cleanup();
        }

        private void UpdateQueryVisualizationSubscription()
        {
            if (_isEnabled)
            {
                if (_isQueryVisualizationSubscribed)
                    return;

                DebugQueryShapeReporter.ShapeReported += HandleQueryShapeReported;
                DebugQueryShapeReporter.HitReported += HandleQueryHitReported;
                _isQueryVisualizationSubscribed = true;
                return;
            }

            UnsubscribeQueryVisualization();
        }

        private void UnsubscribeQueryVisualization()
        {
            if (!_isQueryVisualizationSubscribed)
                return;

            DebugQueryShapeReporter.ShapeReported -= HandleQueryShapeReported;
            DebugQueryShapeReporter.HitReported -= HandleQueryHitReported;
            _isQueryVisualizationSubscribed = false;
            _drawCurrentQueryHits = false;
        }

        private void HandleQueryShapeReported(DebugQueryShapeRequest request)
        {
            _drawCurrentQueryHits = TryBeginShapeDraw();
            if (!_drawCurrentQueryHits)
                return;

            switch (request.ShapeType)
            {
                case DebugQueryShapeType.Circle:
                    DrawCircle(request.Origin, request.Radius, _circleColor);
                    break;
                case DebugQueryShapeType.ForwardRect:
                    DrawForwardRect(request.Origin, request.Forward, request.Length, request.Width);
                    break;
                case DebugQueryShapeType.Cone:
                    DrawCone(request.Origin, request.Forward, request.Radius, request.AngleDegrees);
                    break;
            }
        }

        private void HandleQueryHitReported(float3 origin, float3 position)
        {
            if (!_drawCurrentQueryHits || !_drawHitMarkers)
                return;

            Vector3 start = ToDebugPosition(origin);
            Vector3 markerCenter = ToDebugPosition(position);
            Debug.DrawLine(start, markerCenter, _hitColor, 0f, false);

            float halfSize = _hitMarkerSize * 0.5f;
            Debug.DrawLine(markerCenter + Vector3.left * halfSize, markerCenter + Vector3.right * halfSize, _hitColor, 0f, false);
            Debug.DrawLine(markerCenter + Vector3.down * halfSize, markerCenter + Vector3.up * halfSize, _hitColor, 0f, false);
        }

        private bool TryBeginShapeDraw()
        {
            if (!_isEnabled)
                return false;

            int frame = Time.frameCount;
            if (_shapeDrawFrame != frame)
            {
                _shapeDrawFrame = frame;
                _shapeCountThisFrame = 0;
            }

            if (_shapeCountThisFrame >= _maxShapesPerFrame)
                return false;

            _shapeCountThisFrame++;
            return true;
        }

        private void DrawCircle(float3 center, float radius, Color color)
        {
            int segments = Mathf.Clamp(_circleSegmentCount, 3, 128);
            Vector3 previous = ToDebugPosition(center + new float3(radius, 0f, 0f));
            for (int i = 1; i <= segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                Vector3 current = ToDebugPosition(center + new float3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
                Debug.DrawLine(previous, current, color, 0f, false);
                previous = current;
            }
        }

        private void DrawForwardRect(float3 origin, float2 forward, float length, float width)
        {
            float2 right = new(-forward.y, forward.x);
            float halfWidth = width * 0.5f;
            float2 start = origin.xy;
            float2 end = start + forward * length;

            Vector3 corner0 = ToDebugPosition(new float3(start - right * halfWidth, origin.z));
            Vector3 corner1 = ToDebugPosition(new float3(start + right * halfWidth, origin.z));
            Vector3 corner2 = ToDebugPosition(new float3(end + right * halfWidth, origin.z));
            Vector3 corner3 = ToDebugPosition(new float3(end - right * halfWidth, origin.z));

            Debug.DrawLine(corner0, corner1, _forwardRectColor, 0f, false);
            Debug.DrawLine(corner1, corner2, _forwardRectColor, 0f, false);
            Debug.DrawLine(corner2, corner3, _forwardRectColor, 0f, false);
            Debug.DrawLine(corner3, corner0, _forwardRectColor, 0f, false);
        }

        private void DrawCone(float3 origin, float2 forward, float radius, float angleDegrees)
        {
            float clampedAngle = Mathf.Clamp(angleDegrees, 0f, 360f);
            if (clampedAngle >= 359.99f)
            {
                DrawCircle(origin, radius, _coneColor);
                return;
            }

            float halfAngleRadians = clampedAngle * 0.5f * Mathf.Deg2Rad;
            float forwardAngle = Mathf.Atan2(forward.y, forward.x);
            int segments = Mathf.Clamp(Mathf.CeilToInt(_coneArcSegmentCount * clampedAngle / 360f), 1, 128);
            Vector3 originPosition = ToDebugPosition(origin);
            Vector3 previous = originPosition;

            for (int i = 0; i <= segments; i++)
            {
                float progress = i / (float)segments;
                float angle = forwardAngle - halfAngleRadians + clampedAngle * Mathf.Deg2Rad * progress;
                Vector3 current = ToDebugPosition(origin + new float3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
                Debug.DrawLine(previous, current, _coneColor, 0f, false);
                previous = current;
            }

            Debug.DrawLine(previous, originPosition, _coneColor, 0f, false);
        }

        private Vector3 ToDebugPosition(float3 position)
        {
            return new Vector3(position.x, position.y, position.z + _zOffset);
        }
    }
}
