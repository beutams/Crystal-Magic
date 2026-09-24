using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public enum ClientPresentationEventType : byte
{
    None = 0,
    Damage = 1,
    SpawnVfx = 2,
    SpawnFollowVfx = 3,
    SpawnLineVfx = 4,
    MoveVfx = 5,
    Sound = 6,
    CameraShake = 7,
    PickupFeedback = 8,
}

public struct ClientPresentationEventElement : IBufferElementData
{
    public uint Frame;
    public uint Sequence;
    public ClientPresentationEventType Type;
    public Entity Source;
    public Entity Target;
    public int SourceSkillId;
    public FixedString128Bytes AssetName;
    public float3 Position;
    public float3 SecondaryPosition;
    public quaternion Rotation;
    public float Scale;
    public float Duration;
    public float ValueA;
    public float ValueB;
    public float ValueC;
    public int IntValue;
    public byte FlagA;
    public byte FlagB;
}
