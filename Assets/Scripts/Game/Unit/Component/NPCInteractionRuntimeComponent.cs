using Unity.Entities;

public struct NPCInteractionRuntimeComponent : IComponentData
{
    public Entity CurrentTarget;
    public Entity RequestedTarget;
    public byte HasPendingRequest;
}
