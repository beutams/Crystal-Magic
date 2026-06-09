using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public sealed class SkillProjectileSpawnRequest
{
    public FixedString128Bytes ProjectileName;
    public float3 StartPosition;
    public float3 Direction;
    public quaternion Rotation;
    public float Speed;
    public float MaxRange;
    public float HitRadius;
    public float Width;
    public float Height;
    public byte CanPierce;
    public byte TriggerDestroyEffectsOnMaxRange;
    public SkillContent Context;
    public Texture2D FlightTexture;
    public int FlightGridColumns;
    public int FlightGridRows;
    public int FlightFrameCount;
    public float FlightFramesPerSecond;
    public Texture2D DestroyTexture;
    public int DestroyGridColumns;
    public int DestroyGridRows;
    public int DestroyFrameCount;
    public float DestroyFramesPerSecond;
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

public struct SkillProjectileComponent : IComponentData
{
    public float3 Direction;
    public float Speed;
    public float MaxRange;
    public float TraveledDistance;
    public float HitRadius;
    public byte CanPierce;
    public byte TriggerDestroyEffectsOnMaxRange;
    public byte IsDestroying;
}

public sealed class SkillProjectilePayloadComponent : IComponentData
{
    public FixedString128Bytes ProjectileName;
    public SkillContent Context;
    public Texture2D FlightTexture;
    public int FlightGridColumns;
    public int FlightGridRows;
    public int FlightFrameCount;
    public float FlightFramesPerSecond;
    public float FlightWidth;
    public float FlightHeight;
    public Texture2D DestroyTexture;
    public int DestroyGridColumns;
    public int DestroyGridRows;
    public int DestroyFrameCount;
    public float DestroyFramesPerSecond;
    public float DestroyWidth;
    public float DestroyHeight;
    public EffectData[] OnCollisionEffects;
    public EffectData[] OnDestroyEffects;
}
