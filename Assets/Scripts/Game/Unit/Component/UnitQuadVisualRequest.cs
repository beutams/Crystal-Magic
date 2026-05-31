using Unity.Collections;
using Unity.Entities;

public struct UnitQuadVisualRequest : IComponentData
{
    public FixedString128Bytes VisualKey;
    public Entity ExtraVisualEntity;
    public byte IsApplied;
}
