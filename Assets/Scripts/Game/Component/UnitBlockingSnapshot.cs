using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

// Runtime-only starting poses for the current physics step. Never baked into a
// unit prefab, networked, or retained across a prediction replay step.
[InternalBufferCapacity(0)]
public struct UnitBlockingSnapshot : IBufferElementData
{
    public RigidBody Body;
    public UnitFactionType Faction;
    public float2 Velocity;
    public byte Writable;
}
