using Unity.Entities;

public struct PlayerPropCooldownComponent : IComponentData
{
    public float SharedCooldownRemaining;
    public byte NetworkDirty;
}
