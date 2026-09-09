using Unity.Entities;
using Unity.Mathematics;

public struct QuadOverlayPulseComponent : IComponentData, IEnableableComponent
{
    public float4 OverlayColor;
    public float DurationSeconds;
    public float RemainingSeconds;
    public float PeakStrength;
}
