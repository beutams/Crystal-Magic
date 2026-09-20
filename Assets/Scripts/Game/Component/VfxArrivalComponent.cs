using Unity.Entities;
using Unity.Mathematics;

public struct VfxArrivalComponent : IComponentData
{
    public float3 StartPosition;
    public float3 EndPosition;
    public float Duration;
    public float Elapsed;
    public EffectRequestContext ArrivalContext;
    public EffectDataListId OnArrivalEffectListId;
    public byte ReleaseManagedContextAfterExecution;
}
