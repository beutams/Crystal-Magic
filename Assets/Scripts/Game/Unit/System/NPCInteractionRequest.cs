using Unity.Entities;

public struct NPCInteractionRequest : IComponentData
{
    public Entity Target;
    public byte HasRequest;
}
