using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct ClientSkillVisualRuntimeComponent : IComponentData
{
    public uint RollbackFrame;
    public byte HasPendingRollback;
}

[InternalBufferCapacity(0)]
public struct ClientSkillVisualRequestElement : IBufferElementData
{
    public uint Frame;
    public int RequestOrdinal;
    public SkillReleaseRequest Request;
}

[InternalBufferCapacity(0)]
public struct ClientPredictedSkillVisualEventElement : IBufferElementData
{
    public uint Frame;
    public int RequestOrdinal;
    public int EffectOrdinal;
    public int SkillId;
    public ClientPresentationEventType Type;
    public Entity Source;
    public Entity VisualEntity;
    public Entity AnchorEntity;
    public FixedString128Bytes AssetName;
    public float3 Position;
    public byte IsProjectile;
    public byte IsConfirmed;
}

public struct ClientPredictedSkillVisualComponent : IComponentData
{
    public uint Frame;
    public int RequestOrdinal;
    public int EffectOrdinal;
    public int SegmentOrdinal;
}

public struct ClientPredictedProjectileComponent : IComponentData
{
    public float3 Direction;
    public float Speed;
    public float MaxRange;
    public float TraveledDistance;
}
