using Unity.Entities;
using Unity.Mathematics;

public enum UnitControlType : byte
{
    None = 0,
    Knockback = 1,
    Stun = 2,
    Fear = 3,
}

[InternalBufferCapacity(4)]
public struct UnitControlElement : IBufferElementData
{
    public UnitControlType ControlType;
    public float RemainingTime;
    public int Priority;
    public byte LockMove;
    public byte LockCast;
    public byte InterruptOnApply;
    public Entity SourceEntity;
}

public struct UnitControlStateComponent : IComponentData
{
    public UnitControlType ActiveType;
    public float RemainingTime;
    public int ActivePriority;
    public byte LockMove;
    public byte LockCast;
    public byte HasControl;
    public Entity ActiveSourceEntity;
}

public struct UnitKnockbackComponent : IComponentData
{
    public float2 Velocity;
    public float Damping;
}
