using System;
using Unity.Mathematics;

namespace CrystalMagic.Core
{
    public enum DebugQueryShapeType
    {
        Circle,
        ForwardRect,
        Cone,
    }

    /// <summary>
    /// Describes one spatial-query volume in the gameplay XY plane.
    /// </summary>
    public readonly struct DebugQueryShapeRequest
    {
        public DebugQueryShapeType ShapeType { get; }
        public float3 Origin { get; }
        public float2 Forward { get; }
        public float Radius { get; }
        public float Length { get; }
        public float Width { get; }
        public float AngleDegrees { get; }

        private DebugQueryShapeRequest(
            DebugQueryShapeType shapeType,
            float3 origin,
            float2 forward,
            float radius,
            float length,
            float width,
            float angleDegrees)
        {
            ShapeType = shapeType;
            Origin = origin;
            Forward = forward;
            Radius = radius;
            Length = length;
            Width = width;
            AngleDegrees = angleDegrees;
        }

        public static DebugQueryShapeRequest Circle(float3 center, float radius)
        {
            return new DebugQueryShapeRequest(DebugQueryShapeType.Circle, center, float2.zero, radius, 0f, 0f, 0f);
        }

        public static DebugQueryShapeRequest ForwardRect(float3 origin, float2 forward, float length, float width)
        {
            return new DebugQueryShapeRequest(DebugQueryShapeType.ForwardRect, origin, forward, 0f, length, width, 0f);
        }

        public static DebugQueryShapeRequest Cone(float3 origin, float2 forward, float radius, float angleDegrees)
        {
            return new DebugQueryShapeRequest(DebugQueryShapeType.Cone, origin, forward, radius, 0f, 0f, angleDegrees);
        }
    }

    /// <summary>
    /// Bridges managed spatial queries to the global runtime debug renderer.
    /// </summary>
    public static class DebugQueryShapeReporter
    {
        public static event Action<DebugQueryShapeRequest> ShapeReported;
        public static event Action<float3, float3> HitReported;

        public static void ReportCircle(float3 center, float radius)
        {
            ShapeReported?.Invoke(DebugQueryShapeRequest.Circle(center, radius));
        }

        public static void ReportForwardRect(float3 origin, float2 forward, float length, float width)
        {
            ShapeReported?.Invoke(DebugQueryShapeRequest.ForwardRect(origin, forward, length, width));
        }

        public static void ReportCone(float3 origin, float2 forward, float radius, float angleDegrees)
        {
            ShapeReported?.Invoke(DebugQueryShapeRequest.Cone(origin, forward, radius, angleDegrees));
        }

        public static void ReportHit(float3 origin, float3 position)
        {
            HitReported?.Invoke(origin, position);
        }
    }
}
