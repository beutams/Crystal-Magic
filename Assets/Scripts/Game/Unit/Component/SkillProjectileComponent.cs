using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

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
    public FixedString128Bytes ProjectileName;
    public SkillContent Context;
    public Texture2D FlightTexture;
    public int FlightFrameCount;
    public Texture2D DestroyTexture;
    public int DestroyFrameCount;
    public EffectData[] OnCollisionEffects;
    public EffectData[] OnDestroyEffects;
}
