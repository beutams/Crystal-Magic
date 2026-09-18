using Unity.Entities;

public struct ClientEntityLifetimePresentationComponent : IComponentData
{
    public uint RequestedFrame;
    public double DestroyAtRealtime;
    public byte DeathStarted;
    public byte DeathDurationResolved;
    public byte DespawnRequested;
}
