using Unity.Entities;
using Unity.Mathematics;

public struct UnitJumpArcComponent : IComponentData
{
    public float3 StartPosition;
    public float3 EndPosition;
    public float Duration;
    public float Elapsed;
    public float ArcHeight;
    public byte IsActive;
    public byte IsCompleted;
}
