using Unity.Entities;

public struct ClientBuffPresentationElement : IBufferElementData
{
    public int BuffId;
    public int SourceSkillId;
    public Entity OriginEntity;
    public uint EndFrame;
    public int StackCount;
    public float DisplayRemainingTime;
    public Entity VisualEntity;
}
