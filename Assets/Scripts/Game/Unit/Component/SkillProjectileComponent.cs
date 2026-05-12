using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

public struct SkillProjectileComponent : IComponentData
{
    public float3 Direction;
    public float Speed;
    public float MaxRange;
    public float TraveledDistance;
    public float HitRadius;
    public float Scale;
    public byte CanPierce;
    public byte TriggerDestroyEffectsOnMaxRange;
}

public struct SkillProjectileSpawnRequestComponent : IComponentData
{
    public FixedString128Bytes ProjectileName;
    public float3 StartPosition;
    public float3 Direction;
    public float Speed;
    public float MaxRange;
    public float HitRadius;
    public float ScaleMultiplier;
    public byte CanPierce;
    public byte TriggerDestroyEffectsOnMaxRange;
}

public struct SkillProjectileHitEntityElement : IBufferElementData
{
    public Entity Value;
}

[MaterialProperty("_StartTime")]
public struct SkillProjectileStartTimeProperty : IComponentData
{
    public float Value;
}

public sealed class SkillProjectilePayloadComponent : IComponentData
{
    public SkillContent Context;
    public EffectData[] OnCollisionEffects;
    public EffectData[] OnDestroyEffects;
}
