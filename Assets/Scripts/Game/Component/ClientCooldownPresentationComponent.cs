using Unity.Entities;

public struct ClientCooldownPresentationComponent : IComponentData
{
    public uint PropCooldownEndFrame;
    public float PropCooldownRemaining;
}
