using Unity.Entities;

public struct InteractionRequestSnapshot
{
    public Entity Target;
    public UnitInteractionData Data;

    public bool IsValid => Target != Entity.Null && Data.IsValid;
}

public struct GameInteractionRequest : IComponentData
{
    public Entity Actor;
    public Entity Target;
    public UnitInteractionData Data;
    public byte HasRequest;
}
