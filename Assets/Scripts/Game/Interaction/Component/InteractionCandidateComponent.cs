using Unity.Entities;

public struct InteractionCandidateComponent : IComponentData
{
    public Entity Target;
    public UnitInteractionData Data;
    public byte IsInteracting;
}
