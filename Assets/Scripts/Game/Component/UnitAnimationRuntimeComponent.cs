using Unity.Entities;

public struct UnitAnimationSampleComponent : IComponentData
{
    public int TrackId;
    public int SpriteIndex;
    public byte FlipX;
    public byte HasFlipX;
    public byte Initialized;
    public uint Revision;
    public uint AppliedRevision;
}
