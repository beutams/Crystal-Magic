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
        private const float QueryShapeLifetimeSeconds = 1f;
        private readonly System.Collections.Generic.List<ActiveQueryShape> _activeShapes = new();
        private readonly System.Collections.Generic.List<ActiveDebugLine> _activeLines = new();

        private struct ActiveQueryShape
        {
            public DebugQueryShapeRequest Request;
            public float ExpiresAt;
        }

        private struct ActiveDebugLine
        {
            public Vector3 Start;
            public Vector3 End;
            public Color Color;
            public float ExpiresAt;
        }

        public override int Priority => 3;

        public bool IsEnabled => _isEnabled;

        public event Action<bool> EnabledChanged;

        // Allow gameplay scenes such as TrainingScene to be played directly in the
        // editor without relying on Start.unity's persistent GameEntry hierarchy.
        protected override void Awake()
        {
            if (!InitializeSingletonInstance(this))
                return;

            UpdateQueryVisualizationSubscription();
        }

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
            _activeShapes.Clear();
            _activeLines.Clear();
            EnabledChanged = null;
            base.Cleanup();
        }

        private void LateUpdate()
        {
            if (!_isEnabled)
            {
                _activeShapes.Clear();
                _activeLines.Clear();
                return;
            }

            float now = Time.unscaledTime;
            for (int i = _activeShapes.Count - 1; i >= 0; i--)
            {
                ActiveQueryShape shape = _activeShapes[i];
                float remaining = shape.ExpiresAt - now;
                if (remaining <= 0f)
                {
                    _activeShapes.RemoveAt(i);
                    continue;
                }

                Color color = GetFadedColor(shape.Request.ShapeType switch
                {
                    DebugQueryShapeType.Circle => _circleColor,
                    DebugQueryShapeType.ForwardRect => _forwardRectColor,
                    _ => _coneColor,
                }, remaining);
                switch (shape.Request.ShapeType)
                {
                    case DebugQueryShapeType.Circle:
                        DrawCircle(shape.Request.Origin, shape.Request.Radius, color);
                        break;
                    case DebugQueryShapeType.ForwardRect:
                        DrawForwardRect(shape.Request.Origin, shape.Request.Forward, shape.Request.Length, shape.Request.Width, color);
                        break;
                    case DebugQueryShapeType.Cone:
                        DrawCone(shape.Request.Origin, shape.Request.Forward, shape.Request.Radius, shape.Request.AngleDegrees, color);
                        break;
                }
            }

            for (int i = _activeLines.Count - 1; i >= 0; i--)
            {
                ActiveDebugLine line = _activeLines[i];
                float remaining = line.ExpiresAt - now;
                if (remaining <= 0f)
                {
                    _activeLines.RemoveAt(i);
                    continue;
                }

                Debug.DrawLine(line.Start, line.End, GetFadedColor(line.Color, remaining), 0f, false);
            }
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

            _activeShapes.Add(new ActiveQueryShape
            {
                Request = request,
                ExpiresAt = Time.unscaledTime + QueryShapeLifetimeSeconds,
            });
        }

        private void HandleQueryHitReported(float3 origin, float3 position)
        {
            if (!_drawCurrentQueryHits || !_drawHitMarkers)
                return;

            Vector3 start = ToDebugPosition(origin);
            Vector3 markerCenter = ToDebugPosition(position);
            float halfSize = _hitMarkerSize * 0.5f;
            float expiresAt = Time.unscaledTime + QueryShapeLifetimeSeconds;
            _activeLines.Add(new ActiveDebugLine
            {
                Start = start,
                End = markerCenter,
                Color = _hitColor,
                ExpiresAt = expiresAt,
            });
            _activeLines.Add(new ActiveDebugLine
            {
                Start = markerCenter + Vector3.left * halfSize,
                End = markerCenter + Vector3.right * halfSize,
                Color = _hitColor,
                ExpiresAt = expiresAt,
            });
            _activeLines.Add(new ActiveDebugLine
            {
                Start = markerCenter + Vector3.down * halfSize,
                End = markerCenter + Vector3.up * halfSize,
                Color = _hitColor,
                ExpiresAt = expiresAt,
            });
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

        private void DrawForwardRect(float3 origin, float2 forward, float length, float width, Color color)
        {
            float2 right = new(-forward.y, forward.x);
            float halfWidth = width * 0.5f;
            float2 start = origin.xy;
            float2 end = start + forward * length;

            Vector3 corner0 = ToDebugPosition(new float3(start - right * halfWidth, origin.z));
            Vector3 corner1 = ToDebugPosition(new float3(start + right * halfWidth, origin.z));
            Vector3 corner2 = ToDebugPosition(new float3(end + right * halfWidth, origin.z));
            Vector3 corner3 = ToDebugPosition(new float3(end - right * halfWidth, origin.z));

            Debug.DrawLine(corner0, corner1, color, 0f, false);
            Debug.DrawLine(corner1, corner2, color, 0f, false);
            Debug.DrawLine(corner2, corner3, color, 0f, false);
            Debug.DrawLine(corner3, corner0, color, 0f, false);
        }

        private void DrawCone(float3 origin, float2 forward, float radius, float angleDegrees, Color color)
        {
            float clampedAngle = Mathf.Clamp(angleDegrees, 0f, 360f);
            if (clampedAngle >= 359.99f)
            {
                DrawCircle(origin, radius, color);
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
                Debug.DrawLine(previous, current, color, 0f, false);
                previous = current;
            }

            Debug.DrawLine(previous, originPosition, color, 0f, false);
        }

        private static Color GetFadedColor(Color color, float remaining)
        {
            color.a *= Mathf.Clamp01(remaining / QueryShapeLifetimeSeconds);
            return color;
        }

        private Vector3 ToDebugPosition(float3 position)
        {
            return new Vector3(position.x, position.y, position.z + _zOffset);
        }
    }
}
