using Unity.Entities;

public enum PlayerInteractionKind : byte
{
    None = 0,
    Drop = 1,
    Treasure = 2,
    Npc = 3,
}

public struct PlayerInteractionRuntimeComponent : IComponentData
{
    public Entity CurrentTarget;
    public PlayerInteractionKind CurrentKind;
}
