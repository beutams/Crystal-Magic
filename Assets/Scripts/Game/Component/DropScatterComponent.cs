using Unity.Entities;
using Unity.Mathematics;

public struct DropScatterComponent : IComponentData
{
    public float3 StartPosition;
    public float3 TargetPosition;
    public float DurationSeconds;
    public float ElapsedSeconds;
    public float ArcHeight;
    public byte IsLanded;
}

public static class DropScatterUtility
{
    public static float3 Evaluate(
        in float3 startPosition,
        in float3 targetPosition,
        float arcHeight,
        float elapsedSeconds,
        float durationSeconds)
    {
        if (durationSeconds <= 0.0001f)
            return targetPosition;

        float progress = math.saturate(elapsedSeconds / durationSeconds);
        float3 position = math.lerp(startPosition, targetPosition, progress);
        position.y += math.max(0f, arcHeight) * 4f * progress * (1f - progress);
        return position;
    }
}
