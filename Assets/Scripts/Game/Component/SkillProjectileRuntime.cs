using Unity.Entities;
using Unity.Mathematics;

public struct SkillProjectileHitEntityElement : IBufferElementData
{
    public Entity Value;
}

public struct SkillProjectileConditionInstructionElement : IBufferElementData
{
    public ExpressionInstruction Value;
}

public struct SkillProjectileConditionLiteralElement : IBufferElementData
{
    public UnitSourceValue Value;
}

public enum SkillProjectileConditionState : byte
{
    None,
    Valid,
    Invalid,
}

public struct SkillProjectilePayloadComponent : IComponentData
{
    public EffectRequestContext Context;
    public byte OwnsManagedContext;
    public SkillProjectileConditionState CollisionConditionState;
    public EffectDataListId OnCollisionEffectListId;
    public EffectDataListId OnDestroyEffectListId;
}

public struct SkillProjectileFrameResultComponent : IComponentData
{
    public Entity HitEntity;
    public float3 HitPosition;
    public byte HasHit;
    public byte ShouldDestroy;
    public byte TriggerDestroyEffects;
    public byte DestroyUsesHitContext;
}

public struct SkillProjectileVisualLinkComponent : IComponentData
{
    public Entity VisualEntity;
}
