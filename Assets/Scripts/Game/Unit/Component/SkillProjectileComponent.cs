using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

public enum SkillProjectileSpawnRequestKind : byte
{
    Projectile = 0,
    DestroyVfx = 1,
}

public sealed class SkillProjectileSpawnRequest
{
    public SkillProjectileSpawnRequestKind Kind;
    public FixedString128Bytes ProjectileName;
    public float3 StartPosition;
    public float3 Direction;
    public quaternion Rotation;
    public float Speed;
    public float MaxRange;
    public float HitRadius;
    public float ScaleMultiplier;
    public byte CanPierce;
    public byte TriggerDestroyEffectsOnMaxRange;
    public SkillContent Context;
    public Texture2D FlightTexture;
    public int FlightFrameCount;
    public Texture2D DestroyTexture;
    public int DestroyFrameCount;
    public EffectData[] OnCollisionEffects;
    public EffectData[] OnDestroyEffects;
}

public static class SkillProjectileSpawnQueue
{
    private static readonly System.Collections.Generic.Queue<SkillProjectileSpawnRequest> s_requests = new();

    public static void Enqueue(SkillProjectileSpawnRequest request)
    {
        if (request == null)
            return;

        s_requests.Enqueue(request);
    }

    public static bool TryDequeue(out SkillProjectileSpawnRequest request)
    {
        if (s_requests.Count > 0)
        {
            request = s_requests.Dequeue();
            return true;
        }

        request = null;
        return false;
    }

    public static bool HasPendingRequests => s_requests.Count > 0;
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
