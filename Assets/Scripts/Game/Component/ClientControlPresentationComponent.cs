using Unity.Entities;

public struct ClientControlPresentationComponent : IComponentData
{
    public UnitControlType DisplayType;
    public Entity SourceEntity;
    public uint EndFrame;
    public uint Revision;
    public float DisplayRemainingTime;
    public byte Active;
}
