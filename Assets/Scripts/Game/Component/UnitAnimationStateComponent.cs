using Unity.Collections;
using Unity.Entities;

public struct UnitAnimationStateComponent : IComponentData
{
    public FixedString64Bytes AnimationName;
    public uint StartFrame;
    public uint Sequence;
    public byte NetworkDirty;
}
