using Unity.Collections;
using Unity.Entities;

[InternalBufferCapacity(0)]
public struct UnitVariableElement : IBufferElementData
{
    public FixedString128Bytes Key;
    public UnitSourceValue Value;
}
