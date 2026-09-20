using Unity.Entities;

[InternalBufferCapacity(0)]
public struct UnitVariableConsumerElement : IBufferElementData
{
    public Entity Value;
}
