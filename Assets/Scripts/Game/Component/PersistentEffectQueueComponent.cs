using Unity.Entities;
using Unity.Mathematics;

public struct PersistentEffectQueueComponent : IComponentData
{
}

public struct PersistentEffectRequest : IBufferElementData
{
    public EffectDataListId PersistentDataId;
    public EffectRequestContext SourceContext;
    public float3 ReleasePosition;
    public byte ReleaseManagedContextAfterConsumption;
}
