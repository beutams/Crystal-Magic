using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public enum UnitControlType : byte
{
    None = 0,
    Knockback = 1,
    Stun = 2,
    Fear = 3,
}

public struct UnitControlRuntimeEntry
{
    public UnitControlType ControlType;
    public float RemainingTime;
    public int Priority;
    public byte LockMove;
    public byte LockCast;
    public byte InterruptOnApply;
    public Entity SourceEntity;
    public float2 MotionVelocity;
    public float MotionDamping;
}

public struct UnitControlRuntimeComponent : IComponentData
{
    public FixedList512Bytes<UnitControlRuntimeEntry> Entries;
    public UnitControlType ActiveType;
    public float ActiveRemainingTime;
    public int ActivePriority;
    public byte LockMove;
    public byte LockCast;
    public byte HasControl;
    public Entity ActiveSourceEntity;
    public float2 ActiveMotionVelocity;
    public float ActiveMotionDamping;
}
