using Unity.Entities;
using Unity.Mathematics;

public struct ClientTransformInterpolationComponent : IComponentData
{
    public float3 FromPosition;
    public float3 TargetPosition;
    public uint FromFrame;
    public uint TargetFrame;
    public double StartRealtime;
    public float Duration;
    public byte Initialized;
}
